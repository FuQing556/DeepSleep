using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Targeting
{
    /// <summary>
    /// 在预分配的 2D 物理查询结果中解析玩家明确选择的目标。
    /// 不处理朝向、技能消耗或目标锁定生命周期。
    /// </summary>
    internal sealed class ManualTargetFinder2D
    {
        private readonly Collider2D[] _candidates;
        private readonly ContactFilter2D _targetFilter;

        public ManualTargetFinder2D(
            LayerMask targetLayers,
            int maximumCandidates)
        {
            _candidates = new Collider2D[maximumCandidates];

            ContactFilter2D targetFilter = new ContactFilter2D();
            targetFilter.SetLayerMask(targetLayers);
            targetFilter.useTriggers = true;
            _targetFilter = targetFilter;
        }

        public bool TryFindNearPoint(
            Vector2 selectionPoint,
            Vector2 ownerOrigin,
            float selectionRadius,
            float maximumLockDistance,
            out Collider2D target)
        {
            return TryFindNearPoint(
                selectionPoint,
                ownerOrigin,
                selectionRadius,
                maximumLockDistance,
                null,
                out target);
        }

        public bool TryFindNearPoint(
            Vector2 selectionPoint,
            Vector2 ownerOrigin,
            float selectionRadius,
            float maximumLockDistance,
            Predicate<Collider2D> isEligible,
            out Collider2D target,
            Func<Collider2D, int> priority = null)
        {
            int candidateCount = Physics2D.OverlapCircle(
                selectionPoint,
                selectionRadius,
                _targetFilter,
                _candidates);

            Collider2D bestTarget = null;
            float bestDistanceSquared = float.PositiveInfinity;
            float bestCenterDistance = float.PositiveInfinity;
            int bestPriority = int.MinValue;
            bool bestExact = false;
            float maximumDistanceSquared =
                maximumLockDistance * maximumLockDistance;

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidate = _candidates[index];

                if (!IsUsable(candidate) ||
                    (isEligible != null && !isEligible(candidate)) ||
                    ((Vector2)candidate.bounds.center - ownerOrigin)
                    .sqrMagnitude > maximumDistanceSquared)
                {
                    continue;
                }

                Vector2 closestPoint = candidate.ClosestPoint(selectionPoint);
                float distanceSquared =
                    (closestPoint - selectionPoint).sqrMagnitude;

                bool exact = candidate.OverlapPoint(selectionPoint);
                int rank = priority != null ? priority(candidate) : 0;
                float centerDistance = ((Vector2)candidate.bounds.center - selectionPoint).sqrMagnitude;
                // 精确点中优先于辅助抢目标；同档按点击距离、中心距离和稳定实体身份排序。
                if (bestTarget != null)
                {
                    if (bestExact && !exact) continue;
                    if (bestExact == exact)
                    {
                        if (!exact && rank < bestPriority) continue;
                        if (exact || rank == bestPriority)
                        {
                            if (distanceSquared > bestDistanceSquared) continue;
                            if (Mathf.Approximately(distanceSquared, bestDistanceSquared) &&
                                (centerDistance > bestCenterDistance ||
                                 (Mathf.Approximately(centerDistance, bestCenterDistance) && candidate.GetEntityId().CompareTo(bestTarget.GetEntityId()) >= 0))) continue;
                        }
                    }
                }

                bestTarget = candidate;
                bestDistanceSquared = distanceSquared;
                bestCenterDistance = centerDistance; bestPriority = rank; bestExact = exact;
            }

            target = bestTarget;
            return target != null;
        }

        public bool TryFindInDirection(
            Vector2 ownerOrigin,
            Vector2 direction,
            float maximumLockDistance,
            float minimumDirectionDot,
            out Collider2D target)
        {
            return TryFindInDirection(
                ownerOrigin,
                direction,
                maximumLockDistance,
                minimumDirectionDot,
                null,
                out target);
        }

        public bool TryFindInDirection(
            Vector2 ownerOrigin,
            Vector2 direction,
            float maximumLockDistance,
            float minimumDirectionDot,
            Predicate<Collider2D> isEligible,
            out Collider2D target)
        {
            target = null;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            direction.Normalize();

            int candidateCount = Physics2D.OverlapCircle(
                ownerOrigin,
                maximumLockDistance,
                _targetFilter,
                _candidates);

            float bestDirectionDot = minimumDirectionDot;
            float bestDistanceSquared = float.PositiveInfinity;

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidate = _candidates[index];

                if (!IsUsable(candidate) ||
                    (isEligible != null && !isEligible(candidate)))
                {
                    continue;
                }

                Vector2 offset =
                    (Vector2)candidate.bounds.center - ownerOrigin;

                if (offset.sqrMagnitude <= Mathf.Epsilon)
                {
                    continue;
                }

                float directionDot =
                    Vector2.Dot(direction, offset.normalized);

                if (directionDot < bestDirectionDot ||
                    (Mathf.Approximately(directionDot, bestDirectionDot) &&
                     offset.sqrMagnitude >= bestDistanceSquared))
                {
                    continue;
                }

                target = candidate;
                bestDirectionDot = directionDot;
                bestDistanceSquared = offset.sqrMagnitude;
            }

            return target != null;
        }

        private static bool IsUsable(Collider2D candidate)
        {
            return candidate != null &&
                   candidate.isActiveAndEnabled &&
                   candidate.gameObject.activeInHierarchy;
        }
    }
}
