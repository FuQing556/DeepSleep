using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// 一条激光对一个伤害接收者成功结算后发布的只读事实。
    /// 表现层只能消费它，不能据此再次造成伤害。
    /// </summary>
    public readonly struct HarnessTerminalLaserHitConfirmed
    {
        public HarnessTerminalLaserHitConfirmed(
            int laneIndex,
            DamageHitbox2D hitbox,
            Vector2 hitPoint,
            Vector2 direction,
            float beamWidth,
            float damageAmount,
            bool isPrimaryTarget)
        {
            LaneIndex = laneIndex;
            Hitbox = hitbox;
            HitPoint = hitPoint;
            Direction = direction.normalized;
            BeamWidth = beamWidth;
            DamageAmount = damageAmount;
            IsPrimaryTarget = isPrimaryTarget;
        }

        public int LaneIndex { get; }
        public DamageHitbox2D Hitbox { get; }
        public Vector2 HitPoint { get; }
        public Vector2 Direction { get; }
        public float BeamWidth { get; }
        public float DamageAmount { get; }
        public bool IsPrimaryTarget { get; }

        public bool IsValid =>
            LaneIndex >= 0 &&
            Hitbox != null &&
            Direction.sqrMagnitude > 0f &&
            BeamWidth > 0f &&
            DamageAmount > 0f;
    }
}
