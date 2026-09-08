using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 敌人离场瞬间的不可变快照。表现层读取快照，
    /// 不持有即将被对象池复用的敌人引用。
    /// </summary>
    public readonly struct EnemyDespawnRequest2D
    {
        public EnemyDespawnRequest2D(
            EnemyDespawnReason reason,
            Vector2 effectPosition,
            float worldRotationDegrees,
            Vector2 travelDirection)
        {
            Reason = reason;
            EffectPosition = effectPosition;
            WorldRotationDegrees = worldRotationDegrees;
            TravelDirection = travelDirection;
        }

        public EnemyDespawnReason Reason { get; }
        public Vector2 EffectPosition { get; }
        public float WorldRotationDegrees { get; }
        public Vector2 TravelDirection { get; }
    }
}
