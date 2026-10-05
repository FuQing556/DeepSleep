using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>
    /// AI 当前可观察的接触危险。只包含实际几何与运动，不包含生成器的预留路线。
    /// </summary>
    public readonly struct CompanionObstacleSnapshot
    {
        public CompanionObstacleSnapshot(Vector2 center, float radius, Vector2 velocity)
        {
            Center = center;
            Radius = radius;
            Velocity = velocity;
        }

        public Vector2 Center { get; }
        public float Radius { get; }
        public Vector2 Velocity { get; }
    }
}
