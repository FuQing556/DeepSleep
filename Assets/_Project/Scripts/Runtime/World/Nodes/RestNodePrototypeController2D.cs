using System;
using System.Collections.Generic;
using System.IO;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.World.Scrolling;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.World.Nodes
{
    public enum RestNodeState : byte
    {
        Combat,
        Clearing,
        Revealing,
        Open,
        Departing
    }

    public enum RestNodeHotspotKind : byte
    {
        DeepSeekUpgrade,
        ExitPortal,
        HarnessUpgrade,
        MemoryFragment
    }

    /// <summary>
    /// 第一版休息节点闭环：停止刷怪、等待敌人自然清空、
    /// 淡出循环云层，并启用神殿图上的透明交互区域。
    /// </summary>
    public sealed class RestNodePrototypeController2D : MonoBehaviour
    {
        private const byte NETWORK_STATE = 40;
        private const byte NETWORK_PORTAL_READY_REQUEST = 41;

        [Header("战斗与网络")]
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private EnemySpawnDirector2D[] _spawnDirectors;
        [SerializeField] private EnemyActorPool2D[] _enemyPools;
        [SerializeField] private EnemyProjectilePool2D[] _enemyProjectilePools;

        [Header("背景揭示")]
        [SerializeField] private LoopingBackgroundLayer2D _scrollingCloudLayer;
        [SerializeField] private Transform _cloudLayerRoot;
        [SerializeField] private SpriteRenderer _templeRenderer;
        [SerializeField, Min(0.01f)] private float _cloudFadeSeconds = 1.25f;

        [Header("本地文字交互")]
        [SerializeField] private PlayerControlAssignment _controlAssignment;
        [SerializeField] private Text _promptText;
        [SerializeField] private Button _actionButton;
        [SerializeField] private Text _actionButtonLabel;
        [SerializeField] private RestNodeHotspot2D[] _hotspots;
        [SerializeField] private RestNodeUpgradeController _upgradeController;

        private readonly List<SpriteRenderer> _cloudRenderers = new();
        private readonly List<Color> _cloudBaseColors = new();
        private readonly Dictionary<RestNodeHotspot2D,
            Dictionary<PlayerActor, int>> _overlaps = new();
        private float _revealElapsed;
        private float _feedbackUntil;
        private Color _templeBaseColor;
        private bool _isInitialized;
        private bool _deepSeekPortalReady;
        private bool _harnessPortalReady;
        private RestNodeHotspot2D _nearestLocalHotspot;

        public RestNodeState State { get; private set; }
        public event Action<RestNodeState> StateChanged;

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;

        private bool CanDriveState => !IsOnline || _session.IsAuthority;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(RestNodePrototypeController2D)}] " +
                    $"休息节点装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            SetHotspotsActive(false);
            SetPrompt(string.Empty);
            SetActionButtonActive(false);
            _templeBaseColor = _templeRenderer.color;
            SetTempleAlpha(0f);
            State = RestNodeState.Combat;
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_session != null)
            {
                _session.AuthorityMessage += ReadNetworkState;
                _session.PeerMessage += ReadPeerRequest;
                _session.SessionOpened += OnSessionOpened;
                _session.PeerJoined += OnPeerJoined;
            }

            if (_actionButton != null)
            {
                _actionButton.onClick.AddListener(ActivateNearestHotspot);
            }
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.AuthorityMessage -= ReadNetworkState;
                _session.PeerMessage -= ReadPeerRequest;
                _session.SessionOpened -= OnSessionOpened;
                _session.PeerJoined -= OnPeerJoined;
            }

            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveListener(ActivateNearestHotspot);
            }
        }

        private void Update()
        {
            switch (State)
            {
                case RestNodeState.Clearing when CanDriveState:
                    if (AreEnemiesCleared())
                    {
                        ReturnEnemyProjectiles();
                        ApplyState(RestNodeState.Revealing);
                        BroadcastState(RestNodeState.Revealing);
                    }
                    break;

                case RestNodeState.Revealing:
                    _revealElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(
                        _revealElapsed / _cloudFadeSeconds);
                    SetCloudAlpha(1f - progress);
                    if (progress >= 1f)
                    {
                        ApplyState(RestNodeState.Open);
                        if (CanDriveState)
                        {
                            BroadcastState(RestNodeState.Open);
                        }
                    }
                    break;

                case RestNodeState.Open:
                    RefreshAutomatedPortalReady();
                    RefreshNearestPrompt();
                    TryCompletePortalDeparture();
                    break;

                case RestNodeState.Departing:
                    _revealElapsed += Time.deltaTime;
                    float departureProgress = Mathf.Clamp01(
                        _revealElapsed / _cloudFadeSeconds);
                    SetTempleAlpha(1f - departureProgress);
                    SetCloudAlpha(departureProgress);
                    if (departureProgress >= 1f && CanDriveState)
                    {
                        ApplyState(RestNodeState.Combat);
                        BroadcastState(RestNodeState.Combat);
                    }
                    break;
            }
        }

        public bool BeginNodeTransition()
        {
            if (!_isInitialized ||
                State != RestNodeState.Combat ||
                !CanDriveState)
            {
                return false;
            }

            ApplyState(RestNodeState.Clearing);
            BroadcastState(RestNodeState.Clearing);
            return true;
        }

        public bool ReturnToCombat()
        {
            if (!_isInitialized ||
                State != RestNodeState.Open ||
                !CanDriveState)
            {
                return false;
            }

            ApplyState(RestNodeState.Departing);
            BroadcastState(RestNodeState.Departing);
            return true;
        }

        public void SuspendCombatForFailure()
        {
            if (!_isInitialized || !CanDriveState)
            {
                return;
            }

            SuspendCombatWorld();
        }

        public void SuspendCombatForSettlement()
        {
            if (!_isInitialized)
            {
                return;
            }

            SuspendCombatWorld();
        }

        private void SuspendCombatWorld()
        {

            _scrollingCloudLayer.SetScrollMultiplier(0f);
            for (int index = 0; index < _spawnDirectors.Length; index++)
            {
                _spawnDirectors[index]?.Stop();
            }
            for (int index = 0; index < _enemyPools.Length; index++)
            {
                _enemyPools[index]?.DespawnAll(EnemyDespawnReason.RunReset);
            }
            ReturnEnemyProjectiles();
            SetHotspotsActive(false);
        }

        public bool RestoreCheckpointNode()
        {
            if (!_isInitialized || !CanDriveState ||
                !_upgradeController.RestoreProgressCheckpoint(true))
            {
                return false;
            }

            ResetNodeInteraction();
            CaptureCloudRenderers();
            SetCloudAlpha(0f);
            SetTempleAlpha(1f);
            _scrollingCloudLayer.SetScrollMultiplier(0f);
            for (int index = 0; index < _spawnDirectors.Length; index++)
            {
                _spawnDirectors[index]?.Stop();
            }
            SetHotspotsActive(true);
            State = RestNodeState.Open;
            SetPrompt("已返回上一休息节点 · 本次战斗收益已回滚");
            StateChanged?.Invoke(State);
            BroadcastState(State);
            return true;
        }

        public bool RestartCombatFromCheckpoint()
        {
            if (!_isInitialized || !CanDriveState ||
                !_upgradeController.RestoreProgressCheckpoint(false))
            {
                return false;
            }

            ApplyState(RestNodeState.Combat);
            BroadcastState(RestNodeState.Combat);
            return true;
        }

        internal void NotifyEntered(
            RestNodeHotspot2D hotspot,
            PlayerActor actor)
        {
            if (State != RestNodeState.Open ||
                hotspot == null ||
                actor == null)
            {
                return;
            }

            if (!_overlaps.TryGetValue(
                    hotspot,
                    out Dictionary<PlayerActor, int> actors))
            {
                actors = new Dictionary<PlayerActor, int>();
                _overlaps.Add(hotspot, actors);
            }

            actors.TryGetValue(actor, out int count);
            actors[actor] = count + 1;
            RefreshNearestPrompt();
        }

        internal void NotifyExited(
            RestNodeHotspot2D hotspot,
            PlayerActor actor)
        {
            if (hotspot == null ||
                actor == null ||
                !_overlaps.TryGetValue(
                    hotspot,
                    out Dictionary<PlayerActor, int> actors) ||
                !actors.TryGetValue(actor, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                actors.Remove(actor);
                if (actors.Count == 0)
                {
                    _overlaps.Remove(hotspot);
                }
            }
            else
            {
                actors[actor] = count - 1;
            }

            if (hotspot.Kind == RestNodeHotspotKind.ExitPortal &&
                !IsActorInsideHotspot(hotspot, actor))
            {
                CancelPortalReadyForActor(actor);
            }

            RefreshNearestPrompt();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_scrollingCloudLayer == null || _cloudLayerRoot == null)
            {
                reason = "未配置循环云层及其根节点。";
                return false;
            }

            if (_templeRenderer == null || _templeRenderer.sprite == null)
            {
                reason = "神殿 SpriteRenderer 未配置有效图片。";
                return false;
            }

            if (_controlAssignment == null ||
                _promptText == null ||
                _actionButton == null ||
                _actionButtonLabel == null ||
                _upgradeController == null)
            {
                reason = "未配置本地玩家分配、交互 UI 或强化控制器。";
                return false;
            }

            if (_spawnDirectors == null || _spawnDirectors.Length == 0 ||
                _enemyPools == null || _enemyPools.Length == 0)
            {
                reason = "至少需要一个刷怪器和一个敌人池。";
                return false;
            }

            if (_hotspots == null || _hotspots.Length == 0)
            {
                reason = "没有配置神殿交互区域。";
                return false;
            }

            for (int index = 0; index < _hotspots.Length; index++)
            {
                if (_hotspots[index] == null)
                {
                    reason = $"第 {index} 个交互区域未配置。";
                    return false;
                }

                if (!_hotspots[index].TryValidateConfiguration(
                        out string hotspotReason))
                {
                    reason = $"第 {index} 个交互区域无效：" + hotspotReason;
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private void ApplyState(RestNodeState state)
        {
            State = state;
            switch (state)
            {
                case RestNodeState.Combat:
                    _upgradeController.EndNode();
                    ResetNodeInteraction();
                    SetHotspotsActive(false);
                    CaptureCloudRenderers();
                    SetCloudAlpha(1f);
                    SetTempleAlpha(0f);
                    _scrollingCloudLayer.SetScrollMultiplier(1f);
                    for (int index = 0; index < _spawnDirectors.Length; index++)
                    {
                        _spawnDirectors[index]?.Begin();
                    }
                    SetPrompt(string.Empty);
                    break;

                case RestNodeState.Clearing:
                    ResetNodeInteraction();
                    CaptureCloudRenderers();
                    SetTempleAlpha(1f);
                    _scrollingCloudLayer.SetScrollMultiplier(0f);
                    for (int index = 0; index < _spawnDirectors.Length; index++)
                    {
                        _spawnDirectors[index]?.Stop();
                    }
                    SetHotspotsActive(false);
                    SetPrompt("已抵达休息节点 · 清理残余威胁");
                    break;

                case RestNodeState.Revealing:
                    _revealElapsed = 0f;
                    SetPrompt("云层正在散开……");
                    break;

                case RestNodeState.Open:
                    SetCloudAlpha(0f);
                    SetTempleAlpha(1f);
                    SetHotspotsActive(true);
                    _upgradeController.BeginNode();
                    SetPrompt("休息节点 · 靠近设施进行调查");
                    break;

                case RestNodeState.Departing:
                    _revealElapsed = 0f;
                    _upgradeController.EndNode();
                    ResetNodeInteraction();
                    SetHotspotsActive(false);
                    CaptureCloudRenderers();
                    SetCloudAlpha(0f);
                    SetTempleAlpha(1f);
                    _scrollingCloudLayer.SetScrollMultiplier(0f);
                    SetPrompt("即将离开休息节点……");
                    break;
            }

            StateChanged?.Invoke(State);
        }

        private bool AreEnemiesCleared()
        {
            for (int index = 0; index < _enemyPools.Length; index++)
            {
                if (_enemyPools[index] != null &&
                    _enemyPools[index].ActiveCount > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ReturnEnemyProjectiles()
        {
            if (_enemyProjectilePools == null)
            {
                return;
            }

            for (int index = 0;
                 index < _enemyProjectilePools.Length;
                 index++)
            {
                _enemyProjectilePools[index]?.ReturnAllActive();
            }
        }

        private void CaptureCloudRenderers()
        {
            _cloudRenderers.Clear();
            _cloudBaseColors.Clear();
            _cloudLayerRoot.GetComponentsInChildren(
                true,
                _cloudRenderers);
            for (int index = 0; index < _cloudRenderers.Count; index++)
            {
                Color color = _cloudRenderers[index].color;
                color.a = 1f;
                _cloudBaseColors.Add(color);
            }
        }

        private void SetCloudAlpha(float alpha)
        {
            for (int index = 0; index < _cloudRenderers.Count; index++)
            {
                if (_cloudRenderers[index] == null)
                {
                    continue;
                }

                Color color = _cloudBaseColors[index];
                color.a *= Mathf.Clamp01(alpha);
                _cloudRenderers[index].color = color;
            }
        }

        private void SetTempleAlpha(float alpha)
        {
            Color color = _templeBaseColor;
            color.a *= Mathf.Clamp01(alpha);
            _templeRenderer.color = color;
        }

        private void SetHotspotsActive(bool active)
        {
            for (int index = 0; index < _hotspots.Length; index++)
            {
                if (_hotspots[index] != null)
                {
                    _hotspots[index].gameObject.SetActive(active);
                }
            }

            if (!active)
            {
                _overlaps.Clear();
                _nearestLocalHotspot = null;
                SetActionButtonActive(false);
            }
        }

        private void RefreshNearestPrompt()
        {
            PlayerActor localActor = _controlAssignment.CurrentLocalPlayerActor;
            RestNodeHotspot2D nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (KeyValuePair<RestNodeHotspot2D,
                         Dictionary<PlayerActor, int>> pair in _overlaps)
            {
                if (pair.Key == null ||
                    !pair.Value.TryGetValue(localActor, out int count) ||
                    count <= 0 ||
                    !CanLocalActorUse(pair.Key, localActor))
                {
                    continue;
                }

                float distance = ((Vector2)pair.Key.transform.position -
                    (Vector2)localActor.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = pair.Key;
                    nearestDistance = distance;
                }
            }

            _nearestLocalHotspot = nearest;
            SetActionButtonActive(nearest != null);
            if (nearest != null)
            {
                _actionButtonLabel.text = GetActionLabel(nearest);
            }

            if (Time.unscaledTime >= _feedbackUntil)
            {
                SetPrompt(nearest != null
                    ? nearest.Prompt
                    : "休息节点 · 靠近设施进行调查");
            }
        }

        private void SetActionButtonActive(bool active)
        {
            _actionButton.gameObject.SetActive(active);
        }

        private string GetActionLabel(RestNodeHotspot2D hotspot)
        {
            return hotspot.Kind switch
            {
                RestNodeHotspotKind.DeepSeekUpgrade => "查看 DS 强化",
                RestNodeHotspotKind.HarnessUpgrade => "查看 HS 强化",
                RestNodeHotspotKind.MemoryFragment => "读取记忆",
                RestNodeHotspotKind.ExitPortal =>
                    IsPortalReady(_controlAssignment.CurrentLocalPlayerRole)
                        ? "取消准备"
                        : "准备离开",
                _ => "交互"
            };
        }

        private void ActivateNearestHotspot()
        {
            PlayerActor actor = _controlAssignment.CurrentLocalPlayerActor;
            RestNodeHotspot2D hotspot = _nearestLocalHotspot;
            if (State != RestNodeState.Open ||
                actor == null ||
                hotspot == null ||
                !IsActorInsideHotspot(hotspot, actor))
            {
                return;
            }

            switch (hotspot.Kind)
            {
                case RestNodeHotspotKind.DeepSeekUpgrade:
                    if (!_upgradeController.OpenForRole(PlayerRole.DeepSeek))
                    {
                        ShowFeedback("该 DS 强化装置不属于本地玩家");
                    }
                    break;

                case RestNodeHotspotKind.HarnessUpgrade:
                    if (!_upgradeController.OpenForRole(PlayerRole.Harness))
                    {
                        ShowFeedback("该 HS 强化装置不属于本地玩家");
                    }
                    break;

                case RestNodeHotspotKind.MemoryFragment:
                    ShowFeedback(
                        "读取到一段未整理的数据记忆 · 内容将在关卡阶段接入");
                    break;

                case RestNodeHotspotKind.ExitPortal:
                    ToggleLocalPortalReady(actor);
                    break;
            }
        }

        private void ToggleLocalPortalReady(PlayerActor actor)
        {
            PlayerRole role = actor.Definition.Role;
            bool ready = !IsPortalReady(role);
            if (ready && !IsRoleInsidePortal(role))
            {
                return;
            }

            SetPortalReady(role, ready);
            if (IsOnline)
            {
                if (_session.IsAuthority)
                {
                    BroadcastState(State);
                }
                else
                {
                    _session.SendToAuthority(
                        NETWORK_PORTAL_READY_REQUEST,
                        writer => writer.Write(ready),
                        reliable: true);
                }
            }

            _actionButtonLabel.text = GetActionLabel(_nearestLocalHotspot);
            ShowFeedback(ready
                ? "已准备 · 与同伴同时留在传送门内即可离开"
                : "已取消准备");
            TryCompletePortalDeparture();
        }

        private void CancelPortalReadyForActor(PlayerActor actor)
        {
            PlayerRole role = actor.Definition.Role;
            if (!IsPortalReady(role))
            {
                return;
            }

            bool isLocal = actor == _controlAssignment.CurrentLocalPlayerActor;
            if (IsOnline && !_session.IsAuthority && !isLocal)
            {
                return;
            }

            SetPortalReady(role, false);
            if (IsOnline)
            {
                if (_session.IsAuthority)
                {
                    BroadcastState(State);
                }
                else if (isLocal)
                {
                    _session.SendToAuthority(
                        NETWORK_PORTAL_READY_REQUEST,
                        writer => writer.Write(false),
                        reliable: true);
                }
            }
        }

        private bool CanLocalActorUse(
            RestNodeHotspot2D hotspot,
            PlayerActor actor)
        {
            if (hotspot == null || actor == null)
            {
                return false;
            }

            if (hotspot.Allows(actor))
            {
                return true;
            }

            // 单机只有一名真人：空间站位仍由当前角色完成，
            // 但构筑权属于玩家本人，因此可操作 DS/HS 两台强化装置。
            return !IsOnline &&
                (hotspot.Kind == RestNodeHotspotKind.DeepSeekUpgrade ||
                 hotspot.Kind == RestNodeHotspotKind.HarnessUpgrade);
        }

        private void RefreshAutomatedPortalReady()
        {
            if (!IsOnline)
            {
                PlayerRole companionRole =
                    _controlAssignment.CurrentLocalPlayerRole == PlayerRole.DeepSeek
                        ? PlayerRole.Harness
                        : PlayerRole.DeepSeek;
                SetPortalReady(
                    companionRole,
                    IsRoleInsidePortal(companionRole));
                return;
            }

            if (!_session.IsAuthority)
            {
                return;
            }

            PlayerRole guestRole = _session.HostRole == PlayerRole.DeepSeek
                ? PlayerRole.Harness
                : PlayerRole.DeepSeek;
            if (!_session.IsRoleDisconnected(guestRole))
            {
                return;
            }

            bool ready = IsRoleInsidePortal(guestRole);
            if (IsPortalReady(guestRole) == ready)
            {
                return;
            }

            SetPortalReady(guestRole, ready);
            BroadcastState(State);
        }

        private void TryCompletePortalDeparture()
        {
            if (State != RestNodeState.Open ||
                !CanDriveState ||
                !_deepSeekPortalReady ||
                !_harnessPortalReady ||
                !IsRoleInsidePortal(PlayerRole.DeepSeek) ||
                !IsRoleInsidePortal(PlayerRole.Harness))
            {
                return;
            }

            ReturnToCombat();
        }

        private bool IsRoleInsidePortal(PlayerRole role)
        {
            foreach (KeyValuePair<RestNodeHotspot2D,
                         Dictionary<PlayerActor, int>> pair in _overlaps)
            {
                if (pair.Key == null ||
                    pair.Key.Kind != RestNodeHotspotKind.ExitPortal)
                {
                    continue;
                }

                foreach (KeyValuePair<PlayerActor, int> actorPair in pair.Value)
                {
                    if (actorPair.Key != null &&
                        actorPair.Value > 0 &&
                        actorPair.Key.Definition.Role == role)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsActorInsideHotspot(
            RestNodeHotspot2D hotspot,
            PlayerActor actor)
        {
            return _overlaps.TryGetValue(
                       hotspot,
                       out Dictionary<PlayerActor, int> actors) &&
                   actors.TryGetValue(actor, out int count) &&
                   count > 0;
        }

        private bool IsPortalReady(PlayerRole role)
        {
            return role == PlayerRole.DeepSeek
                ? _deepSeekPortalReady
                : _harnessPortalReady;
        }

        private void SetPortalReady(PlayerRole role, bool ready)
        {
            if (role == PlayerRole.DeepSeek)
            {
                _deepSeekPortalReady = ready;
            }
            else
            {
                _harnessPortalReady = ready;
            }
        }

        private void ResetNodeInteraction()
        {
            _overlaps.Clear();
            _nearestLocalHotspot = null;
            _deepSeekPortalReady = false;
            _harnessPortalReady = false;
            _feedbackUntil = 0f;
            SetActionButtonActive(false);
        }

        private void ShowFeedback(string value)
        {
            _feedbackUntil = Time.unscaledTime + 2.5f;
            SetPrompt(value);
        }

        private void SetPrompt(string value)
        {
            _promptText.text = value ?? string.Empty;
            _promptText.gameObject.SetActive(
                !string.IsNullOrEmpty(_promptText.text));
        }

        private void BroadcastState(RestNodeState state)
        {
            if (IsOnline && _session.IsAuthority)
            {
                _session.SendAuthority(
                    NETWORK_STATE,
                    writer =>
                    {
                        writer.Write((byte)state);
                        writer.Write(_deepSeekPortalReady);
                        writer.Write(_harnessPortalReady);
                    },
                    reliable: true);
            }
        }

        private void ReadNetworkState(byte kind, BinaryReader reader)
        {
            if (!_isInitialized || kind != NETWORK_STATE)
            {
                return;
            }

            RestNodeState state = (RestNodeState)reader.ReadByte();
            if (state <= RestNodeState.Departing)
            {
                ApplyState(state);
                _deepSeekPortalReady = reader.ReadBoolean();
                _harnessPortalReady = reader.ReadBoolean();
            }
        }

        private void ReadPeerRequest(byte kind, BinaryReader reader)
        {
            if (!_isInitialized ||
                kind != NETWORK_PORTAL_READY_REQUEST ||
                !IsOnline ||
                !_session.IsAuthority ||
                State != RestNodeState.Open)
            {
                return;
            }

            PlayerRole guestRole =
                _session.HostRole == PlayerRole.DeepSeek
                    ? PlayerRole.Harness
                    : PlayerRole.DeepSeek;
            bool ready = reader.ReadBoolean();
            SetPortalReady(
                guestRole,
                ready && IsRoleInsidePortal(guestRole));
            BroadcastState(State);
            TryCompletePortalDeparture();
        }

        private void OnSessionOpened(bool authority)
        {
            if (_isInitialized)
            {
                ApplyState(RestNodeState.Combat);
            }
        }

        private void OnPeerJoined()
        {
            if (_isInitialized)
            {
                if (State == RestNodeState.Open && _session.IsAuthority)
                {
                    PlayerRole guestRole =
                        _session.HostRole == PlayerRole.DeepSeek
                            ? PlayerRole.Harness
                            : PlayerRole.DeepSeek;
                    SetPortalReady(guestRole, false);
                }
                BroadcastState(State);
            }
        }
    }
}
