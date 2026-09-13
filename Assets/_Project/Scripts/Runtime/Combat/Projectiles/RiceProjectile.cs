using DeepSleep.Runtime.Combat.Damage;
using System.Collections.Generic;
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
        private bool _impactConsumed;
        private float _splashRadius;
        private float _splashDamageRatio;
        private LayerMask _splashLayers;
        private readonly List<Collider2D> _splashCandidates = new(32);
        private readonly HashSet<IDamageReceiver> _splashReceivers = new();

        public bool IsRented => _isRented;
        public uint SpawnGeneration { get; private set; }

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
            if (!_isRented || _impactConsumed ||
                (_collisionLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            _impactConsumed = true;
            _bodyCollider.enabled = false;
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
            GameObject damageSource,
            float splashRadius,
            float splashDamageRatio,
            LayerMask splashLayers)
        {
            _ownerPool = ownerPool;
            _collisionLayers = collisionLayers;
            _remainingLifetimeSeconds = lifetimeSeconds;
            _damageAmount = damageAmount;
            _damageSource = damageSource;
            _isRented = true;
            _impactConsumed = false;
            _splashRadius = splashRadius;
            _splashDamageRatio = splashDamageRatio;
            _splashLayers = splashLayers;
            SpawnGeneration++;

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
            _splashRadius = 0f;
            _splashCandidates.Clear();
            _splashReceivers.Clear();
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

            // 在直击可能令敌人回池前冻结爆心及候选，按接收者去重。
            _splashReceivers.Clear();
            if (hitbox.TryGetReceiver(out var directReceiver))
                _splashReceivers.Add(directReceiver);
            if (_splashRadius > 0f)
                Physics2D.OverlapCircle(hitPoint, _splashRadius,
                    new ContactFilter2D { useLayerMask = true, layerMask = _splashLayers, useTriggers = true },
                    _splashCandidates);

            if (!hitbox.TryReceiveDamage(in damage))
            {
                return;
            }

            hitbox.ApplyHitMotion(HitMotionKind.DeepSeekSlow);

            _ownerPool?.NotifyHitConfirmed(
                new RiceProjectileHitConfirmed(
                    hitbox,
                    hitPoint,
                    direction,
                    damage.Amount));

            if (_splashRadius <= 0f) return;
            _ownerPool?.NotifySplash(hitPoint, direction);
            foreach (var candidate in _splashCandidates)
            {
                if (candidate == null || !candidate.TryGetComponent(out DamageHitbox2D splashHit) ||
                    !splashHit.CanReceiveDamage || !splashHit.TryGetReceiver(out var receiver) ||
                    !_splashReceivers.Add(receiver)) continue;
                Vector2 point = candidate.ClosestPoint(hitPoint);
                Vector2 outward = (Vector2)candidate.bounds.center - hitPoint;
                if (outward.sqrMagnitude < 0.0001f) outward = direction;
                var splashDamage = new DamagePacket(_damageAmount * _splashDamageRatio,
                    point, outward.normalized, _damageSource);
                if (splashHit.TryReceiveDamage(in splashDamage))
                {
                    splashHit.ApplyHitMotion(HitMotionKind.DeepSeekSlow);
                    _ownerPool?.NotifyHitConfirmed(new RiceProjectileHitConfirmed(
                        splashHit, point, outward.normalized, splashDamage.Amount));
                }
            }
        }
    }
}
