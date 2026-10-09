using System;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 共用接触攻击：配置决定攻击后回收或持续存活，重复攻击由所属固定步驱动冷却。
    /// </summary>
    public sealed class EnemyContactAttack2D : MonoBehaviour, DeepSleep.Runtime.Simulation.IFixedSimulationStep
    {
        [SerializeField] private Collider2D _bodyCollider;
        [SerializeField] private EnemyActor2D _actor;
        [SerializeField] private EnemyMotor2D _motor;
        [SerializeField] private EnemyContactDamageConfig _config;

        private bool _hasImpacted;
        private ulong _attackId;
        private float _remaining;
        public float RemainingCooldown => _remaining;
        public void Simulate(float deltaTime) => _remaining = Mathf.Max(0f, _remaining - Mathf.Max(0f, deltaTime));

        public event Action<EnemyContactAttack2D, Vector2> ImpactOccurred;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyContactAttack2D)}] " +
                    $"接触攻击装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _hasImpacted = false;
            _remaining = 0f;
            _attackId = DamageAttackIdAllocator.Next();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 子盾牌的触发不能冒充本体接触。
            if (other != null && _bodyCollider.Distance(other).isOverlapped) TryImpact(other);
        }
        private void OnTriggerStay2D(Collider2D other)
        {
            if (!_config.DespawnOnImpact && other != null && _bodyCollider.Distance(other).isOverlapped) TryImpact(other);
        }

        public bool TryImpact(Collider2D other) => TryImpact(other, _bodyCollider.bounds.center);

        // 扫掠运动可以跨越屏幕边界；使用当前线段的起点，而非尚未移动的刚体位置。
        public bool TryImpact(Collider2D other, Vector2 contactOrigin)
        {
            if (!isActiveAndEnabled || !_bodyCollider.enabled || _hasImpacted || _remaining > 0f || other == null ||
                !_config.ContainsLayer(other.gameObject.layer))
            {
                return false;
            }

            if (!other.TryGetComponent(out DamageHitbox2D hitbox) ||
                !hitbox.IsActiveTarget)
            {
                return false;
            }

            _hasImpacted = _config.DespawnOnImpact;
            _remaining = _config.RepeatIntervalSeconds;
            _attackId = DamageAttackIdAllocator.Next();
            Vector2 hitPoint = other.ClosestPoint(contactOrigin);
            Vector2 direction = (Vector2)other.bounds.center - contactOrigin;
            if (direction.sqrMagnitude <= Mathf.Epsilon) direction = _motor.TravelDirection;
            DamagePacket damage = new DamagePacket(
                _config.DamageAmount,
                hitPoint,
                direction,
                gameObject,
                _attackId,
                DamageInterceptionPolicy.Blockable,
                knockbackDistance: _config.KnockbackDistance, knockbackSeconds: _config.KnockbackSeconds);
            hitbox.TryReceiveDamage(in damage);

            ImpactOccurred?.Invoke(this, hitPoint);
            if (_config.DespawnOnImpact)
                _actor.TryRequestDespawn(EnemyDespawnReason.ContactImpact, hitPoint);
            else
                _actor.NotifyContactImpact(hitPoint, direction);
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_bodyCollider == null)
            {
                reason = "未配置主体碰撞体。";
                return false;
            }

            if (!_bodyCollider.isTrigger)
            {
                reason = "敌人接触判定碰撞体必须是触发器。";
                return false;
            }

            if (_actor == null)
            {
                reason = "未配置敌人实体。";
                return false;
            }

            if (_motor == null)
            {
                reason = "未配置敌人运动组件。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置接触伤害参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }
    }
}
