using System;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Health
{
    /// <summary>
    /// 保存一个实体的运行时生命状态，不决定死亡动画、掉落或回收策略。
    /// </summary>
    public sealed class HealthComponent : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] private HealthConfig _config;

        private float _currentHealth;
        private bool _isInitialized;

        public event Action<HealthComponent> HealthChanged;
        public event Action<HealthComponent> Depleted;

        public float CurrentHealth => _currentHealth;
        public float MaximumHealth =>
            _config != null ? _config.MaximumHealth : 0f;
        public bool IsDepleted => _currentHealth <= 0f;
        public bool CanReceiveDamage =>
            _isInitialized && isActiveAndEnabled && !IsDepleted;

        private void Awake()
        {
            string reason = "未配置生命参数。";

            if (_config == null || !_config.TryValidate(out reason))
            {
                Debug.LogError(
                    $"[{nameof(HealthComponent)}] " +
                    $"生命配置无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
            ResetToMaximum();
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid)
            {
                return false;
            }

            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - damage.Amount);

            if (Mathf.Approximately(previousHealth, _currentHealth))
            {
                return false;
            }

            HealthChanged?.Invoke(this);

            if (IsDepleted)
            {
                Depleted?.Invoke(this);
            }

            return true;
        }

        public bool TryRestore(float amount)
        {
            if (!_isInitialized || IsDepleted || amount <= 0f ||
                _currentHealth >= MaximumHealth)
            {
                return false;
            }

            _currentHealth = Mathf.Min(
                MaximumHealth,
                _currentHealth + amount);
            HealthChanged?.Invoke(this);
            return true;
        }

        /// <summary>
        /// 只允许生命已经耗尽的实体重新进入有生命状态。
        /// 具体是否能够复活由实体自己的生命周期系统决定。
        /// </summary>
        public bool TryRevive(float health)
        {
            if (!_isInitialized || !IsDepleted || health <= 0f)
            {
                return false;
            }

            _currentHealth = Mathf.Min(MaximumHealth, health);
            HealthChanged?.Invoke(this);
            return true;
        }

        public void ResetToMaximum()
        {
            if (!_isInitialized)
            {
                return;
            }

            _currentHealth = MaximumHealth;
            HealthChanged?.Invoke(this);
        }

        public bool RestoreCheckpointHealth(float health)
        {
            if (!_isInitialized || health <= 0f)
            {
                return false;
            }

            _currentHealth = Mathf.Clamp(health, 0.01f, MaximumHealth);
            HealthChanged?.Invoke(this);
            return true;
        }
    }
}
