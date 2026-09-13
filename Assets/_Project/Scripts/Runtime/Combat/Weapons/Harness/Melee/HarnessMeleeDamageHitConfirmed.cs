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
            bool isWave)
        {
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector2.right;
            DamageAmount = damageAmount;
            IsWave = isWave;
        }

        public Vector2 HitPoint { get; }
        public Vector2 Direction { get; }
        public float DamageAmount { get; }
        public bool IsWave { get; }
    }
}
