using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 刷怪端在出生时冻结的运动随机结果。
    /// 网络端只同步此结果，不在每台设备上重复抽签。
    /// </summary>
    public readonly struct EnemySpawnVariation2D
    {
        public EnemySpawnVariation2D(
            Vector2 travelDirection,
            float travelSpeed,
            float initialRotationDegrees,
            float angularVelocityDegreesPerSecond)
        {
            TravelDirection = travelDirection.normalized;
            TravelSpeed = travelSpeed;
            InitialRotationDegrees = initialRotationDegrees;
            AngularVelocityDegreesPerSecond =
                angularVelocityDegreesPerSecond;
        }

        public Vector2 TravelDirection { get; }
        public float TravelSpeed { get; }
        public float InitialRotationDegrees { get; }
        public float AngularVelocityDegreesPerSecond { get; }

        public bool IsValid =>
            Mathf.Abs(TravelDirection.x) > 0.001f &&
            TravelSpeed > 0f;
    }
}
