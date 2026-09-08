using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>
    /// 池化饭团弹体。负责飞行、寿命、单次碰撞伤害与命中事实上报。
    /// </summary>
    public sealed class RiceProjectile : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private Collider2D _bodyCollider;

        private RiceProjectilePool _ownerPool;
        private LayerMask _collisionLayers;
        private GameObject _damageSource;
        private float _damageAmount;
        private float _remainingLifetimeSeconds;
        private bool _isRented;

        public bool IsRented => _isRented;

        private void Awake()
        {
            if (_body == null || _bodyCollider == null)
            {
                Debug.LogError(
                    $"[{nameof(RiceProjectile)}] " +
                    "必须显式配置 Rigidbody2D 与 Collider2D。",
                    this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!_isRented)
            {
                return;
            }

            _remainingLifetimeSeconds -= Time.fixedDeltaTime;

            if (_remainingLifetimeSeconds <= 0f)
            {
                ReleaseToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isRented ||
                (_collisionLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            ApplyDamageIfPossible(other);
            ReleaseToPool();
        }

        internal void PrepareForPool()
        {
            _isRented = false;
            _ownerPool = null;
            _remainingLifetimeSeconds = 0f;
            _collisionLayers = default;
            _damageSource = null;
            _damageAmount = 0f;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = false;
            gameObject.SetActive(false);
        }

        internal void OnRent(
            RiceProjectilePool ownerPool,
            Vector2 position,
            Vector2 direction,
            float speed,
            float lifetimeSeconds,
            LayerMask collisionLayers,
            float damageAmount,
            GameObject damageSource)
        {
            _ownerPool = ownerPool;
            _collisionLayers = collisionLayers;
            _remainingLifetimeSeconds = lifetimeSeconds;
            _damageAmount = damageAmount;
            _damageSource = damageSource;
            _isRented = true;

            float rotationDegrees = Mathf.Atan2(
                direction.y,
                direction.x) * Mathf.Rad2Deg;

            transform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, 0f, rotationDegrees));

            gameObject.SetActive(true);
            _body.position = position;
            _body.rotation = rotationDegrees;
            _body.linearVelocity = direction * speed;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = true;
        }

        internal void OnReturn()
        {
            _isRented = false;
            _remainingLifetimeSeconds = 0f;
            _collisionLayers = default;
            _damageSource = null;
            _damageAmount = 0f;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = false;
            _ownerPool = null;
            gameObject.SetActive(false);
        }

        private void ReleaseToPool()
        {
            RiceProjectilePool ownerPool = _ownerPool;

            if (ownerPool != null)
            {
                ownerPool.Return(this);
            }
        }

        private void ApplyDamageIfPossible(Collider2D other)
        {
            if (!other.TryGetComponent(out DamageHitbox2D hitbox))
            {
                return;
            }

            Vector2 direction = _body.linearVelocity.sqrMagnitude > 0f
                ? _body.linearVelocity.normalized
                : transform.right;
            Vector2 hitPoint = other.ClosestPoint(_body.position);
            DamagePacket damage = new DamagePacket(
                _damageAmount,
                hitPoint,
                direction,
                _damageSource);

            if (!hitbox.TryReceiveDamage(in damage))
            {
                return;
            }

            _ownerPool?.NotifyHitConfirmed(
                new RiceProjectileHitConfirmed(
                    hitbox,
                    hitPoint,
                    direction,
                    _damageAmount));
        }
    }
}
