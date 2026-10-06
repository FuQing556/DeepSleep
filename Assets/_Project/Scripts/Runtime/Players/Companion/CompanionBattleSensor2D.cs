using DeepSleep.Runtime.Combat.Perception;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>有界局部快照及目标评分。每次评估复用缓冲，满缓冲按不安全处理。</summary>
    public sealed class CompanionBattleSensor2D : MonoBehaviour
    {
        public CombatPerceptionRegistry2D Registry;
        public CompanionObstacleRegistry2D ObstacleRegistry;
        public CompanionTacticsConfig Config;
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
                if (!body.ThreatTrackedAsObstacle && distance <= Config.NearbyRadius) NearbyCount++;
                if (!body.IsAttackTarget || body.ThreatTrackedAsObstacle || distance > Config.AttackRange) continue;
                float nearEither = Mathf.Min(distance, Vector2.Distance(ally, bodyPosition));
                float wounded = body.IsEnemy
                    ? 1f - body.Enemy.Health.CurrentHealth / body.Enemy.Health.MaximumHealth : 0f;
                float score = body.TargetValue + (body.IsCharging ? Config.ChargingBonus : 0f) +
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
                if (!Registry.TryResolve(_colliders[i], out var body) || !body.IsObservable || !body.IsAttackTarget) continue;
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
            if (Target != null && !Target.ThreatTrackedAsObstacle) return;
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
                if (body == null || !body.IsObservable) continue;
                // 仅复用本次循环体的 bounds；每次 Danger 仍重新读取活动状态与几何。
                Bounds bounds = body.Shape.bounds;
                danger += CompanionThreatMath.Risk((Vector2)bounds.center - position, body.Velocity - velocity,
                    ((Vector2)bounds.extents).magnitude + ownerRadius + Config.SafetyPadding, Config.PredictionSeconds);
            }
            return danger;
        }

        public bool TryGetNearestThreat(Vector2 position, float range, out Vector2 point)
        {
            float best = range * range;
            bool found = false;
            point = position + Vector2.right;
            for (int i = 0; i < _count; i++)
            {
                var body = _visible[i];
                if (body == null || !body.IsObservable) continue;
                Vector2 bodyPosition = body.Position;
                float distance = (bodyPosition - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                point = bodyPosition;
                found = true;
            }
            // 仅允许导航/救援明确选中的拦路气泡触发近战，不对附近所有气泡挥刀。
            if (Target != null && Target.IsObservable && Target.ThreatTrackedAsObstacle)
            {
                float distance = (Target.Position - position).sqrMagnitude;
                if (distance < best) { point = Target.Position; found = true; }
            }
            return found;
        }
    }
}
