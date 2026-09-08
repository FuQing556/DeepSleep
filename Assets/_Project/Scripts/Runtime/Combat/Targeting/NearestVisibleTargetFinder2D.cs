using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Targeting
{
    /// <summary>
    /// 使用无分配 2D 物理查询寻找最近且未被障碍遮挡的目标。
    /// 首版只按距离排序；威胁和标记权重将在目标契约建立后扩展。
    /// </summary>
    internal sealed class NearestVisibleTargetFinder2D
    {
        private readonly DeepSeekRiceWeaponConfig _config;
        private readonly Collider2D[] _targetCandidates;
        private readonly RaycastHit2D[] _linecastHits = new RaycastHit2D[1];
        private readonly ContactFilter2D _targetFilter;
        private readonly ContactFilter2D _obstacleFilter;

        public NearestVisibleTargetFinder2D(
            DeepSeekRiceWeaponConfig config)
        {
            _config = config;
            _targetCandidates =
                new Collider2D[config.MaximumTargetCandidates];

            ContactFilter2D targetFilter = new ContactFilter2D();
            targetFilter.SetLayerMask(config.TargetLayers);
            targetFilter.useTriggers = true;
            _targetFilter = targetFilter;

            ContactFilter2D obstacleFilter = new ContactFilter2D();
            obstacleFilter.SetLayerMask(config.ObstacleLayers);
            obstacleFilter.useTriggers = false;
            _obstacleFilter = obstacleFilter;
        }

        public bool TryFind(
            Vector2 origin,
            Vector2 forward,
            out Vector2 targetPosition)
        {
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                targetPosition = default;
                return false;
            }

            forward.Normalize();

            int candidateCount = Physics2D.OverlapCircle(
                origin,
                _config.TargetSearchRadius,
                _targetFilter,
                _targetCandidates);

            Collider2D nearestTarget = null;
            Vector2 nearestPosition = default;
            float nearestDistanceSquared = float.PositiveInfinity;

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidate = _targetCandidates[index];

                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                Vector2 candidatePosition = candidate.bounds.center;
                Vector2 directionToCandidate = candidatePosition - origin;

                if (directionToCandidate.sqrMagnitude <= Mathf.Epsilon ||
                    Vector2.Dot(
                        forward,
                        directionToCandidate.normalized) <=
                    _config.MinimumForwardDot)
                {
                    continue;
                }

                if (IsBlocked(origin, candidatePosition))
                {
                    continue;
                }

                float distanceSquared =
                    (candidatePosition - origin).sqrMagnitude;

                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestTarget = candidate;
                nearestPosition = candidatePosition;
                nearestDistanceSquared = distanceSquared;
            }

            targetPosition = nearestPosition;
            return nearestTarget != null;
        }

        private bool IsBlocked(Vector2 origin, Vector2 targetPosition)
        {
            if (_config.ObstacleLayers.value == 0)
            {
                return false;
            }

            return Physics2D.Linecast(
                origin,
                targetPosition,
                _obstacleFilter,
                _linecastHits) > 0;
        }
    }
}
