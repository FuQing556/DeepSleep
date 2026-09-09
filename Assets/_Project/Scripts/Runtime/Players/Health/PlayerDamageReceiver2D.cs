using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Health
{
    /// <summary>
    /// 玩家专属伤害门：将合法伤害转交给通用生命组件，成功扣血后开启无敌时间。
    /// 它不决定倒地、复活、UI 或受击动画。
    /// </summary>
    public sealed class PlayerDamageReceiver2D : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] private HealthComponent _health;
        [SerializeField] private PlayerDamageResponseConfig _config;

        private float _remainingInvulnerabilitySeconds;
        private bool _isReviveProtection;
        private bool _isInitialized;
        private readonly List<IPlayerDamageInterceptor> _damageInterceptors = new(2);

        public event Action<PlayerDamageReceiver2D, DamagePacket>
            DamageAccepted;
        public event Action<PlayerDamageReceiver2D>
            InvulnerabilityEnded;

        public bool IsInvulnerable =>
            _remainingInvulnerabilitySeconds > 0f;
        public float RemainingInvulnerabilitySeconds =>
            _remainingInvulnerabilitySeconds;
        public bool IsReviveProtected => IsInvulnerable && _isReviveProtection;
        public bool CanReceiveDamage =>
            _isInitialized &&
            isActiveAndEnabled &&
            !IsInvulnerable &&
            _health.CanReceiveDamage;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerDamageReceiver2D)}] " +
                    $"玩家受伤装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void FixedUpdate()
        {
            if (!IsInvulnerable)
            {
                return;
            }

            _remainingInvulnerabilitySeconds = Mathf.Max(
                0f,
                _remainingInvulnerabilitySeconds - Time.deltaTime);

            if (!IsInvulnerable)
            {
                _isReviveProtection = false;
                InvulnerabilityEnded?.Invoke(this);
            }
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid)
            {
                return false;
            }

            for (int index = 0; index < _damageInterceptors.Count; index++)
            {
                if (_damageInterceptors[index].TryIntercept(this, in damage))
                    return true;
            }

            if (!_health.TryReceiveDamage(in damage)) return false;

            _remainingInvulnerabilitySeconds =
                _config.InvulnerabilityDurationSeconds;
            _isReviveProtection = false;
            DamageAccepted?.Invoke(this, damage);
            return true;
        }

        public bool RegisterDamageInterceptor(IPlayerDamageInterceptor interceptor)
        {
            if (interceptor == null || _damageInterceptors.Contains(interceptor))
                return false;
            _damageInterceptors.Add(interceptor);
            return true;
        }

        public void UnregisterDamageInterceptor(IPlayerDamageInterceptor interceptor)
        {
            if (interceptor != null) _damageInterceptors.Remove(interceptor);
        }

        /// <summary>
        /// 供之后的重生或章节重置流程显式清除受伤保护状态。
        /// </summary>
        public void ResetDamageGate()
        {
            _remainingInvulnerabilitySeconds = 0f;
            _isReviveProtection = false;
        }

        public void BeginInvulnerability(float durationSeconds)
        {
            if (!_isInitialized || durationSeconds <= 0f)
            {
                return;
            }

            _remainingInvulnerabilitySeconds = Mathf.Max(
                _remainingInvulnerabilitySeconds,
                durationSeconds);
            _isReviveProtection = true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_health == null)
            {
                reason = "未配置通用生命组件。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置玩家受伤响应参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }
    }
}
