using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>HS 近战的一次真实伤害结算，供纯表现系统消费。</summary>
    public readonly struct HarnessMeleeDamageHitConfirmed
    {
        public HarnessMeleeDamageHitConfirmed(
            Vector2 hitPoint,
            Vector2 direction,
            float damageAmount,
            bool isWave,
            bool isSurface = false,
            bool useReceiverHitFeedback = false,
            Vector2? damageNumberPosition = null)
        {
            HitPoint = hitPoint;
            DamageNumberPosition = damageNumberPosition ?? hitPoint;
            Direction = direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector2.right;
            DamageAmount = damageAmount;
            IsWave = isWave;
            IsSurface = isSurface;
            UseReceiverHitFeedback = useReceiverHitFeedback;
        }

        public Vector2 HitPoint { get; }
        public Vector2 DamageNumberPosition { get; }
        public Vector2 Direction { get; }
        public float DamageAmount { get; }
        public bool IsWave { get; }
        public bool IsSurface { get; }
        public bool UseReceiverHitFeedback { get; }
    }
}
