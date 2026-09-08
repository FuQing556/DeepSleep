using UnityEngine;

namespace DeepSleep.Runtime.World.Playfield
{
    /// <summary>
    /// 逻辑战斗区域的纯几何算法，不读取相机、分辨率或场景对象。
    /// </summary>
    public static class CombatPlayfieldMath
    {
        // 仅用于识别接近零的方向分量，属于浮点安全阈值而非玩法数据。
        private const float DirectionEpsilon = 0.000001f;

        public static bool TryGetRayExitDistance(
            Rect bounds,
            Vector2 origin,
            Vector2 direction,
            out float distance)
        {
            distance = 0f;

            if (bounds.width <= 0f ||
                bounds.height <= 0f ||
                !IsFinite(origin) ||
                !IsFinite(direction) ||
                direction.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            if (origin.x < bounds.xMin - DirectionEpsilon ||
                origin.x > bounds.xMax + DirectionEpsilon ||
                origin.y < bounds.yMin - DirectionEpsilon ||
                origin.y > bounds.yMax + DirectionEpsilon)
            {
                return false;
            }

            Vector2 normalizedDirection = direction.normalized;
            float horizontalDistance = GetAxisExitDistance(
                origin.x,
                normalizedDirection.x,
                bounds.xMin,
                bounds.xMax);
            float verticalDistance = GetAxisExitDistance(
                origin.y,
                normalizedDirection.y,
                bounds.yMin,
                bounds.yMax);

            distance = Mathf.Min(horizontalDistance, verticalDistance);
            return !float.IsNaN(distance) &&
                   !float.IsInfinity(distance) &&
                   distance > DirectionEpsilon;
        }

        private static float GetAxisExitDistance(
            float origin,
            float direction,
            float minimum,
            float maximum)
        {
            if (direction > DirectionEpsilon)
            {
                return (maximum - origin) / direction;
            }

            if (direction < -DirectionEpsilon)
            {
                return (minimum - origin) / direction;
            }

            return float.PositiveInfinity;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y);
        }
    }
}
