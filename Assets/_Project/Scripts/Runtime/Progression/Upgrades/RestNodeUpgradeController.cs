using System;
using System.Collections.Generic;
using System.IO;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Upgrades
{
    /// <summary>
    /// 管理节点内两名角色各自的可重复购买三选一商店。
    /// 主机生成并验证结果；客机只提交刷新/选择请求。
    /// </summary>
    public sealed class RestNodeUpgradeController : MonoBehaviour
    {
        private const byte NetworkSnapshot = NetworkMessageCatalog.Authority.UpgradeSnapshot;
        private const byte NetworkResult = NetworkMessageCatalog.Authority.UpgradeResult;
        private const byte NetworkRequest = NetworkMessageCatalog.Peer.UpgradeRequest;
        private const byte RequestRefresh = 1;
        private const byte RequestSelect = 2;
        private const int OfferCount = 3;
        private const int PaidRefreshCost = 5;

        [SerializeField] private PlayerUpgradeRuntimeState _runtimeState;
        [SerializeField] private RestNodeUpgradePanelView _panel;
        [SerializeField] private PlayerControlAssignment _controlAssignment;
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private TokenWallet _wallet;
        [SerializeField] private int _chapterSeed = 20260910;

        private readonly List<UpgradeDefinition> _candidates = new();
        private readonly OfferState _deepSeek = new();
        private readonly OfferState _harness = new();
        private int _nodeSerial;
        private bool _nodeActive;
        private bool _isInitialized;
        private uint _resultSequence, _lastResultSequence;
        private bool _receivedResult;
        private ProgressCheckpoint _checkpoint;

        public bool HasCheckpoint => _checkpoint != null;

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;
        private bool CanAuthor => _session == null || _session.Phase == SessionPhase.Offline ||
            _session.IsAuthority;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(RestNodeUpgradeController)}] 装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_session != null)
            {
                _session.AuthorityMessage += ReadAuthorityMessage;
                _session.PeerMessage += ReadPeerMessage;
                _session.PeerJoined += OnPeerJoined;
                _session.SessionOpened += OnSessionOpened;
                _session.SessionClosed += ResetAudioResults;
            }
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.AuthorityMessage -= ReadAuthorityMessage;
                _session.PeerMessage -= ReadPeerMessage;
                _session.PeerJoined -= OnPeerJoined;
                _session.SessionOpened -= OnSessionOpened;
                _session.SessionClosed -= ResetAudioResults;
            }
        }

        private void OnSessionOpened(bool authority) => ResetAudioResults();

        private void ResetAudioResults()
        {
            _resultSequence = _lastResultSequence = 0;
            _receivedResult = false;
        }

        public void BeginNode()
        {
            if (!_isInitialized || _nodeActive || !CanAuthor)
            {
                return;
            }

            _nodeActive = true;
            _wallet.SettleBattle();
            _panel.Hide();
            _nodeSerial++;
            ResetOffer(_deepSeek);
            ResetOffer(_harness);
            GenerateOffers(PlayerRole.DeepSeek, _deepSeek);
            GenerateOffers(PlayerRole.Harness, _harness);
            BroadcastSnapshot();
        }

        public void EndNode()
        {
            if (!CanAuthor) return;
            if (!_nodeActive)
            {
                _panel.Hide();
                return;
            }

            _nodeActive = false;
            _panel.Hide();
            _wallet.BeginBattle();
            CaptureProgressCheckpoint();
            BroadcastSnapshot();
        }

        /// <summary>
        /// 最后一段战斗直接进入章节结算，不经过休息节点；因此由章节流程
        /// 显式提交本段共同收益，但不会开启下一场战斗。
        /// </summary>
        public void SettleFinalBattle()
        {
            if (!_isInitialized || !CanAuthor)
            {
                return;
            }

            _nodeActive = false;
            _panel.Hide();
            _wallet.SettleBattle();
            BroadcastSnapshot();
        }

        public void CaptureProgressCheckpoint()
        {
            if (!_isInitialized || !CanAuthor) return;
            _checkpoint = new ProgressCheckpoint(
                _nodeSerial,
                CaptureOffer(_deepSeek),
                CaptureOffer(_harness),
                _wallet.CaptureSnapshot(),
                _runtimeState.CaptureSnapshot());
        }

        public bool RestoreProgressCheckpoint(bool activateNode)
        {
            if (!TryValidateCheckpoint(out _))
            {
                return false;
            }

            _nodeSerial = _checkpoint.NodeSerial;
            RestoreOffer(_checkpoint.DeepSeek, _deepSeek);
            RestoreOffer(_checkpoint.Harness, _harness);
            _wallet.RestoreSnapshot(_checkpoint.Wallet);
            _runtimeState.RestoreSnapshot(_checkpoint.Upgrades);
            _nodeActive = activateNode;
            if (activateNode)
            {
                // 检查点保存于离开节点之后；返回节点时只把战斗标记为
                // 已结算，不会加入失败战斗中产生的任何收益。
                _wallet.SettleBattle();
            }
            _panel.Hide();
            BroadcastSnapshot();
            return true;
        }

        /// <summary>副本只跟随节点打开交互；钱包和强化内容只读权威快照，不本地结算。</summary>
        public void SetReplicaNodeActive(bool active)
        {
            if (!_isInitialized || CanAuthor || _nodeActive == active) return;
            _nodeActive = active;
            if (!active) _panel.Hide();
        }

        /// <summary>恢复事务提交前的只读检查，不写钱包、卡牌或角色状态。</summary>
        public bool TryValidateCheckpoint(out string reason)
        {
            if (!_isInitialized || !isActiveAndEnabled || !CanAuthor || _checkpoint == null)
            {
                reason = "强化检查点不存在、控制器未启用或本端无恢复权限。";
                return false;
            }
            if (!TryValidateConfiguration(out reason)) return false;
            if (_checkpoint.NodeSerial < 0 || !IsValidOfferCheckpoint(_checkpoint.DeepSeek) ||
                !IsValidOfferCheckpoint(_checkpoint.Harness) ||
                _checkpoint.Wallet.DeepSeekBalance < 0 || _checkpoint.Wallet.HarnessBalance < 0 ||
                _checkpoint.Wallet.BattleEarned < 0 || _checkpoint.Upgrades == null ||
                !IsValidRanks(_checkpoint.Upgrades.DeepSeek) ||
                !IsValidRanks(_checkpoint.Upgrades.Harness) ||
                !IsValidRanks(_checkpoint.Upgrades.Team))
            {
                reason = "强化检查点的商店、余额或等级数据无效；已阻止部分恢复。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private bool IsValidOfferCheckpoint(OfferCheckpoint offer)
        {
            if (offer == null || offer.Offers == null || offer.Offers.Length != OfferCount ||
                offer.RefreshCount < 0 || offer.PurchaseCount < 0) return false;
            for (int index = 0; index < offer.Offers.Length; index++)
                if (offer.Offers[index] != default &&
                    !_runtimeState.Catalog.TryGet(offer.Offers[index], out _)) return false;
            return true;
        }

        private bool IsValidRanks(Dictionary<UpgradeCardId, int> ranks)
        {
            if (ranks == null) return false;
            foreach (var pair in ranks)
                if (!_runtimeState.Catalog.TryGet(pair.Key, out UpgradeDefinition definition) ||
                    pair.Value < 0 || pair.Value > definition.MaximumRank) return false;
            return true;
        }

        public bool OpenForRole(PlayerRole role)
        {
            if (!_nodeActive)
            {
                GameAppRoot.Instance?.Audio?.Play(AudioCue.UiReject);
                return false;
            }

            if (IsOnline && role != _controlAssignment.CurrentLocalPlayerRole)
            {
                GameAppRoot.Instance?.Audio?.Play(AudioCue.UiReject);
                return false;
            }

            bool newlyOpened = !IsPanelShowing(role);
            RefreshPanel(role);
            if (newlyOpened) GameAppRoot.Instance?.Audio?.Play(AudioCue.UiOpen);
            return true;
        }

        private void RefreshPanel(PlayerRole role)
        {
            OfferState state = GetOffer(role);
            var definitions = new UpgradeDefinition[OfferCount];
            for (int index = 0; index < OfferCount; index++)
            {
                _runtimeState.Catalog.TryGet(
                    state.Offers[index],
                    out definitions[index]);
            }

            _panel.Show(
                role,
                definitions,
                id => _runtimeState.GetRank(role, id),
                _wallet.GetBalance(role),
                state.RefreshCount == 0 ? 0 : PaidRefreshCost,
                index => RequestSelection(role, index),
                () => RequestRefreshFor(role));
        }

        private void RequestRefreshFor(PlayerRole role)
        {
            if (!IsOnline || _session.IsAuthority)
            {
                ApplyRefresh(role);
                return;
            }

            _panel.ShowStatus("正在等待主机确认刷新……");
            GameAppRoot.Instance?.Audio?.Play(AudioCue.UiConfirm);
            _session.SendToAuthority(
                NetworkRequest,
                writer =>
                {
                    writer.Write(RequestRefresh);
                    writer.Write((byte)role);
                },
                reliable: true);
        }

        private void RequestSelection(PlayerRole role, int offerIndex)
        {
            OfferState state = GetOffer(role);
            if (offerIndex < 0 || offerIndex >= OfferCount)
            {
                return;
            }

            UpgradeCardId id = state.Offers[offerIndex];
            if (!IsOnline || _session.IsAuthority)
            {
                ApplySelection(role, id);
                return;
            }

            _panel.ShowStatus("正在等待主机确认选择……");
            GameAppRoot.Instance?.Audio?.Play(AudioCue.UiConfirm);
            _session.SendToAuthority(
                NetworkRequest,
                writer =>
                {
                    writer.Write(RequestSelect);
                    writer.Write((byte)role);
                    writer.Write((byte)id);
                },
                reliable: true);
        }

        private void ApplyRefresh(PlayerRole role)
        {
            OfferState state = GetOffer(role);
            if (!_nodeActive)
            {
                PlayLocalResult(role, AudioCue.UiReject);
                return;
            }

            int cost = state.RefreshCount == 0 ? 0 : PaidRefreshCost;
            if (!_wallet.TrySpend(role, cost))
            {
                PlayLocalResult(role, AudioCue.UiReject);
                if (IsPanelShowing(role))
                {
                    _panel.ShowStatus($"TOKEN 不足：再次刷新需要 {cost}");
                }
                BroadcastSnapshot();
                return;
            }

            state.RefreshCount++;
            PlayLocalResult(role, AudioCue.Refresh);
            GenerateOffers(role, state);
            BroadcastSnapshot();
            if (IsPanelShowing(role))
            {
                RefreshPanel(role);
            }
        }

        private void ApplySelection(PlayerRole role, UpgradeCardId id)
        {
            OfferState state = GetOffer(role);
            if (!_nodeActive || !Contains(state, id) ||
                !_runtimeState.Catalog.TryGet(id, out UpgradeDefinition definition) ||
                !IsOfferUnlocked(role, id))
            {
                PlayLocalResult(role, AudioCue.UiReject);
                return;
            }

            int cost = definition.GetTokenCost(
                _runtimeState.GetRank(role, id));
            if (_wallet.GetBalance(role) < cost)
            {
                PlayLocalResult(role, AudioCue.UiReject);
                if (IsPanelShowing(role))
                {
                    _panel.ShowStatus($"TOKEN 不足：购买需要 {cost}");
                }
                BroadcastSnapshot();
                return;
            }
            if (!_runtimeState.TryApply(role, id))
            {
                PlayLocalResult(role, AudioCue.UiReject);
                BroadcastSnapshot();
                return;
            }
            _wallet.TrySpend(role, cost);

            state.PurchaseCount++;
            PlayLocalResult(role, AudioCue.Upgrade);
            GenerateOffers(role, state);
            BroadcastSnapshot();
            if (IsPanelShowing(role))
            {
                RefreshPanel(role);
            }
        }

        private bool IsPanelShowing(PlayerRole role)
        {
            return _panel.IsOpen && _panel.Role == role;
        }

        private void PlayLocalResult(PlayerRole role, AudioCue cue)
        {
            // 本机结果直接播；对方结果只发送明确回执，整份快照与检查点恢复不代表操作成功。
            if (!IsOnline || role == _controlAssignment.CurrentLocalPlayerRole)
            {
                GameAppRoot.Instance?.Audio?.Play(cue);
                return;
            }
            if (!_session.IsAuthority || (byte)role > 1) return;
            byte result = cue switch
            {
                AudioCue.UiReject => 0, AudioCue.Refresh => 1, AudioCue.Upgrade => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(cue))
            };
            _resultSequence = unchecked(_resultSequence + 1);
            if (_resultSequence == 0) _resultSequence = 1;
            _session.SendAuthority(NetworkResult, writer =>
            {
                writer.Write(_nodeSerial); writer.Write(_resultSequence);
                writer.Write((byte)role); writer.Write(result);
            }, reliable: true);
        }

        private bool TryReadResult(BinaryReader reader, out AudioCue cue)
        {
            cue = default;
            if (!_isInitialized || !IsOnline || _session.IsAuthority || !_nodeActive || _controlAssignment == null ||
                !NetworkMessageCatalog.TryValidatePayload(NetworkResult,
                    NetworkMessageCatalog.Direction.AuthorityToPeer, reader, out _)) return false;
            int nodeSerial = reader.ReadInt32();
            uint sequence = reader.ReadUInt32();
            PlayerRole role = (PlayerRole)reader.ReadByte();
            byte result = reader.ReadByte();
            if (nodeSerial != _nodeSerial || role != _controlAssignment.CurrentLocalPlayerRole ||
                (_receivedResult && !RemoteCommandSource.IsNewer(sequence, _lastResultSequence))) return false;
            _receivedResult = true;
            _lastResultSequence = sequence;
            cue = result == 0 ? AudioCue.UiReject : result == 1 ? AudioCue.Refresh : AudioCue.Upgrade;
            return true;
        }

        private bool IsOfferUnlocked(PlayerRole role, UpgradeCardId id)
        {
            return id != UpgradeCardId.RiceGuidance ||
                _runtimeState.GetRank(role, UpgradeCardId.RiceFan) >= 1;
        }

        private void GenerateOffers(PlayerRole role, OfferState state)
        {
            _runtimeState.Catalog.GetEligible(role, _candidates);
            for (int index = _candidates.Count - 1; index >= 0; index--)
            {
                if (_runtimeState.IsMaximumRank(role, _candidates[index].Id) ||
                    !IsOfferUnlocked(role, _candidates[index].Id))
                {
                    _candidates.RemoveAt(index);
                }
            }

            int seed = _chapterSeed ^ (_nodeSerial * 397) ^
                ((int)role * 7919) ^ (state.RefreshCount * 104729);
            seed ^= state.PurchaseCount * 130363;
            var random = new System.Random(seed);
            for (int index = _candidates.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                (_candidates[index], _candidates[swap]) =
                    (_candidates[swap], _candidates[index]);
            }

            for (int index = 0; index < OfferCount; index++)
            {
                state.Offers[index] = index < _candidates.Count
                    ? _candidates[index].Id
                    : default;
            }
        }

        private void BroadcastSnapshot()
        {
            if (!IsOnline || !_session.IsAuthority)
            {
                return;
            }

            _session.SendAuthority(
                NetworkSnapshot,
                WriteSnapshot,
                reliable: true);
        }

        private void WriteSnapshot(BinaryWriter writer)
        {
            writer.Write(_nodeSerial);
            WriteOffer(writer, _deepSeek);
            WriteOffer(writer, _harness);
            _wallet.WriteNetworkState(writer);
            IReadOnlyList<UpgradeDefinition> definitions =
                _runtimeState.Catalog.Definitions;
            writer.Write((byte)definitions.Count);
            for (int index = 0; index < definitions.Count; index++)
            {
                UpgradeCardId id = definitions[index].Id;
                writer.Write((byte)id);
                writer.Write((byte)_runtimeState.GetRank(PlayerRole.DeepSeek, id));
                writer.Write((byte)_runtimeState.GetRank(PlayerRole.Harness, id));
            }
        }

        private void ReadAuthorityMessage(byte kind, BinaryReader reader)
        {
            if (kind == NetworkResult)
            {
                if (TryReadResult(reader, out AudioCue cue)) GameAppRoot.Instance?.Audio?.Play(cue);
                return;
            }
            if (!_isInitialized || kind != NetworkSnapshot ||
                !IsOnline || _session.IsAuthority)
            {
                return;
            }

            _nodeSerial = reader.ReadInt32();
            ReadOffer(reader, _deepSeek);
            ReadOffer(reader, _harness);
            _wallet.ReadNetworkState(reader);
            int definitionCount = reader.ReadByte();
            for (int index = 0; index < definitionCount; index++)
            {
                UpgradeCardId id = (UpgradeCardId)reader.ReadByte();
                _runtimeState.SetRankFromAuthority(
                    PlayerRole.DeepSeek, id, reader.ReadByte());
                _runtimeState.SetRankFromAuthority(
                    PlayerRole.Harness, id, reader.ReadByte());
            }
            _runtimeState.NotifySnapshotApplied();

            if (_panel.IsOpen)
            {
                RefreshPanel(_panel.Role);
            }
        }

        private void ReadPeerMessage(byte kind, BinaryReader reader)
        {
            if (!_isInitialized || kind != NetworkRequest ||
                !IsOnline || !_session.IsAuthority || !_nodeActive)
            {
                return;
            }

            byte request = reader.ReadByte();
            PlayerRole role = (PlayerRole)reader.ReadByte();
            PlayerRole guestRole = _session.HostRole == PlayerRole.DeepSeek
                ? PlayerRole.Harness
                : PlayerRole.DeepSeek;
            if (role != guestRole)
            {
                return;
            }

            if (request == RequestRefresh)
            {
                ApplyRefresh(role);
            }
            else if (request == RequestSelect)
            {
                ApplySelection(role, (UpgradeCardId)reader.ReadByte());
            }
        }

        private void OnPeerJoined()
        {
            if (_nodeActive)
            {
                BroadcastSnapshot();
            }
        }

        private OfferState GetOffer(PlayerRole role)
        {
            return role == PlayerRole.DeepSeek ? _deepSeek : _harness;
        }

        private static bool Contains(OfferState state, UpgradeCardId id)
        {
            for (int index = 0; index < OfferCount; index++)
            {
                if (state.Offers[index] == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ResetOffer(OfferState state)
        {
            state.RefreshCount = 0;
            state.PurchaseCount = 0;
            Array.Clear(state.Offers, 0, state.Offers.Length);
        }

        private static OfferCheckpoint CaptureOffer(OfferState state)
        {
            return new OfferCheckpoint(
                (UpgradeCardId[])state.Offers.Clone(),
                state.RefreshCount,
                state.PurchaseCount);
        }

        private static void RestoreOffer(
            OfferCheckpoint checkpoint,
            OfferState state)
        {
            Array.Copy(
                checkpoint.Offers,
                state.Offers,
                state.Offers.Length);
            state.RefreshCount = checkpoint.RefreshCount;
            state.PurchaseCount = checkpoint.PurchaseCount;
        }

        private static void WriteOffer(BinaryWriter writer, OfferState state)
        {
            writer.Write(state.RefreshCount);
            writer.Write(state.PurchaseCount);
            for (int index = 0; index < OfferCount; index++)
            {
                writer.Write((byte)state.Offers[index]);
            }
        }

        private static void ReadOffer(BinaryReader reader, OfferState state)
        {
            state.RefreshCount = reader.ReadInt32();
            state.PurchaseCount = reader.ReadInt32();
            for (int index = 0; index < OfferCount; index++)
            {
                state.Offers[index] = (UpgradeCardId)reader.ReadByte();
            }
        }

        private bool TryValidateConfiguration(out string reason)
        {
            if (_runtimeState == null || _runtimeState.Catalog == null)
            {
                reason = "未配置强化运行时状态。";
                return false;
            }
            if (_panel == null || _controlAssignment == null ||
                _wallet == null)
            {
                reason = "未配置强化面板、本地玩家分配或 Token 钱包。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private sealed class OfferState
        {
            public readonly UpgradeCardId[] Offers =
                new UpgradeCardId[OfferCount];
            public int RefreshCount;
            public int PurchaseCount;
        }

        private sealed class OfferCheckpoint
        {
            public OfferCheckpoint(
                UpgradeCardId[] offers,
                int refreshCount,
                int purchaseCount)
            {
                Offers = offers;
                RefreshCount = refreshCount;
                PurchaseCount = purchaseCount;
            }

            public UpgradeCardId[] Offers { get; }
            public int RefreshCount { get; }
            public int PurchaseCount { get; }
        }

        private sealed class ProgressCheckpoint
        {
            public ProgressCheckpoint(
                int nodeSerial,
                OfferCheckpoint deepSeek,
                OfferCheckpoint harness,
                TokenWalletSnapshot wallet,
                PlayerUpgradeSnapshot upgrades)
            {
                NodeSerial = nodeSerial;
                DeepSeek = deepSeek;
                Harness = harness;
                Wallet = wallet;
                Upgrades = upgrades;
            }

            public int NodeSerial { get; }
            public OfferCheckpoint DeepSeek { get; }
            public OfferCheckpoint Harness { get; }
            public TokenWalletSnapshot Wallet { get; }
            public PlayerUpgradeSnapshot Upgrades { get; }
        }
    }
}
