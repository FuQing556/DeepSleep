using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>纯数学风险估计，可单独测试。不是命中裁决，实际伤害仍由物理系统决定。</summary>
    public static class CompanionThreatMath
    {
        public static float Risk(Vector2 offset, Vector2 relativeVelocity, float radius, float horizon)
        {
            // 静止物体不可除以零；epsilon 仅为数值安全，不是玩法距离。
            float speedSquared = relativeVelocity.sqrMagnitude;
            float time = speedSquared > 0.000001f
                ? Mathf.Clamp(-Vector2.Dot(offset, relativeVelocity) / speedSquared, 0f, horizon) : 0f;
            float separation = (offset + relativeVelocity * time).magnitude;
            if (separation >= radius) return 0f;
            return (1f - separation / Mathf.Max(radius, 0.000001f)) * (1f - time / (2f * horizon));
        }

        public static Vector2 ClampPoint(Vector2 point, Rect bounds, Vector2 extent) => new(
            Mathf.Clamp(point.x, bounds.xMin + extent.x, bounds.xMax - extent.x),
            Mathf.Clamp(point.y, bounds.yMin + extent.y, bounds.yMax - extent.y));
    }
}
