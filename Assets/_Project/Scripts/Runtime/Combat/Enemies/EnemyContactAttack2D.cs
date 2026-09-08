using System;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 一次性接触攻击。接触合法目标后先尝试提交伤害，
    /// 随后无条件消耗敌人，因此护盾和无敌帧不会留下贴身判定。
    /// </summary>
    public sealed class EnemyContactAttack2D : MonoBehaviour
    {
        [SerializeField] private Collider2D _bodyCollider;
        [SerializeField] private EnemyActor2D _actor;
        [SerializeField] private EnemyMotor2D _motor;
        [SerializeField] private EnemyContactDamageConfig _config;

        private bool _hasImpacted;

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
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasImpacted || other == null ||
                !_config.ContainsLayer(other.gameObject.layer))
            {
                return;
            }

            if (!other.TryGetComponent(out DamageHitbox2D hitbox) ||
                !hitbox.IsActiveTarget)
            {
                return;
            }

            _hasImpacted = true;
            Vector2 hitPoint = other.ClosestPoint(_bodyCollider.bounds.center);
            DamagePacket damage = new DamagePacket(
                _config.DamageAmount,
                hitPoint,
                _motor.TravelDirection,
                gameObject);
            hitbox.TryReceiveDamage(in damage);

            ImpactOccurred?.Invoke(this, hitPoint);
            _actor.TryRequestDespawn(
                EnemyDespawnReason.ContactImpact,
                hitPoint);
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
