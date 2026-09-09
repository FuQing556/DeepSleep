using DeepSleep.Runtime.Combat.Perception;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>有界局部快照及目标评分。每次评估复用缓冲，满缓冲按不安全处理。</summary>
    public sealed class CompanionBattleSensor2D : MonoBehaviour
    {
        public CombatPerceptionRegistry2D Registry;
        public CompanionTacticsConfig Config;
        private Collider2D[] _colliders;
        private CombatPerceptionBody2D[] _visible;
        private ContactFilter2D _filter;
        private int _count;
        public bool Saturated { get; private set; }
        public CombatPerceptionBody2D Target { get; private set; }
        public float TargetScore { get; private set; }
        public int NearbyCount { get; private set; }

        public bool Initialize()
        {
            if (Registry == null || Config == null || !Config.IsValid) return false;
            _colliders = new Collider2D[Config.QueryCapacity];
            _visible = new CombatPerceptionBody2D[Config.QueryCapacity];
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(Config.PerceptionLayers);
            return true;
        }

        public void Refresh(Vector2 position, Vector2 ally)
        {
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
                _visible[_count++] = body;
                float distance = Vector2.Distance(position, body.Position);
                if (distance <= Config.NearbyRadius) NearbyCount++;
                if (!body.IsEnemy || distance > Config.AttackRange) continue;
                float nearEither = Mathf.Min(distance, Vector2.Distance(ally, body.Position));
                float wounded = 1f - body.Enemy.Health.CurrentHealth / body.Enemy.Health.MaximumHealth;
                float score = body.TargetValue + (body.IsCharging ? Config.ChargingBonus : 0f) +
                    Config.NearbyThreatWeight * Mathf.Clamp01(1f - nearEither / Config.NearbyRadius) +
                    Config.WoundedBonus * wounded - Config.DistanceCost * distance +
                    (body == previous ? Config.TargetStickiness : 0f);
                if (score <= TargetScore) continue;
                Target = body;
                TargetScore = score;
            }
        }

        public float Danger(Vector2 position, Vector2 velocity, float ownerRadius)
        {
            float danger = Saturated ? Config.EmergencyDanger : 0f;
            for (int i = 0; i < _count; i++)
            {
                var body = _visible[i];
                if (body == null || !body.IsObservable) continue;
                danger += CompanionThreatMath.Risk(body.Position - position, body.Velocity - velocity,
                    body.Radius + ownerRadius + Config.SafetyPadding, Config.PredictionSeconds);
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
                float distance = (body.Position - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                point = body.Position;
                found = true;
            }
            return found;
        }
    }
}
