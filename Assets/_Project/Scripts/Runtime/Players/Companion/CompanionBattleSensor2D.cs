using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>有界局部快照及目标评分。每次评估复用缓冲，满缓冲按不安全处理。</summary>
    public sealed class CompanionBattleSensor2D : MonoBehaviour
    {
        public CombatPerceptionRegistry2D Registry;
        public CompanionObstacleRegistry2D ObstacleRegistry;
        public CompanionTacticsConfig Config;
        public ClaudeEncounter2D Claude;
        private Vector2 _observerPosition;
        private bool HasClaudeContactThreat => Claude != null && Claude.Actor.Body.IsAlive && Claude.Actor.Body.Sphere.enabled &&
            Vector2.Distance(_observerPosition, Claude.Actor.Body.Sphere.bounds.center) <=
            Claude.Actor.Body.Sphere.bounds.extents.x + Claude.Config.MoveSpeed * Config.PredictionSeconds + 2;
        public bool HasClaudeWarning => Claude != null &&
            (HasClaudeContactThreat || Claude.SpatialCut.PendingLaneCount > 0 || Claude.TrackingCut.State == ClaudeTrackingCutState.Locked ||
             Claude.Energy.State == ClaudeEnergyState.Flying ||
             (Claude.SecondaryEnergy != null && Claude.SecondaryEnergy.State == ClaudeEnergyState.Flying));
        public bool HasMechanicTarget => Target != null && MechanicPriority(Target) > 0;

        public bool CanAttack(CombatPerceptionBody2D body, Vector2 position)
        {
            if (Claude == null) return true;
            var energy = body == Claude.Energy.Perception ? Claude.Energy :
                Claude.SecondaryEnergy != null && body == Claude.SecondaryEnergy.Perception ? Claude.SecondaryEnergy : null;
            if (energy == null) return true;
            if (energy.State != ClaudeEnergyState.Flying) return false;
            float clearance = energy.ExplosionRadius + Config.SafetyPadding;
            float ownerRadius = 0;
            foreach (var shape in energy.TargetShapes) ownerRadius = Mathf.Max(ownerRadius, shape.bounds.extents.magnitude);
            clearance += ownerRadius;
            if (Vector2.Distance(position, energy.Position) <= clearance) return false;
            for (int i = 0; i < energy.TargetLives.Length; i++)
                if (energy.TargetLives[i].State == DeepSleep.Runtime.Players.LifeCycle.PlayerLifeState.Alive &&
                    Vector2.Distance(energy.TargetShapes[i].bounds.center, energy.Position) <= clearance) return false;
            return true;
        }
        private float MechanicPriority(CombatPerceptionBody2D body)
        {
            if (Claude == null) return 0;
            foreach (var book in Claude.Permissions.Books)
                if (book.IsOpen && body == book.Perception)
                    return book.Permission == ClaudePermission.Skill ? 20 : 50;
            return body == Claude.Energy.Perception || (Claude.SecondaryEnergy != null && body == Claude.SecondaryEnergy.Perception) ? 35 : 0;
        }
        public static float LaneRisk(BeamLaneSnapshot lane, Vector2 position, float radius)
        {
            if (!lane.IsValid) return 0;
            float along = Mathf.Clamp(Vector2.Dot(position - lane.Origin, lane.Direction), 0, lane.Length);
            float distance = Vector2.Distance(position, lane.Origin + lane.Direction * along);
            return Mathf.Clamp01(1 - distance / (radius + lane.Width * .5f));
        }
        private static Vector2 PredictPoint(Vector2 position, Vector2 velocity, Vector2 extent,
            Vector2 offset, Vector2 input, PlayerMotorConfig motor, float seconds)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(seconds / .04f));
            float dt = seconds / steps;
            for (int i = 0; i < steps; i++)
            {
                PlayerMovementStep.Calculate(ref position, ref velocity, input, motor, extent, offset, dt);
                position += velocity * dt;
            }
            return position + offset;
        }
        public float PredictClaudeRisk(Vector2 position, Vector2 velocity, Vector2 extent,
            Vector2 offset, Vector2 input, PlayerMotorConfig motor)
        {
            if (Claude == null) return 0;
            float risk = 0, radius = extent.magnitude + Config.SafetyPadding;
            var cut = Claude.SpatialCut;
            if (cut.PendingLaneCount > 0)
            {
                Vector2 point = PredictPoint(position, velocity, extent, offset, input, motor, cut.PendingSeconds);
                for (int i = 0; i < cut.PendingLaneCount; i++) risk += LaneRisk(cut.PendingLane(i), point, radius);
            }
            var tracking = Claude.TrackingCut;
            if (tracking.State == ClaudeTrackingCutState.Locked)
            {
                Vector2 point = PredictPoint(position, velocity, extent, offset, input, motor, tracking.LockedSecondsRemaining);
                risk += LaneRisk(tracking.Lane, point, radius);
                if (Claude.Actor.Body.PhaseTwo) risk += LaneRisk(ClaudeTrackingCutPattern2D.CrossLane(tracking.Lane), point, radius);
                if (tracking.SecondaryTargetIndex >= 0) risk += LaneRisk(tracking.SecondaryLane, point, radius);
                if (Claude.Actor.Body.PhaseTwo && tracking.SecondaryTargetIndex >= 0)
                    risk += LaneRisk(ClaudeTrackingCutPattern2D.CrossLane(tracking.SecondaryLane), point, radius);
            }
            risk += PredictEnergyRisk(Claude.Energy, position, velocity, extent, offset, input, motor);
            risk += PredictEnergyRisk(Claude.SecondaryEnergy, position, velocity, extent, offset, input, motor);
            risk += PredictClaudeContactRisk(position, velocity, extent, offset, input, motor);
            return risk;
        }
        // 护罩是接触危险体，不依赖“可受击”标志；锁血/转阶段时也不能贴住。
        private float PredictClaudeContactRisk(Vector2 position, Vector2 velocity, Vector2 extent,
            Vector2 offset, Vector2 input, PlayerMotorConfig motor)
        {
            var body = Claude.Actor.Body;
            if (!body.IsAlive || !body.Sphere.enabled) return 0;
            float radius = body.Sphere.bounds.extents.x + extent.magnitude + Config.SafetyPadding;
            float horizon = Mathf.Max(.1f, Config.PredictionSeconds);
            int steps = Mathf.Max(1, Mathf.CeilToInt(horizon / .04f));
            float dt = horizon / steps, risk = 0;
            for (int i = 1; i <= steps; i++)
            {
                PlayerMovementStep.Calculate(ref position, ref velocity, input, motor, extent, offset, dt);
                position += velocity * dt;
                risk += Mathf.Clamp01(1 - Vector2.Distance(position + offset,
                    Claude.PredictContactCenter(i * dt)) / radius);
            }
            return risk / steps;
        }
        // 与切割共用最高优先级的走位评分；不能为靠近Boss或救援点换取一次爆炸。
        // 爆炸残留仅是美术，不当作持续伤害区；两枚飞行球分别预测。
        private float PredictEnergyRisk(ClaudeEnergyPattern2D energy, Vector2 position, Vector2 velocity,
            Vector2 extent, Vector2 offset, Vector2 input, PlayerMotorConfig motor)
        {
            if (energy == null || energy.State != ClaudeEnergyState.Flying) return 0;
            float radius = energy.ExplosionRadius + extent.magnitude + Config.SafetyPadding;
            float horizon = Mathf.Max(.1f, Config.PredictionSeconds);
            int steps = Mathf.Max(1, Mathf.CeilToInt(horizon / .04f));
            float dt = horizon / steps, total = 0;
            var bounds = energy.Playfield.WorldBounds;
            float core = energy.Config.CollisionRadius;
            for (int i = 1; i <= steps; i++)
            {
                PlayerMovementStep.Calculate(ref position, ref velocity, input, motor, extent, offset, dt);
                position += velocity * dt;
                Vector2 ball = energy.Position + energy.Velocity * (i * dt);
                ball.x = Mathf.Clamp(ball.x, bounds.xMin + core, bounds.xMax - core);
                ball.y = Mathf.Clamp(ball.y, bounds.yMin + core, bounds.yMax - core);
                total += Mathf.Clamp01(1 - Vector2.Distance(position + offset, ball) / radius);
            }
            return total / steps;
        }
        private Collider2D[] _colliders;
        private CombatPerceptionBody2D[] _visible;
        private ContactFilter2D _filter;
        private int _count;
        public bool Saturated { get; private set; }
        public CombatPerceptionBody2D Target { get; private set; }
        public float TargetScore { get; private set; }
        public int NearbyCount { get; private set; }
        public CompanionObstacleSnapshot[] Obstacles { get; private set; }
        public int ObstacleCount { get; private set; }
        public bool ObstacleSaturated { get; private set; }
        public bool HasImminentCharge
        {
            get
            {
                for (int i=0;i<_count;i++)
                {
                    var body=_visible[i];
                    if (body==null || !body.IsObservable || body.DownloadCharge==null || !body.DownloadCharge.IsRunning) continue;
                    var charge=body.DownloadCharge;
                    if (charge.State==DownloadChargeState.Dashing || charge.State==DownloadChargeState.Charging) return true;
                }
                return false;
            }
        }
        /// <summary>候选闪避沿真实加减速/边界逐步预测，不把反向输入当作瞬间反向速度。</summary>
        public float PredictChargeRisk(Vector2 position, Vector2 velocity, Vector2 extent, Vector2 offset,
            Vector2 input, PlayerMotorConfig motor)
        {
            float total=0;
            for(int i=0;i<_count;i++)
            {
                var body=_visible[i];
                if(body==null || !body.IsObservable || body.DownloadCharge==null || !body.DownloadCharge.IsRunning) continue;
                var charge=body.DownloadCharge;
                bool charging=charge.State==DownloadChargeState.Charging;
                if(!charging && charge.State!=DownloadChargeState.Dashing) continue;
                float delay=charging?charge.StateRemaining:0;
                float duration=charging?charge.Config.DashSeconds:charge.StateRemaining;
                bool follows=charging && charge.AimTarget!=null && Vector2.Distance(charge.AimTarget.Position,position+offset)<=extent.magnitude;
                Vector2 point=position, speed=velocity, enemy=charge.Body.position, direction=charge.TravelDirection;
                float risk=0, radius=Mathf.Max(body.Radius,charge.Config.ContactSweepRadius)+extent.magnitude+Config.SafetyPadding;
                // 两个有限阶段分别积分，避免浮点累计在锁向时刻产生极小步长。
                for(int phase=0;phase<2;phase++)
                {
                    float seconds=phase==0?delay:duration;
                    if(seconds<=0) continue;
                    if(phase==1 && follows) direction=(point+offset-enemy).normalized;
                    int steps=Mathf.CeilToInt(seconds/Time.fixedDeltaTime);
                    float dt=seconds/steps;
                    Vector2 enemySpeed=phase==1?direction*charge.Config.DashSpeed:Vector2.zero;
                    for(int step=0;step<steps;step++)
                    {
                        Vector2 previous=point;
                        PlayerMovementStep.Calculate(ref point,ref speed,input,motor,extent,offset,dt);
                        point+=speed*dt;
                        risk=Mathf.Max(risk,CompanionThreatMath.Risk(enemy-previous-offset,enemySpeed-speed,radius,dt));
                        enemy+=enemySpeed*dt;
                    }
                }
                total+=risk;
            }
            return total;
        }

        /// <summary>预判蓄力结束后的直线扫掠；被追踪者不能把蓄力中平移误当成已躲开锁线。</summary>
        public float ChargeDanger(Vector2 position, Vector2 velocity, float ownerRadius)
        {
            float danger=0;
            for (int i=0;i<_count;i++)
            {
                var body=_visible[i];
                if(body==null || !body.IsObservable || body.DownloadCharge==null || !body.DownloadCharge.IsRunning) continue;
                var charge=body.DownloadCharge;
                bool charging=charge.State==DownloadChargeState.Charging;
                if (!charging && charge.State!=DownloadChargeState.Dashing) continue;
                float delay=charging?charge.StateRemaining:0;
                Vector2 future=position+velocity*delay;
                Vector2 direction=charge.TravelDirection;
                // 只有正被迅雷追踪的角色预测锁向更新；另一角色按可见预警方向避让。
                if(charging && charge.AimTarget!=null && Vector2.Distance(charge.AimTarget.Position,position)<=ownerRadius)
                    direction=(future-(Vector2)charge.Body.position).normalized;
                float radius=Mathf.Max(body.Radius,charge.Config.ContactSweepRadius)+ownerRadius+Config.SafetyPadding;
                float horizon=Mathf.Min(charging?charge.Config.DashSeconds:charge.StateRemaining,Config.PredictionSeconds);
                if(horizon<=0) continue;
                float dashRisk=CompanionThreatMath.Risk((Vector2)charge.Body.position-future,
                    direction*charge.Config.DashSpeed-velocity,radius,horizon);
                float approachRisk=charging && delay>0 ? CompanionThreatMath.Risk(
                    (Vector2)charge.Body.position-position,-velocity,radius,delay) : 0;
                // 不能为了避开未来的冲刺，先穿过仍在蓄力的另一只迅雷本体。
                danger+=Mathf.Max(dashRisk,approachRisk);
            }
            return danger;
        }

        public bool Initialize()
        {
            if (Registry == null || ObstacleRegistry == null || Config == null || !Config.IsValid) return false;
            _colliders = new Collider2D[Config.QueryCapacity];
            _visible = new CombatPerceptionBody2D[Config.QueryCapacity];
            Obstacles = new CompanionObstacleSnapshot[Config.ObstacleCapacity];
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(Config.PerceptionLayers);
            return true;
        }

        public void Refresh(Vector2 position, Vector2 ally)
        {
            _observerPosition = position;
            ObstacleCount = ObstacleRegistry.CopyVisible(position, Config.PerceptionRadius,
                Obstacles, out bool obstacleSaturated);
            ObstacleSaturated = obstacleSaturated;
            int hits = Physics2D.OverlapCircle(position, Config.PerceptionRadius, _filter, _colliders);
            Saturated = hits == _colliders.Length;
            _count = 0;
            NearbyCount = 0;
            var previous = Target;
            Target = null;
            TargetScore = float.NegativeInfinity;
            for (int i = 0; i < hits; i++)
            {
                if (!Registry.TryResolve(_colliders[i], out var body) || !body.IsObservable) continue;
                // 气泡默认只参与避障，不进入普通锁定、敌群数量或近战威胁评分。
                if (!body.ThreatTrackedAsObstacle) _visible[_count++] = body;
                Vector2 bodyPosition = body.Position;
                float distance = Vector2.Distance(position, bodyPosition);
                if (!body.ThreatTrackedAsObstacle && !body.PassiveAttackTarget && distance <= Config.NearbyRadius) NearbyCount++;
                // 能量弹同时登记为避障圆与可击破目标；不能套用气泡的默认忽略规则。
                if (!body.IsAttackTarget || (body.ThreatTrackedAsObstacle && MechanicPriority(body) <= 0) ||
                    distance > Config.AttackRange || !CanAttack(body, position)) continue;
                float nearEither = Mathf.Min(distance, Vector2.Distance(ally, bodyPosition));
                float wounded = body.IsEnemy
                    ? 1f - body.Enemy.Health.CurrentHealth / body.Enemy.Health.MaximumHealth : 0f;
                float score = body.TargetValue + MechanicPriority(body) + (body.IsCharging ? Config.ChargingBonus : 0f) +
                    Config.NearbyThreatWeight * Mathf.Clamp01(1f - nearEither / Config.NearbyRadius) +
                    Config.WoundedBonus * wounded - Config.DistanceCost * distance +
                    (body == previous ? Config.TargetStickiness : 0f);
                if (score <= TargetScore) continue;
                Target = body;
                TargetScore = score;
            }
        }

        /// <summary>独立查询倒地队友周围，不被自己的感知圆截断；清场目标优先，复用查询缓冲。</summary>
        public bool PrioritizeRescueThreat(Vector2 position, Vector2 ally, float radius, float rescueBodyRadius)
        {
            int hits = Physics2D.OverlapCircle(ally, radius, _filter, _colliders);
            bool occupied = hits == _colliders.Length;
            float best = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                if (!Registry.TryResolve(_colliders[i], out var body) || !body.IsObservable || !body.IsAttackTarget || body.PassiveAttackTarget) continue;
                // Boss本体不能作为救人前必须击杀的杂兵；接触危险仍由Danger/招式预测避让。
                if (!body.IsEnemy && !body.ThreatTrackedAsObstacle) continue;
                // 只有实际压住救援点的气泡需要清除，不能为了救人清空周围整片迷宫。
                if (body.ThreatTrackedAsObstacle &&
                    Vector2.Distance(body.Shape.ClosestPoint(ally), ally) > rescueBodyRadius) continue;
                occupied = true;
                if (Vector2.Distance(position, body.Position) > Config.AttackRange) continue;
                float distance = (body.Position - ally).sqrMagnitude + (body.ThreatTrackedAsObstacle ? radius * radius : 0f);
                if (distance >= best) continue;
                best = distance;
                Target = body;
            }
            return occupied;
        }

        /// <summary>导航已确认无法绕行后，才选择前往目的地途中可触及的气泡。</summary>
        public void PrioritizeBlockedRoute(Vector2 position, Vector2 goal, float radius)
        {
            if (Target != null && (!Target.ThreatTrackedAsObstacle || HasMechanicTarget)) return;
            Vector2 delta = goal - position;
            int hits = Physics2D.OverlapCapsule((position + goal) * .5f,
                new Vector2(delta.magnitude + radius * 2, radius * 2), CapsuleDirection2D.Horizontal,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, _filter, _colliders);
            float nearest = Config.AttackRange * Config.AttackRange;
            for (int i = 0; i < hits; i++)
            {
                if (!Registry.TryResolve(_colliders[i], out var body) || !body.IsObservable ||
                    !body.IsAttackTarget || !body.ThreatTrackedAsObstacle) continue;
                float distance = (body.Position - position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                Target = body;
            }
        }

        public float Danger(Vector2 position, Vector2 velocity, float ownerRadius)
        {
            float danger = Saturated ? Config.EmergencyDanger : 0f;
            for (int i = 0; i < _count; i++)
            {
                var body = _visible[i];
                if (body == null || !body.IsObservable || body.PassiveAttackTarget) continue;
                // 仅复用本次循环体的 bounds；每次 Danger 仍重新读取活动状态与几何。
                Bounds bounds = body.Shape.bounds;
                danger += CompanionThreatMath.Risk((Vector2)bounds.center - position, body.Velocity - velocity,
                    ((Vector2)bounds.extents).magnitude + ownerRadius + Config.SafetyPadding, Config.PredictionSeconds);
            }
            if (Claude != null && Claude.Energy.State == ClaudeEnergyState.Flying)
                danger += CompanionThreatMath.Risk(Claude.Energy.Position - position,
                    Claude.Energy.Velocity - velocity, Claude.Energy.ExplosionRadius + ownerRadius,
                    Config.PredictionSeconds);
            if (Claude != null && Claude.SecondaryEnergy != null && Claude.SecondaryEnergy.State == ClaudeEnergyState.Flying)
                danger += CompanionThreatMath.Risk(Claude.SecondaryEnergy.Position - position,
                    Claude.SecondaryEnergy.Velocity - velocity, Claude.SecondaryEnergy.ExplosionRadius + ownerRadius,
                    Config.PredictionSeconds);
            return danger + ChargeDanger(position, velocity, ownerRadius);
        }

        public bool TryGetNearestThreat(Vector2 position, float range, out Vector2 point)
        {
            float best = range * range;
            bool found = false;
            point = position + Vector2.right;
            if (Target != null && Target.IsObservable && HasMechanicTarget && CanAttack(Target, position) &&
                (Target.Position - position).sqrMagnitude < best)
            { point = Target.Position; return true; }
            for (int i = 0; i < _count; i++)
            {
                var body = _visible[i];
                if (body == null || !body.IsObservable || !CanAttack(body, position)) continue;
                Vector2 bodyPosition = body.Position;
                float distance = (bodyPosition - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                point = bodyPosition;
                found = true;
            }
            // 仅允许导航/救援明确选中的拦路气泡触发近战，不对附近所有气泡挥刀。
            if (Target != null && Target.IsObservable && Target.ThreatTrackedAsObstacle && CanAttack(Target, position))
            {
                float distance = (Target.Position - position).sqrMagnitude;
                if (distance < best) { point = Target.Position; found = true; }
            }
            return found;
        }
    }
}
