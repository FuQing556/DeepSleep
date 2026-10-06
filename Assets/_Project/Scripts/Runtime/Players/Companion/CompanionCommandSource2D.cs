using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Revive;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    public enum CompanionPlan { Inactive, Downed, Follow, Fight, Evade, ClearForRescue, ApproachRescue, Revive,
        ApproachPortal, HoldPortal, Navigate, WaitForRoute }

    /// <summary>感知→战术→移动/角色策略→统一命令。仅被选中的同伴由Dispatcher驱动。</summary>
    public sealed class CompanionCommandSource2D : MonoBehaviour, ICommandSource, IPlayerReviveStartBlocker
    {
        public CompanionTacticsConfig Config;
        public CompanionBattleSensor2D Sensor;
        public CompanionCombatPolicy2D Combat;
        public Transform Owner;
        public Rigidbody2D Body;
        public Collider2D Shape;
        public PlayerMotorConfig MotorConfig;
        public HealthComponent Health;
        public PlayerLifeStateController2D Life;
        public PlayerLifeStateController2D AllyLife;
        public PlayerReviveActionChannel Revive;
        public DeepSeekRiceGuardController TeamGuard;
        public CompanionNodeGoal2D NodeGoal;
        public CompanionSquadAnchor2D SquadAnchor;
        [Header("运行时观察（只读用途）")]
        [SerializeField] private CompanionPlan _plan;
        [SerializeField] private string _reason;
        [SerializeField] private float _danger;
        [SerializeField] private Vector2 _destination;
        [SerializeField] private GameObject _target;
        [SerializeField] private Vector2 _waypoint;
        [SerializeField] private bool _routeBlocked;
        private readonly CompanionNavigation2D _navigation = new CompanionNavigation2D();
        private Vector2 _move;
        private float _untilDecision;
        private bool _initialized;
        private bool _portalActive;
        private bool _hasTick;
        private uint _tick;
        private uint _sequence;
        private PlayerCommand _cached;
        private bool _controlsRescue;
        private float _rescueSafeSeconds;

        public bool BlocksReviveStart => _controlsRescue && Combat.Role == DeepSleep.Runtime.Players.Identity.PlayerRole.Harness &&
            AllyLife.State == PlayerLifeState.Downed &&
            _plan != CompanionPlan.ApproachRescue && _plan != CompanionPlan.Revive;

        public CompanionPlan Plan => Life != null && Life.State == PlayerLifeState.Downed ? CompanionPlan.Downed : _plan;
        public string Reason => Plan == CompanionPlan.Downed ? "自己倒地，停止输入" : _reason;
        public float Danger => _danger;
        public Vector2 Destination => _destination;
        public Vector2 Waypoint => _waypoint;
        public bool RouteBlocked => _routeBlocked;

        private void Awake()
        {
            _initialized = Config != null && Config.IsValid && Sensor != null && Combat != null &&
                Combat.IsValid && Owner != null && Body != null && Shape != null && MotorConfig != null &&
                Health != null && Life != null && AllyLife != null && Revive != null && TeamGuard != null &&
                NodeGoal != null && SquadAnchor != null &&
                Sensor.Initialize();
            if (_initialized) _navigation.Configure(Config.NavigationCellSize, Config.NavigationPadding,
                Config.NavigationPredictionSeconds, Config.NavigationReplanSeconds, Config.NavigationGoalHysteresis,
                Config.NavigationStuckSeconds, Config.NavigationMaxExpandedNodes);
            if (!_initialized) Debug.LogError("[CompanionAI] 同伴显式依赖或配置不完整，未启用。", this);
            if (Life != null) Life.StateChanged += OnLifeChanged;
        }

        private void OnDestroy()
        {
            if (Life != null) Life.StateChanged -= OnLifeChanged;
        }

        private void OnDisable() => ReleaseControl();

        private void OnLifeChanged(PlayerLifeStateController2D owner, PlayerLifeState state)
        {
            // 倒地会停用Dispatcher，因此观察状态必须响应生命事件而非等下一个命令。
            if (state == PlayerLifeState.Downed && _plan != CompanionPlan.Inactive)
            {
                _plan = CompanionPlan.Downed;
                _reason = "自己倒地，停止输入";
            }
            _untilDecision = 0;
            _rescueSafeSeconds = 0;
            _navigation.Reset();
        }

        public void ReleaseControl()
        {
            _controlsRescue = false;
            _rescueSafeSeconds = 0;
            _plan = CompanionPlan.Inactive;
            _reason = "当前角色由其他控制源操纵";
            _move = Vector2.zero;
            _routeBlocked = _portalActive = false;
            _navigation.Reset();
            if (NodeGoal != null) NodeGoal.ResetIntent();
        }

        public void ResetIntent()
        {
            _controlsRescue = false;
            _rescueSafeSeconds = 0;
            _hasTick = false;
            _untilDecision = 0;
            _move = Vector2.zero;
            _routeBlocked = _portalActive = false;
            _navigation.Reset();
            if (NodeGoal != null) NodeGoal.ResetIntent();
            Combat.ResetIntent();
        }

        public bool TryGetCommand(uint simulationTick, out PlayerCommand command)
        {
            command = default;
            if (!_initialized || !isActiveAndEnabled) return false;
            _controlsRescue = true;
            if (_hasTick && _tick == simulationTick) { command = _cached; return true; }
            _hasTick = true;
            _tick = simulationTick;
            float dt = Time.fixedDeltaTime;
            Vector2 position = Owner.position;
            if (Life.State == PlayerLifeState.Downed)
            {
                _plan = CompanionPlan.Downed;
                _reason = "自己倒地，停止输入";
                _untilDecision = 0;
                return Cache(simulationTick, Vector2.zero, default, 0, 0, 0, out command);
            }
            _untilDecision -= dt;
            bool portalNow = AllyLife.State != PlayerLifeState.Downed && NodeGoal.TryGetGoal(out _, out _);
            if (portalNow != _portalActive) { _untilDecision = 0; _navigation.Reset(); _portalActive = portalNow; }
            if (_untilDecision <= 0)
            {
                Decide(position);
                _untilDecision = Config.DecisionInterval;
            }
            bool rescue = AllyLife.State == PlayerLifeState.Downed &&
                (_plan == CompanionPlan.ApproachRescue || _plan == CompanionPlan.Revive);
            // 实际触发器已经接纳才停下；不另设一个猜测的救援半径。
            bool stopForRescue = rescue && Revive.OfferedTarget == AllyLife;
            bool stopAtPortal = portalNow && NodeGoal.TryGetGoal(out _, out bool holdNow) && holdNow;
            if (stopAtPortal) { _plan = CompanionPlan.HoldPortal; _reason = "已进入门内，停稳等待"; }
            if (stopForRescue) { _plan = CompanionPlan.Revive; _reason = "进入救援圈，停手等待自动引导"; }
            bool guardWanted = _danger >= Config.GuardDangerThreshold ||
                (AllyLife.State == PlayerLifeState.Downed && Vector2.Distance(position, AllyLife.transform.position) <= TeamGuard.Radius);
            Combat.Build(Sensor, position, rescue, guardWanted, _danger >= Config.EmergencyDanger,
                dt, out var aim, out var skill, out var attack, out var cancel);
            // 冷却/收刀的空档也不得偷起救；近处再现威胁则明确取消原引导。
            if (BlocksReviveStart && Revive.IsChanneling) cancel |= CommandButtonState.Pressed;
            return Cache(simulationTick, stopForRescue || stopAtPortal ? Vector2.zero : _move, aim, skill, attack, cancel, out command);
        }

        private void Decide(Vector2 position)
        {
            Vector2 ally = AllyLife.transform.position;
            Bounds shapeBounds = Shape.bounds;
            Vector2 extent = shapeBounds.extents;
            Vector2 colliderOffset = (Vector2)shapeBounds.center - position;
            Sensor.Refresh(position, ally);
            _target = Sensor.Target == null ? null : Sensor.Target.gameObject;
            _danger = Sensor.Danger(position + colliderOffset, Body.linearVelocity, extent.magnitude);
            if (Sensor.ObstacleSaturated || !CompanionNavigation2D.IsSegmentSafe(position + colliderOffset,
                position + colliderOffset + Body.linearVelocity * Config.PredictionSeconds, 0,
                Config.PredictionSeconds, extent, Config.NavigationPadding, Sensor.Obstacles, Sensor.ObstacleCount))
                _danger = Mathf.Max(_danger, Config.EmergencyDanger);
            bool protectedHere = TeamGuard.IsActive && Vector2.Distance(position, TeamGuard.transform.position) <= TeamGuard.Radius;
            bool downedAlly = AllyLife.State == PlayerLifeState.Downed;
            if (!downedAlly) _rescueSafeSeconds = 0;
            _destination = ally + Config.FormationOffset;
            if (SquadAnchor.TryGetGoal(Combat.Role, Config.FormationOffset, out var squadGoal)) _destination = squadGoal;
            _plan = _target != null ? CompanionPlan.Fight : CompanionPlan.Follow;
            _reason = _target != null ? "选择威胁/蓄力/残血评分最高的目标" : "无目标，保持编队";
            Vector2 portalGoal = default;
            bool holdPortal = false;
            bool portal = !downedAlly && NodeGoal.TryGetGoal(out portalGoal, out holdPortal);
            if (portal)
            {
                _destination = portalGoal;
                _plan = holdPortal ? CompanionPlan.HoldPortal : CompanionPlan.ApproachPortal;
                _reason = holdPortal ? "已在门内停稳，等待原有准备规则" : "配合离开意图，前往传送门";
            }
            if (downedAlly)
            {
                float riskLimit = Config.RescueDangerLimit * Mathf.Max(Config.LowHealthFraction,
                    Health.CurrentHealth / Health.MaximumHealth);
                float rescueDanger = Sensor.Danger(ally + colliderOffset, Vector2.zero, extent.magnitude);
                if (Sensor.ObstacleSaturated || !CompanionNavigation2D.IsSegmentSafe(ally + colliderOffset,
                    ally + colliderOffset, 0, Config.PredictionSeconds, extent, Config.NavigationPadding,
                    Sensor.Obstacles, Sensor.ObstacleCount)) rescueDanger += Config.EmergencyDanger;
                bool canApproach = rescueDanger <= riskLimit || protectedHere;
                if (Combat.Role == DeepSleep.Runtime.Players.Identity.PlayerRole.Harness)
                {
                    float radius = Revive.IsChanneling ? Config.HarnessRescueInterruptRadius : Config.HarnessRescueClearRadius;
                    bool occupied = Sensor.PrioritizeRescueThreat(position, ally, radius, extent.magnitude);
                    _rescueSafeSeconds = !occupied && canApproach
                        ? _rescueSafeSeconds + Config.DecisionInterval : 0f;
                    canApproach &= !occupied && (Revive.IsChanneling || _rescueSafeSeconds >= Config.HarnessRescueSafeSeconds);
                    _target = Sensor.Target == null ? null : Sensor.Target.gameObject;
                }
                _plan = canApproach ? CompanionPlan.ApproachRescue : CompanionPlan.ClearForRescue;
                _destination = canApproach ? ally : ally + Config.FormationOffset;
                _reason = canApproach ? "救援区域已安全，靠近队友" : "先清理救援区域并等待安全窗口";
            }
            if (_danger >= Config.EmergencyDanger && !protectedHere)
            {
                _plan = CompanionPlan.Evade;
                _reason = "预测碰撞风险高，优先避险并允许防御技能";
            }
            _destination = CompanionThreatMath.ClampPoint(_destination + colliderOffset, MotorConfig.MovementBounds, extent) - colliderOffset;
            var route = _navigation.Navigate(Sensor.Obstacles, Sensor.ObstacleCount, Sensor.ObstacleSaturated,
                MotorConfig.MovementBounds, position + colliderOffset, extent, Body.linearVelocity,
                MotorConfig.MaximumSpeed, MotorConfig.Acceleration, _destination + colliderOffset, Config.DecisionInterval);
            _waypoint = route.Waypoint - colliderOffset;
            _routeBlocked = route.ShouldWait && !route.ReachedGoal;
            if (_routeBlocked)
            {
                Sensor.PrioritizeBlockedRoute(position, _destination, extent.magnitude);
                _target = Sensor.Target == null ? null : Sensor.Target.gameObject;
            }
            if (_routeBlocked && downedAlly && _plan == CompanionPlan.ApproachRescue)
            { _plan = CompanionPlan.ClearForRescue; _reason = "救援路线被气泡截断，先避险清障"; }
            else if (_routeBlocked && !portal && !downedAlly)
            { _plan = CompanionPlan.WaitForRoute; _reason = "通路暂不安全，等待或退让后重规划"; }
            else if (Sensor.ObstacleCount > 0 && !portal && !downedAlly && _plan != CompanionPlan.Evade)
            { _plan = CompanionPlan.Navigate; _reason = "按可见移动气泡规划绕行路点"; }
            _move = CompanionSteering2D.Choose(Sensor, Config, MotorConfig, position,
                Body.linearVelocity, extent, _waypoint, _move, colliderOffset);
        }

        private bool Cache(uint tick, Vector2 move, AimIntent aim, CommandButtonState skill,
            CommandButtonState attack, CommandButtonState cancel, out PlayerCommand command)
        {
            _cached = new PlayerCommand(++_sequence, tick, move, aim, skill, 0, attack, cancel, 0, 0);
            command = _cached;
            return true;
        }
    }
}
