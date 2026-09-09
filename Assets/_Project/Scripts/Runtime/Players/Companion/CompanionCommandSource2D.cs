using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Revive;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    public enum CompanionPlan { Inactive, Downed, Follow, Fight, Evade, ClearForRescue, ApproachRescue, Revive }

    /// <summary>感知→战术→移动/角色策略→统一命令。仅被选中的同伴由Dispatcher驱动。</summary>
    public sealed class CompanionCommandSource2D : MonoBehaviour, ICommandSource
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
        [Header("运行时观察（只读用途）")]
        [SerializeField] private CompanionPlan _plan;
        [SerializeField] private string _reason;
        [SerializeField] private float _danger;
        [SerializeField] private Vector2 _destination;
        [SerializeField] private GameObject _target;
        private Vector2 _move;
        private float _untilDecision;
        private bool _initialized;
        private bool _hasTick;
        private uint _tick;
        private uint _sequence;
        private PlayerCommand _cached;

        public CompanionPlan Plan => Life != null && Life.State == PlayerLifeState.Downed ? CompanionPlan.Downed : _plan;
        public string Reason => Plan == CompanionPlan.Downed ? "自己倒地，停止输入" : _reason;
        public float Danger => _danger;

        private void Awake()
        {
            _initialized = Config != null && Config.IsValid && Sensor != null && Combat != null &&
                Combat.IsValid && Owner != null && Body != null && Shape != null && MotorConfig != null &&
                Health != null && Life != null && AllyLife != null && Revive != null && TeamGuard != null &&
                Sensor.Initialize();
            if (!_initialized) Debug.LogError("[CompanionAI] 同伴显式依赖或配置不完整，未启用。", this);
            if (Life != null) Life.StateChanged += OnLifeChanged;
        }

        private void OnDestroy()
        {
            if (Life != null) Life.StateChanged -= OnLifeChanged;
        }

        private void OnLifeChanged(PlayerLifeStateController2D owner, PlayerLifeState state)
        {
            // 倒地会停用Dispatcher，因此观察状态必须响应生命事件而非等下一个命令。
            if (state == PlayerLifeState.Downed && _plan != CompanionPlan.Inactive)
            {
                _plan = CompanionPlan.Downed;
                _reason = "自己倒地，停止输入";
            }
            _untilDecision = 0;
        }

        public void ReleaseControl()
        {
            _plan = CompanionPlan.Inactive;
            _reason = "当前角色由其他控制源操纵";
            _move = Vector2.zero;
        }

        public void ResetIntent()
        {
            _hasTick = false;
            _untilDecision = 0;
            _move = Vector2.zero;
            Combat.ResetIntent();
        }

        public bool TryGetCommand(uint simulationTick, out PlayerCommand command)
        {
            command = default;
            if (!_initialized || !isActiveAndEnabled) return false;
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
            if (_untilDecision <= 0)
            {
                Decide(position);
                _untilDecision = Config.DecisionInterval;
            }
            bool rescue = AllyLife.State == PlayerLifeState.Downed &&
                (_plan == CompanionPlan.ApproachRescue || _plan == CompanionPlan.Revive);
            // 实际触发器已经接纳才停下；不另设一个猜测的救援半径。
            bool stopForRescue = rescue && Revive.OfferedTarget == AllyLife;
            if (stopForRescue) { _plan = CompanionPlan.Revive; _reason = "进入救援圈，停手等待自动引导"; }
            bool guardWanted = _danger >= Config.GuardDangerThreshold ||
                (AllyLife.State == PlayerLifeState.Downed && Vector2.Distance(position, AllyLife.transform.position) <= TeamGuard.Radius);
            Combat.Build(Sensor, position, rescue, guardWanted, _danger >= Config.EmergencyDanger,
                dt, out var aim, out var skill, out var attack, out var cancel);
            return Cache(simulationTick, stopForRescue ? Vector2.zero : _move, aim, skill, attack, cancel, out command);
        }

        private void Decide(Vector2 position)
        {
            Vector2 ally = AllyLife.transform.position;
            Vector2 extent = Shape.bounds.extents;
            Vector2 colliderOffset = (Vector2)Shape.bounds.center - position;
            Sensor.Refresh(position, ally);
            _target = Sensor.Target == null ? null : Sensor.Target.gameObject;
            _danger = Sensor.Danger(position + colliderOffset, Body.linearVelocity, extent.magnitude);
            bool protectedHere = TeamGuard.IsActive && Vector2.Distance(position, TeamGuard.transform.position) <= TeamGuard.Radius;
            bool downedAlly = AllyLife.State == PlayerLifeState.Downed;
            float riskLimit = Config.RescueDangerLimit * Mathf.Max(Config.LowHealthFraction,
                Health.CurrentHealth / Health.MaximumHealth);
            float rescueDanger = Sensor.Danger(ally + colliderOffset, Vector2.zero, extent.magnitude);
            _destination = ally + Config.FormationOffset;
            _plan = _target != null ? CompanionPlan.Fight : CompanionPlan.Follow;
            _reason = _target != null ? "选择威胁/蓄力/残血评分最高的目标" : "无目标，保持编队";
            if (downedAlly)
            {
                bool canApproach = rescueDanger <= riskLimit || protectedHere;
                _plan = canApproach ? CompanionPlan.ApproachRescue : CompanionPlan.ClearForRescue;
                _destination = canApproach ? ally : ally + Config.FormationOffset;
                _reason = canApproach ? "救援点风险可接受，靠近队友" : "救援点受威胁，先清场";
            }
            if (_danger >= Config.EmergencyDanger && !protectedHere)
            {
                _plan = CompanionPlan.Evade;
                _reason = "预测碰撞风险高，优先避险并允许防御技能";
            }
            _destination = CompanionThreatMath.ClampPoint(_destination + colliderOffset, MotorConfig.MovementBounds, extent) - colliderOffset;
            _move = CompanionSteering2D.Choose(Sensor, Config, MotorConfig, position,
                Body.linearVelocity, extent, _destination, _move, colliderOffset);
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
