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
                // 截束受击体以迎光表面结算，不能投影到球内部再返回内部点。
                Vector2 hitPoint = collider.ClosestPoint(hitbox.StopsPiercingBeams ? lane.End : samplePoint);
                if (hitbox.StopsPiercingBeams && collider is CircleCollider2D circle)
                {
                    Vector2 center = circle.transform.TransformPoint(circle.offset);
                    float radius = circle.radius * Mathf.Abs(circle.transform.lossyScale.x);
                    Vector2 offset = center - lane.Origin;
                    float along = Vector2.Dot(offset, lane.Direction);
                    float discriminant = radius * radius - (offset.sqrMagnitude - along * along);
                    if (discriminant >= 0 && along - Mathf.Sqrt(discriminant) >= 0)
                        hitPoint = lane.Origin + lane.Direction * (along - Mathf.Sqrt(discriminant));
                    else
                    {
                        Vector2 radial = lane.End - center;
                        hitPoint = center + (radial.sqrMagnitude > 0 ? radial.normalized : -lane.Direction) * radius;
                    }
                }

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
