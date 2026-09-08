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
            out Collider2D target)
        {
            int candidateCount = Physics2D.OverlapCircle(
                selectionPoint,
                selectionRadius,
                _targetFilter,
                _candidates);

            Collider2D bestTarget = null;
            float bestDistanceSquared = float.PositiveInfinity;
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

                if (distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                bestTarget = candidate;
                bestDistanceSquared = distanceSquared;
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
