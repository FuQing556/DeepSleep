using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams
{
    /// <summary>
    /// 对单条束线执行无阻挡区域查询，并按最终伤害接收者去重。
    /// 每次调用只处理一条束线，因此多束命中可以分别结算。
    /// </summary>
    public sealed class BeamHitResolver2D
    {
        private readonly List<Collider2D> _overlapResults = new();
        private readonly HashSet<IDamageReceiver> _seenReceivers = new();
        private readonly ContactFilter2D _contactFilter = new()
        {
            useTriggers = true,
        };

        public int Resolve(
            in BeamLaneSnapshot lane,
            LayerMask targetLayers,
            IDamageReceiver primaryReceiver,
            List<BeamResolvedHit2D> resolvedHits)
        {
            if (!lane.IsValid ||
                targetLayers.value == 0 ||
                resolvedHits == null)
            {
                return 0;
            }

            resolvedHits.Clear();
            _overlapResults.Clear();
            _seenReceivers.Clear();

            ContactFilter2D filter = _contactFilter;
            filter.SetLayerMask(targetLayers);

            Physics2D.OverlapBox(
                lane.Center,
                new Vector2(lane.Length, lane.Width),
                lane.RotationDegrees,
                filter,
                _overlapResults);

            for (int index = 0; index < _overlapResults.Count; index++)
            {
                Collider2D collider = _overlapResults[index];

                if (collider == null ||
                    !collider.TryGetComponent(out DamageHitbox2D hitbox) ||
                    !hitbox.CanReceiveDamage ||
                    !hitbox.TryGetReceiver(out IDamageReceiver receiver) ||
                    !_seenReceivers.Add(receiver))
                {
                    continue;
                }

                Vector2 samplePoint = ClosestPointOnSegment(
                    collider.bounds.center,
                    lane.Origin,
                    lane.End);
                Vector2 hitPoint = collider.ClosestPoint(samplePoint);

                resolvedHits.Add(new BeamResolvedHit2D(
                    hitbox,
                    hitPoint,
                    ReferenceEquals(receiver, primaryReceiver)));
            }

            return resolvedHits.Count;
        }

        private static Vector2 ClosestPointOnSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;

            if (lengthSquared <= Mathf.Epsilon)
            {
                return start;
            }

            float progress = Mathf.Clamp01(
                Vector2.Dot(point - start, segment) / lengthSquared);
            return start + segment * progress;
        }
    }
}
