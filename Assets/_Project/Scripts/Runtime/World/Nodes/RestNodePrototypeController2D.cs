using System;
using System.Collections.Generic;
using System.IO;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Orientation;
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

    /// <summary>节点对移动决策提供的只读门状态；不授予准备、奖励或离场权限。</summary>
    public readonly struct RestNodePortalGoal
    {
        public RestNodePortalGoal(Vector2 destination, bool isInside, bool fullyInside, bool ready)
        {
            Destination = destination;
            IsInside = isInside;
            FullyInside = fullyInside;
            Ready = ready;
        }

        public Vector2 Destination { get; }
        public bool IsInside { get; }
        public bool FullyInside { get; }
        public bool Ready { get; }
    }

    /// <summary>
    /// 休息节点表现与交互：章节负责战斗域/检查点，本组件只负责揭示、占用、准备与离开。
    /// </summary>
    public sealed class RestNodePrototypeController2D : MonoBehaviour
    {
        private const byte NETWORK_STATE = NetworkMessageCatalog.Authority.RestNodeState;
        private const byte NETWORK_PORTAL_READY_REQUEST = NetworkMessageCatalog.Peer.RestNodeReady;

        [Header("战斗与网络")]
        [SerializeField] private CoopSessionController _session;

        [Header("背景揭示")]
        [SerializeField] private LoopingBackgroundLayer2D _scrollingCloudLayer;
        [SerializeField] private Transform _cloudLayerRoot;
        [Tooltip("不在背景根下、但需要随战斗背景一起淡出的前景。不会移动或禁用其玩法对象。")]
        [SerializeField] private SpriteRenderer[] _additionalTransitionRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private SpriteRenderer _templeRenderer;
        [SerializeField, Min(0.01f)] private float _cloudFadeSeconds = 1.25f;

        [Header("本地文字交互")]
        [SerializeField] private PlayerControlAssignment _controlAssignment;
        [SerializeField] private Text _promptText;
        [SerializeField] private Button _actionButton;
        [SerializeField] private Text _actionButtonLabel;
        [SerializeField] private RestNodeHotspot2D[] _hotspots;
        [SerializeField] private RestNodeUpgradeController _upgradeController;

        [Header("节点移动朝向")]
        [SerializeField] private PlayerMovementMotor2D _deepSeekMovement;
        [SerializeField] private PlayerFacingController2D _deepSeekFacing;
        [SerializeField] private PlayerMovementMotor2D _harnessMovement;
        [SerializeField] private PlayerFacingController2D _harnessFacing;

        private readonly List<SpriteRenderer> _cloudRenderers = new();
        private readonly List<Color> _cloudBaseColors = new();
        private readonly Dictionary<RestNodeHotspot2D,
            Dictionary<PlayerActor, int>> _overlaps = new();
        private float _revealElapsed;
        private float _feedbackUntil;
        private Color _templeBaseColor;
        private bool _isInitialized;
        private bool _presentationSuspended;
        private bool _deepSeekPortalReady;
        private bool _harnessPortalReady;
        private RestNodeHotspot2D _nearestLocalHotspot;
        private BoxCollider2D[] _portalGoalShapes;

        public RestNodeState State { get; private set; }
        public event Action<RestNodeState> StateChanged;
        /// <summary>真人通过交互或网络请求明确取消准备；通知 AI 撤销跟门意图，不改变准备规则。</summary>
        public event Action<PlayerRole> PortalReadyCancelled;

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;

        private bool CanDriveState => _session == null || _session.Phase == SessionPhase.Offline ||
            _session.IsAuthority;

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

            _portalGoalShapes = new BoxCollider2D[_hotspots.Length];
            for (int index = 0; index < _hotspots.Length; index++)
                if (_hotspots[index].Kind == RestNodeHotspotKind.ExitPortal)
                    _portalGoalShapes[index] = _hotspots[index].GetComponent<BoxCollider2D>();
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
            // 场景卸载顺序不保证UI晚于触发器；只清逻辑，不在这里反向访问UI/激活热点。
            _overlaps.Clear();
            _nearestLocalHotspot = null;
            _deepSeekPortalReady = _harnessPortalReady = false;
            _feedbackUntil = 0f;
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
            if (!_isInitialized || _presentationSuspended) return;
            switch (State)
            {
                case RestNodeState.Revealing:
                    _revealElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(
                        _revealElapsed / _cloudFadeSeconds);
                    SetCloudAlpha(1f - progress);
                    if (progress >= 1f && CanDriveState)
                    {
                        ApplyState(RestNodeState.Open);
                        if (CanDriveState)
                        {
                            BroadcastState(RestNodeState.Open);
                        }
                    }
                    break;

                case RestNodeState.Open:
                    RefreshSpatialOccupancy();
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

            if (UsesMovementFacing(State))
            {
                ApplyMovementFacing(_deepSeekMovement, _deepSeekFacing);
                ApplyMovementFacing(_harnessMovement, _harnessFacing);
            }
        }

        public bool BeginNodeTransition()
        {
            if (!_isInitialized || _presentationSuspended ||
                State != RestNodeState.Combat ||
                !CanDriveState)
            {
                return false;
            }

            ApplyState(RestNodeState.Clearing);
            BroadcastState(RestNodeState.Clearing);
            return true;
        }

        /// <summary>仅由章节在确认战斗域清空后提交；节点不自行扫描或回收敌人。</summary>
        public bool CompleteClearing()
        {
            if (!_isInitialized || _presentationSuspended || !CanDriveState ||
                State != RestNodeState.Clearing) return false;
            ApplyState(RestNodeState.Revealing);
            BroadcastState(State);
            return true;
        }

        /// <summary>
        /// 直接读取当前门几何，不刷新占用、不准备、不离场。body 为空时只读角色根点与准备状态；
        /// 提供 body 时返回对齐碰撞体中心的根目标，且四个世界 AABB 角点都在门内才算完整进入。
        /// </summary>
        public bool TryReadPortalGoal(PlayerActor actor, Collider2D body, out RestNodePortalGoal goal)
        {
            goal = default;
            if (!_isInitialized || !isActiveAndEnabled || State != RestNodeState.Open ||
                actor == null || actor.Definition == null || _portalGoalShapes == null) return false;
            for (int index = 0; index < _portalGoalShapes.Length; index++)
            {
                BoxCollider2D portal = _portalGoalShapes[index];
                RestNodeHotspot2D hotspot = _hotspots[index];
                if (portal == null || !portal.enabled || !hotspot.isActiveAndEnabled || !hotspot.Allows(actor)) continue;
                Vector2 center = portal.transform.TransformPoint(portal.offset);
                bool inside = ContainsPortalPoint(portal, actor.transform.position);
                bool fullyInside = false;
                if (body != null && body.enabled && body.gameObject.activeInHierarchy)
                {
                    Bounds bounds = body.bounds;
                    center -= (Vector2)(bounds.center - actor.transform.position);
                    fullyInside = ContainsPortalPoint(portal, new Vector2(bounds.min.x, bounds.min.y)) &&
                        ContainsPortalPoint(portal, new Vector2(bounds.min.x, bounds.max.y)) &&
                        ContainsPortalPoint(portal, new Vector2(bounds.max.x, bounds.min.y)) &&
                        ContainsPortalPoint(portal, new Vector2(bounds.max.x, bounds.max.y));
                }
                goal = new RestNodePortalGoal(center, inside, fullyInside, IsPortalReady(actor.Definition.Role));
                return true;
            }
            return false;
        }

        private static bool ContainsPortalPoint(BoxCollider2D portal, Vector2 worldPoint)
        {
            Vector2 point = (Vector2)portal.transform.InverseTransformPoint(worldPoint) - portal.offset;
            Vector2 half = portal.size * .5f;
            return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y;
        }

        public bool ReturnToCombat()
        {
            if (!_isInitialized || _presentationSuspended ||
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
            if (!_isInitialized)
            {
                return;
            }

            SetPresentationSuspended(true);
        }

        public void SuspendCombatForSettlement()
        {
            if (!_isInitialized)
            {
                return;
            }

            SetPresentationSuspended(true);
        }

        /// <summary>章节/副本只冻结节点表现，不在节点中启停战斗或结算经济。</summary>
        public void SetPresentationSuspended(bool suspended)
        {
            if (!_isInitialized || _presentationSuspended == suspended) return;
            _presentationSuspended = suspended;
            if (suspended)
            {
                _scrollingCloudLayer.SetScrollMultiplier(0f);
                SetHotspotsActive(false);
                SetPrompt(string.Empty);
            }
            else ApplyState(State, true);
        }

        public bool TryValidateCheckpointRestore(out string reason)
        {
            if (!_isInitialized || !isActiveAndEnabled || !CanDriveState)
            {
                reason = "节点未初始化、未启用或本端没有检查点恢复权限。";
                return false;
            }
            return TryValidateConfiguration(out reason);
        }

        public bool RestoreCheckpointNode()
        {
            if (!TryValidateCheckpointRestore(out _))
            {
                return false;
            }

            _presentationSuspended = false;
            ResetNodeInteraction();
            ApplyState(RestNodeState.Open, true);
            SetPrompt("已返回上一休息节点 · 本次战斗收益已回滚");
            BroadcastState(State);
            return true;
        }

        public bool RestartCombatFromCheckpoint()
        {
            if (!TryValidateCheckpointRestore(out _))
            {
                return false;
            }

            _presentationSuspended = false;
            ApplyState(RestNodeState.Combat, true);
            BroadcastState(RestNodeState.Combat);
            return true;
        }

        internal void NotifyEntered(
            RestNodeHotspot2D hotspot,
            PlayerActor actor)
        {
            if (!_isInitialized || !isActiveAndEnabled || State != RestNodeState.Open ||
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
            if (!_isInitialized || !isActiveAndEnabled || State != RestNodeState.Open || hotspot == null ||
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

            foreach (var renderer in _additionalTransitionRenderers)
            {
                if (renderer == null || renderer == _templeRenderer)
                {
                    reason = "附加淡出层不能为空或包含休息节点自身图片。";
                    return false;
                }
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

            if (_deepSeekMovement == null ||
                _deepSeekFacing == null ||
                _harnessMovement == null ||
                _harnessFacing == null)
            {
                reason = "未完整配置两名角色的节点移动与朝向组件。";
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

        private void ApplyState(RestNodeState state, bool forcePresentation = false)
        {
            bool changed = State != state;
            if (!changed && !forcePresentation) return;
            State = state;
            switch (state)
            {
                case RestNodeState.Combat:
                    RestoreCombatFacing();
                    ResetNodeInteraction();
                    SetHotspotsActive(false);
                    CaptureCloudRenderers();
                    SetCloudAlpha(1f);
                    SetTempleAlpha(0f);
                    _scrollingCloudLayer.SetScrollMultiplier(1f);
                    SetPrompt(string.Empty);
                    break;

                case RestNodeState.Clearing:
                    ResetNodeInteraction();
                    CaptureCloudRenderers();
                    SetTempleAlpha(1f);
                    _scrollingCloudLayer.SetScrollMultiplier(0f);
                    SetHotspotsActive(false);
                    SetPrompt("已抵达休息节点 · 清理残余威胁");
                    break;

                case RestNodeState.Revealing:
                    _revealElapsed = 0f;
                    CaptureCloudRenderers();
                    SetTempleAlpha(1f);
                    _scrollingCloudLayer.SetScrollMultiplier(0f);
                    SetPrompt("云层正在散开……");
                    break;

                case RestNodeState.Open:
                    CaptureCloudRenderers();
                    SetCloudAlpha(0f);
                    SetTempleAlpha(1f);
                    SetHotspotsActive(true);
                    _scrollingCloudLayer.SetScrollMultiplier(0f);
                    SetPrompt("休息节点 · 靠近设施进行调查");
                    break;

                case RestNodeState.Departing:
                    _revealElapsed = 0f;
                    ResetNodeInteraction();
                    SetHotspotsActive(false);
                    CaptureCloudRenderers();
                    SetCloudAlpha(0f);
                    SetTempleAlpha(1f);
                    _scrollingCloudLayer.SetScrollMultiplier(0f);
                    SetPrompt("即将离开休息节点……");
                    break;
            }

            if (_presentationSuspended)
            {
                _scrollingCloudLayer.SetScrollMultiplier(0f);
                SetHotspotsActive(false);
            }
            if (changed) StateChanged?.Invoke(State);
        }

        private void RestoreCombatFacing()
        {
            _deepSeekFacing.SetDirection(FacingDirection.Right);
            _harnessFacing.SetDirection(FacingDirection.Right);
        }

        private static bool UsesMovementFacing(RestNodeState state)
        {
            return state == RestNodeState.Revealing ||
                   state == RestNodeState.Open ||
                   state == RestNodeState.Departing;
        }

        private static void ApplyMovementFacing(
            PlayerMovementMotor2D movement,
            PlayerFacingController2D facing)
        {
            float horizontalVelocity = movement.Velocity.x;
            if (Mathf.Approximately(horizontalVelocity, 0f))
            {
                return;
            }

            facing.SetDirection(horizontalVelocity < 0f
                ? FacingDirection.Left
                : FacingDirection.Right);
        }

        private void CaptureCloudRenderers()
        {
            _cloudRenderers.Clear();
            _cloudBaseColors.Clear();
            _cloudLayerRoot.GetComponentsInChildren(
                true,
                _cloudRenderers);
            foreach (var renderer in _additionalTransitionRenderers)
                if (!_cloudRenderers.Contains(renderer)) _cloudRenderers.Add(renderer);
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
            if (!_isInitialized || !isActiveAndEnabled || State != RestNodeState.Open ||
                _controlAssignment == null || _actionButton == null || _actionButtonLabel == null || _promptText == null)
                return;
            PlayerActor localActor = _controlAssignment.CurrentLocalPlayerActor;
            if (localActor == null)
            {
                _nearestLocalHotspot = null;
                SetActionButtonActive(false);
                return;
            }
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

        // 客户端 Rigidbody2D.simulated=false，不会触发 Enter/Exit；托管切换、
        // 节点显隐及网络位置同步也不能依赖历史触发次数。节点只查两个角色根点。
        // 不开启客户机物理，不改现有碰撞体参数，不把托管等同于放弃交互权限。
        private void RefreshSpatialOccupancy()
        {
            var ds = _deepSeekMovement.GetComponentInParent<PlayerActor>();
            var hs = _harnessMovement.GetComponentInParent<PlayerActor>();
            foreach (var hotspot in _hotspots)
            {
                if (hotspot == null) continue;
                if (!_overlaps.TryGetValue(hotspot, out var occupants))
                {
                    occupants = new Dictionary<PlayerActor, int>();
                    _overlaps.Add(hotspot, occupants);
                }
                occupants.Clear();
                if (hotspot.ContainsActorPosition(ds)) occupants[ds] = 1;
                if (hotspot.ContainsActorPosition(hs)) occupants[hs] = 1;
            }
            if (ds != null && !IsRoleInsidePortal(PlayerRole.DeepSeek)) CancelPortalReadyForActor(ds);
            if (hs != null && !IsRoleInsidePortal(PlayerRole.Harness)) CancelPortalReadyForActor(hs);
        }

        private void SetActionButtonActive(bool active)
        {
            if (_actionButton != null) _actionButton.gameObject.SetActive(active);
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
            RefreshSpatialOccupancy();
            RefreshNearestPrompt();
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
            if (!ready) PortalReadyCancelled?.Invoke(role);
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
            if (_promptText == null) return;
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
            if (!_isInitialized || kind != NETWORK_STATE || !IsOnline || _session.IsAuthority ||
                !NetworkMessageCatalog.TryValidatePayload(kind,
                    NetworkMessageCatalog.Direction.AuthorityToPeer, reader, out _))
            {
                return;
            }

            RestNodeState state = (RestNodeState)reader.ReadByte();
            bool deepSeekReady = reader.ReadBoolean();
            bool harnessReady = reader.ReadBoolean();
            // 完整校验和读取后才提交；截断或伪节点帧不能先切换背景/战斗状态。
            ApplyState(state);
            _deepSeekPortalReady = deepSeekReady;
            _harnessPortalReady = harnessReady;
        }

        private void ReadPeerRequest(byte kind, BinaryReader reader)
        {
            if (!_isInitialized ||
                kind != NETWORK_PORTAL_READY_REQUEST ||
                !IsOnline ||
                !_session.IsAuthority ||
                State != RestNodeState.Open ||
                !NetworkMessageCatalog.TryValidatePayload(kind,
                    NetworkMessageCatalog.Direction.PeerToAuthority, reader, out _))
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
            if (!ready) PortalReadyCancelled?.Invoke(guestRole);
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
