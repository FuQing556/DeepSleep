using System;
using System.IO;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Runtime.Progression.Run
{
    public enum ChapterRunPhase : byte
    {
        WaitingForSelection,
        Combat,
        Node,
        Defeat,
        Complete
    }

    public enum ChapterFailureReason : byte
    {
        None,
        DeepSeekLost,
        HarnessLost,
        TeamDowned,
        ObjectiveIncomplete
    }

    /// <summary>
    /// 章节宏观流程的唯一权威：倒计时、任务、失败和节点检查点。
    /// 敌人、节点、生命与强化仍由各自系统保存具体状态。
    /// </summary>
    public sealed class ChapterRunController : MonoBehaviour
    {
        private const byte NetworkRunState = 45;
        private const float NetworkBroadcastInterval = 0.2f;

        [SerializeField] private ChapterRunConfig _config;
        [SerializeField] private RestNodePrototypeController2D _restNode;
        [SerializeField] private RestNodeUpgradeController _upgradeController;
        [SerializeField] private OpeningCharacterSelectionController _selection;
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private PlayerLifeStateController2D _deepSeekLife;
        [SerializeField] private PlayerLifeStateController2D _harnessLife;
        [SerializeField] private EnemyActorPool2D[] _enemyPools;
        [SerializeField] private ChapterRunHudView _hud;

        private float _remainingCombatSeconds;
        private float _deepSeekDownedSeconds;
        private float _harnessDownedSeconds;
        private float _teamDownedSeconds;
        private float _defeatElapsed;
        private float _networkElapsed;
        private int _defeats;
        private int _totalDefeats;
        private float _totalCombatSeconds;
        private int _segmentNumber = 1;
        private bool _hasRestNodeCheckpoint;
        private bool _isInitialized;
        private PlayerLifeCheckpoint _deepSeekCheckpoint;
        private PlayerLifeCheckpoint _harnessCheckpoint;

        public ChapterRunPhase Phase { get; private set; } =
            ChapterRunPhase.WaitingForSelection;
        public ChapterFailureReason FailureReason { get; private set; }
        public int SegmentNumber => _segmentNumber;
        public int Defeats => _defeats;
        public float RemainingCombatSeconds => _remainingCombatSeconds;

        public void CompleteObjectiveAndExpireForDevelopment()
        {
            if (!Debug.isDebugBuild || Phase != ChapterRunPhase.Combat)
            {
                return;
            }
            _totalDefeats += Mathf.Max(0, _config.RequiredDefeats - _defeats);
            _defeats = _config.RequiredDefeats;
            _remainingCombatSeconds = 0f;
        }

        public void CompletePrototypeForDevelopment()
        {
            if (!Debug.isDebugBuild || Phase != ChapterRunPhase.Combat)
            {
                return;
            }

            _segmentNumber = _config.CombatSegmentCount;
            CompleteObjectiveAndExpireForDevelopment();
        }

        public void FailObjectiveForDevelopment()
        {
            if (!Debug.isDebugBuild || Phase != ChapterRunPhase.Combat)
            {
                return;
            }
            _defeats = Mathf.Min(_defeats, _config.RequiredDefeats - 1);
            _remainingCombatSeconds = 0f;
        }

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;
        private bool CanAuthor => !IsOnline || _session.IsAuthority;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError($"[{nameof(ChapterRunController)}] {reason}", this);
                enabled = false;
                return;
            }

            _isInitialized = true;
            _remainingCombatSeconds = _config.CombatDurationSeconds;
            Render();
        }

        private void OnEnable()
        {
            if (_selection != null)
            {
                _selection.SelectionConfirmed += OnSelectionConfirmed;
            }
            if (_restNode != null)
            {
                _restNode.StateChanged += OnRestNodeStateChanged;
            }
            if (_session != null)
            {
                _session.AuthorityMessage += ReadAuthorityState;
                _session.PeerJoined += BroadcastState;
            }
            if (_hud != null)
            {
                _hud.ReturnRequested += ReturnToOpening;
            }
            SubscribeEnemyPools(true);
        }

        private void OnDisable()
        {
            if (_selection != null)
            {
                _selection.SelectionConfirmed -= OnSelectionConfirmed;
            }
            if (_restNode != null)
            {
                _restNode.StateChanged -= OnRestNodeStateChanged;
            }
            if (_session != null)
            {
                _session.AuthorityMessage -= ReadAuthorityState;
                _session.PeerJoined -= BroadcastState;
            }
            if (_hud != null)
            {
                _hud.ReturnRequested -= ReturnToOpening;
            }
            SubscribeEnemyPools(false);
        }

        private void Start()
        {
            _upgradeController.CaptureProgressCheckpoint();
            CapturePlayerCheckpoint();
            if (_selection.IsSelectionComplete)
            {
                StartCombatSegment();
            }
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (CanAuthor)
            {
                if (Phase == ChapterRunPhase.Combat)
                {
                    SimulateCombat(Time.deltaTime);
                }
                else if (Phase == ChapterRunPhase.Defeat)
                {
                    SimulateDefeat(Time.deltaTime);
                }

                if (IsOnline)
                {
                    _networkElapsed += Time.unscaledDeltaTime;
                    if (_networkElapsed >= NetworkBroadcastInterval)
                    {
                        _networkElapsed = 0f;
                        BroadcastState();
                    }
                }
            }

            Render();
        }

        private void SimulateCombat(float deltaTime)
        {
            if (deltaTime <= 0f || _restNode.State != RestNodeState.Combat)
            {
                return;
            }

            UpdateDownedTimers(deltaTime);
            if (TryResolveLifeFailure())
            {
                return;
            }

            _remainingCombatSeconds = Mathf.Max(
                0f,
                _remainingCombatSeconds - deltaTime);
            _totalCombatSeconds += deltaTime;
            if (_remainingCombatSeconds > 0f)
            {
                return;
            }

            if (_defeats < _config.RequiredDefeats)
            {
                BeginDefeat(ChapterFailureReason.ObjectiveIncomplete);
            }
            else if (_segmentNumber >= _config.CombatSegmentCount)
            {
                CompleteChapter();
            }
            else if (_restNode.BeginNodeTransition())
            {
                Phase = ChapterRunPhase.Node;
                BroadcastState();
            }
        }

        private void UpdateDownedTimers(float deltaTime)
        {
            bool deepSeekDowned =
                _deepSeekLife.State == PlayerLifeState.Downed;
            bool harnessDowned =
                _harnessLife.State == PlayerLifeState.Downed;
            _deepSeekDownedSeconds = deepSeekDowned
                ? _deepSeekDownedSeconds + deltaTime
                : 0f;
            _harnessDownedSeconds = harnessDowned
                ? _harnessDownedSeconds + deltaTime
                : 0f;
            _teamDownedSeconds = deepSeekDowned && harnessDowned
                ? _teamDownedSeconds + deltaTime
                : 0f;
        }

        private bool TryResolveLifeFailure()
        {
            if (_teamDownedSeconds >= _config.TeamDownedTimeoutSeconds)
            {
                BeginDefeat(ChapterFailureReason.TeamDowned);
                return true;
            }
            if (_deepSeekDownedSeconds >= _config.SingleDownedTimeoutSeconds)
            {
                BeginDefeat(ChapterFailureReason.DeepSeekLost);
                return true;
            }
            if (_harnessDownedSeconds >= _config.SingleDownedTimeoutSeconds)
            {
                BeginDefeat(ChapterFailureReason.HarnessLost);
                return true;
            }
            return false;
        }

        private void BeginDefeat(ChapterFailureReason reason)
        {
            if (Phase != ChapterRunPhase.Combat)
            {
                return;
            }

            FailureReason = reason;
            Phase = ChapterRunPhase.Defeat;
            _defeatElapsed = 0f;
            _restNode.SuspendCombatForFailure();
            BroadcastState();
        }

        private void SimulateDefeat(float deltaTime)
        {
            _defeatElapsed += Mathf.Max(0f, deltaTime);
            if (_defeatElapsed < _config.DefeatPresentationSeconds)
            {
                return;
            }

            RestorePlayersFromCheckpoint();
            if (_hasRestNodeCheckpoint)
            {
                if (_restNode.RestoreCheckpointNode())
                {
                    Phase = ChapterRunPhase.Node;
                }
            }
            else if (_restNode.RestartCombatFromCheckpoint())
            {
                StartCombatSegment();
            }
            BroadcastState();
        }

        private void OnSelectionConfirmed(PlayerRole role)
        {
            if (Phase == ChapterRunPhase.WaitingForSelection)
            {
                StartCombatSegment();
            }
        }

        private void OnRestNodeStateChanged(RestNodeState state)
        {
            if (!CanAuthor)
            {
                return;
            }

            if (state == RestNodeState.Open)
            {
                ApplyRestNodeRecovery();
                Phase = ChapterRunPhase.Node;
            }
            else if (state == RestNodeState.Departing)
            {
                CapturePlayerCheckpoint();
                _hasRestNodeCheckpoint = true;
            }
            else if (state == RestNodeState.Combat &&
                     Phase == ChapterRunPhase.Node)
            {
                _segmentNumber++;
                StartCombatSegment();
            }
            BroadcastState();
        }

        private void ApplyRestNodeRecovery()
        {
            RestNodeRecoveryMode mode = _config.RestNodeRecovery;
            if (mode == RestNodeRecoveryMode.Disabled)
            {
                return;
            }

            bool full = mode == RestNodeRecoveryMode.FullRestore;
            _deepSeekLife.RestoreAtCheckpoint(
                _deepSeekLife.transform.position,
                full,
                _config.ReviveOnlyHealthFraction,
                _config.CheckpointInvulnerabilitySeconds);
            _harnessLife.RestoreAtCheckpoint(
                _harnessLife.transform.position,
                full,
                _config.ReviveOnlyHealthFraction,
                _config.CheckpointInvulnerabilitySeconds);
        }

        private void StartCombatSegment()
        {
            Phase = ChapterRunPhase.Combat;
            FailureReason = ChapterFailureReason.None;
            _remainingCombatSeconds = _config.CombatDurationSeconds;
            _defeats = 0;
            _deepSeekDownedSeconds = 0f;
            _harnessDownedSeconds = 0f;
            _teamDownedSeconds = 0f;
            _defeatElapsed = 0f;
            BroadcastState();
        }

        private void CompleteChapter()
        {
            Phase = ChapterRunPhase.Complete;
            FailureReason = ChapterFailureReason.None;
            _upgradeController.SettleFinalBattle();
            _restNode.SuspendCombatForSettlement();
            Time.timeScale = 0f;
            BroadcastState();
        }

        private void ReturnToOpening()
        {
            if (_session != null && _session.Phase != SessionPhase.Offline)
            {
                _session.Leave();
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void CapturePlayerCheckpoint()
        {
            _deepSeekCheckpoint = _deepSeekLife.CaptureCheckpoint();
            _harnessCheckpoint = _harnessLife.CaptureCheckpoint();
        }

        private void RestorePlayersFromCheckpoint()
        {
            _deepSeekLife.RestoreCheckpoint(
                _deepSeekCheckpoint,
                _config.CheckpointInvulnerabilitySeconds);
            _harnessLife.RestoreCheckpoint(
                _harnessCheckpoint,
                _config.CheckpointInvulnerabilitySeconds);
        }

        private void OnEnemyDespawned(EnemyDespawnRequest2D request)
        {
            if (CanAuthor && Phase == ChapterRunPhase.Combat &&
                request.Reason == EnemyDespawnReason.Defeated)
            {
                _defeats++;
                _totalDefeats++;
            }
        }

        private void SubscribeEnemyPools(bool subscribe)
        {
            if (_enemyPools == null)
            {
                return;
            }
            for (int index = 0; index < _enemyPools.Length; index++)
            {
                if (_enemyPools[index] == null) continue;
                if (subscribe)
                    _enemyPools[index].ActorDespawned += OnEnemyDespawned;
                else
                    _enemyPools[index].ActorDespawned -= OnEnemyDespawned;
            }
        }

        private void Render()
        {
            bool visible = Phase == ChapterRunPhase.Combat ||
                Phase == ChapterRunPhase.Defeat;
            _hud.Render(BuildHudText(), visible,
                Phase == ChapterRunPhase.Defeat);
            _hud.RenderSettlement(
                BuildSettlementText(),
                Phase == ChapterRunPhase.Complete);
        }

        private string BuildSettlementText()
        {
            if (Phase != ChapterRunPhase.Complete)
            {
                return string.Empty;
            }

            int totalSeconds = Mathf.CeilToInt(_totalCombatSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return "原型演练完成\n\n" +
                $"战斗段  {_config.CombatSegmentCount}/{_config.CombatSegmentCount}\n" +
                $"累计击败  {_totalDefeats}\n" +
                $"战斗用时  {minutes:00}:{seconds:00}";
        }

        private string BuildHudText()
        {
            if (Phase == ChapterRunPhase.Defeat)
            {
                return FailureText(FailureReason) + "\n正在返回检查点……";
            }
            if (Phase != ChapterRunPhase.Combat)
            {
                return string.Empty;
            }

            string text = $"第 {_segmentNumber} 段  " +
                $"剩余 {Mathf.CeilToInt(_remainingCombatSeconds)} 秒  " +
                $"击败 {_defeats}/{_config.RequiredDefeats}";
            if (_teamDownedSeconds > 0f)
                text += $"\n全队宕机：{Remaining(_config.TeamDownedTimeoutSeconds, _teamDownedSeconds):0.0}s";
            else if (_deepSeekDownedSeconds > 0f)
                text += $"\nDS 数据丢失倒计时：{Remaining(_config.SingleDownedTimeoutSeconds, _deepSeekDownedSeconds):0.0}s";
            else if (_harnessDownedSeconds > 0f)
                text += $"\nHS 数据丢失倒计时：{Remaining(_config.SingleDownedTimeoutSeconds, _harnessDownedSeconds):0.0}s";
            return text;
        }

        private static float Remaining(float limit, float elapsed) =>
            Mathf.Max(0f, limit - elapsed);

        private static string FailureText(ChapterFailureReason reason)
        {
            return reason switch
            {
                ChapterFailureReason.DeepSeekLost => "任务失败 · DS 数据丢失",
                ChapterFailureReason.HarnessLost => "任务失败 · HS 数据丢失",
                ChapterFailureReason.TeamDowned => "任务失败 · 全队宕机",
                ChapterFailureReason.ObjectiveIncomplete => "任务失败 · 击杀目标未完成",
                _ => "任务失败"
            };
        }

        private void BroadcastState()
        {
            if (!IsOnline || !_session.IsAuthority)
            {
                return;
            }
            _session.SendAuthority(
                NetworkRunState,
                writer =>
                {
                    writer.Write((byte)Phase);
                    writer.Write((byte)FailureReason);
                    writer.Write(_segmentNumber);
                    writer.Write(_remainingCombatSeconds);
                    writer.Write(_defeats);
                    writer.Write(_deepSeekDownedSeconds);
                    writer.Write(_harnessDownedSeconds);
                    writer.Write(_teamDownedSeconds);
                    writer.Write(_totalDefeats);
                    writer.Write(_totalCombatSeconds);
                },
                reliable: true);
        }

        private void ReadAuthorityState(byte kind, BinaryReader reader)
        {
            if (kind != NetworkRunState || !IsOnline || _session.IsAuthority)
            {
                return;
            }
            ChapterRunPhase previousPhase = Phase;
            Phase = (ChapterRunPhase)reader.ReadByte();
            FailureReason = (ChapterFailureReason)reader.ReadByte();
            _segmentNumber = reader.ReadInt32();
            _remainingCombatSeconds = reader.ReadSingle();
            _defeats = reader.ReadInt32();
            _deepSeekDownedSeconds = reader.ReadSingle();
            _harnessDownedSeconds = reader.ReadSingle();
            _teamDownedSeconds = reader.ReadSingle();
            _totalDefeats = reader.ReadInt32();
            _totalCombatSeconds = reader.ReadSingle();
            if (previousPhase != ChapterRunPhase.Complete &&
                Phase == ChapterRunPhase.Complete)
            {
                _restNode.SuspendCombatForSettlement();
                Time.timeScale = 0f;
            }
            Render();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_config == null)
            {
                reason = "未配置章节运行参数。";
                return false;
            }
            if (!_config.TryValidate(out reason))
            {
                reason = "章节运行配置无效：" + reason;
                return false;
            }
            if (_restNode == null || _upgradeController == null ||
                _selection == null || _deepSeekLife == null ||
                _harnessLife == null || _hud == null ||
                _enemyPools == null || _enemyPools.Length == 0)
            {
                reason = "节点、强化、选角、两名玩家、敌人池和 HUD 必须完整配置。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
