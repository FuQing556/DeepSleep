using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>
    /// 池化敌方直线弹体。碰撞后提交伤害并把自身交还所属对象池。
    /// </summary>
    public sealed class EnemyProjectile2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private Collider2D _bodyCollider;
        [SerializeField] private bool _canBeCleared = true;

        private EnemyProjectilePool2D _ownerPool;
        private LayerMask _collisionLayers;
        private GameObject _damageSource;
        private float _damage;
        private float _remainingLifetimeSeconds;
        private ulong _attackId;

        public bool IsRented { get; private set; }
        public uint SpawnGeneration { get; private set; }

        public bool TryClear()
        {
            if (!IsRented || !_canBeCleared) return false;
            ReleaseToPool();
            return true;
        }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyProjectile2D)}] 弹体装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        internal void Simulate(float deltaTime)
        {
            if (!IsRented || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }

            _remainingLifetimeSeconds -= deltaTime;
            if (_remainingLifetimeSeconds <= 0f)
            {
                ReleaseToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsRented || other == null ||
                (_collisionLayers.value & (1 << other.gameObject.layer)) == 0 ||
                !other.TryGetComponent(out DamageHitbox2D hitbox) ||
                !hitbox.IsActiveTarget)
            {
                return;
            }

            Vector2 direction = _body.linearVelocity.sqrMagnitude > 0f
                ? _body.linearVelocity.normalized
                : (Vector2)transform.right;
            Vector2 hitPoint = other.ClosestPoint(_body.position);
            DamagePacket packet = new DamagePacket(
                _damage,
                hitPoint,
                direction,
                _damageSource,
                _attackId,
                DamageInterceptionPolicy.Blockable);
            hitbox.TryReceiveDamage(in packet);
            _ownerPool.NotifyImpact(hitPoint, transform.eulerAngles.z);
            ReleaseToPool();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_body == null || _body.bodyType != RigidbodyType2D.Kinematic)
            {
                reason = "必须配置 Kinematic Rigidbody2D。";
                return false;
            }

            if (_bodyCollider == null || !_bodyCollider.isTrigger)
            {
                reason = "必须配置触发器 Collider2D。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal void PrepareForPool()
        {
            IsRented = false;
            _ownerPool = null;
            _damageSource = null;
            _remainingLifetimeSeconds = 0f;
            _attackId = 0;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = false;
            gameObject.SetActive(false);
        }

        internal void Rent(
            EnemyProjectilePool2D ownerPool,
            Vector2 position,
            Vector2 direction,
            GameObject damageSource,
            EnemyProjectileConfig config)
        {
            _ownerPool = ownerPool;
            _collisionLayers = config.CollisionLayers;
            _damageSource = damageSource;
            _damage = config.Damage;
            _remainingLifetimeSeconds = config.LifetimeSeconds;
            _attackId = DamageAttackIdAllocator.Next();
            IsRented = true;
            SpawnGeneration++;

            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, 0f, angle));
            gameObject.SetActive(true);
            _body.position = position;
            _body.rotation = angle;
            _body.linearVelocity = direction * config.Speed;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = true;
        }

        internal void Return()
        {
            IsRented = false;
            _damageSource = null;
            _remainingLifetimeSeconds = 0f;
            _attackId = 0;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = false;
            _ownerPool = null;
            gameObject.SetActive(false);
        }

        private void ReleaseToPool()
        {
            EnemyProjectilePool2D owner = _ownerPool;
            if (owner != null)
            {
                owner.Return(this);
            }
        }
    }
}
