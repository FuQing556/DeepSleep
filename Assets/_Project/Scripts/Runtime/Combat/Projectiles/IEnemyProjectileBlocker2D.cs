using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>可在不承受敌方伤害的前提下拦截敌方弹体。</summary>
    public interface IEnemyProjectileBlocker2D
    {
        bool TryBlockEnemyProjectile(
            EnemyProjectile2D projectile,
            Vector2 hitPoint);
    }
}
