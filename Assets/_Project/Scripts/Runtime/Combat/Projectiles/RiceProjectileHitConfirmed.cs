using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>
    /// 一枚饭团对一个伤害接收者成功结算后发布的只读事实。
    /// 表现层可以消费它，但不能据此再次造成伤害。
    /// </summary>
    public readonly struct RiceProjectileHitConfirmed
    {
        public RiceProjectileHitConfirmed(
            DamageHitbox2D hitbox,
            Vector2 hitPoint,
            Vector2 direction,
            float damageAmount)
        {
            Hitbox = hitbox;
            HitPoint = hitPoint;
            Direction = direction.normalized;
            DamageAmount = damageAmount;
        }

        public DamageHitbox2D Hitbox { get; }
        public Vector2 HitPoint { get; }
        public Vector2 Direction { get; }
        public float DamageAmount { get; }

        public bool IsValid =>
            Hitbox != null &&
            Direction.sqrMagnitude > 0f &&
            DamageAmount > 0f;
    }
}
