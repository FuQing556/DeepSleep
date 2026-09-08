using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>
    /// 敌方直线弹体的共享数值。敌种只决定何时、向哪里发射。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_EnemyProjectile_",
        menuName = "DeepSleep/Combat/Enemy Projectile Config")]
    public sealed class EnemyProjectileConfig : ScriptableObject
    {
        [Header("弹体")]
        [SerializeField, Min(0.01f)] private float _speed = 5f;
        [SerializeField, Min(0.01f)] private float _lifetimeSeconds = 4f;
        [SerializeField, Min(0.01f)] private float _damage = 1f;
        [SerializeField] private LayerMask _collisionLayers;

        [Header("对象池")]
        [SerializeField, Min(1)] private int _initialPoolSize = 12;
        [SerializeField, Min(1)] private int _maximumPoolSize = 32;

        public float Speed => _speed;
        public float LifetimeSeconds => _lifetimeSeconds;
        public float Damage => _damage;
        public LayerMask CollisionLayers => _collisionLayers;
        public int InitialPoolSize => _initialPoolSize;
        public int MaximumPoolSize => _maximumPoolSize;

        public bool TryValidate(out string reason)
        {
            if (_speed <= 0f || _lifetimeSeconds <= 0f || _damage <= 0f)
            {
                reason = "弹速、寿命和伤害必须大于 0。";
                return false;
            }

            if (_collisionLayers.value == 0)
            {
                reason = "弹体碰撞图层不能为空。";
                return false;
            }

            if (_initialPoolSize < 1 || _maximumPoolSize < _initialPoolSize)
            {
                reason = "对象池上限不得小于预热数量。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
