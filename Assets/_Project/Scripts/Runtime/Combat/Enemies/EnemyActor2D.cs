using System;
using DeepSleep.Runtime.Combat.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 汇总敌人的生命与运动生命周期，并向外请求回收。
    /// 本组件不直接销毁或停用对象。
    /// </summary>
    public sealed class EnemyActor2D : MonoBehaviour
    {
        [SerializeField] private HealthComponent _health;
        [SerializeField] private EnemyMotor2D _motor;

        private bool _despawnRequested;
        private bool _isInitialized;

        public event Action<EnemyActor2D, EnemyDespawnRequest2D>
            DespawnRequested;

        public HealthComponent Health => _health;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyActor2D)}] " +
                    $"敌人装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _health.Depleted += OnHealthDepleted;
            _motor.ExitedPlayfield += OnExitedPlayfield;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Depleted -= OnHealthDepleted;
            }

            if (_motor != null)
            {
                _motor.ExitedPlayfield -= OnExitedPlayfield;
                _motor.Stop();
            }
        }

        public void Activate(
            Vector2 spawnPosition,
            in EnemySpawnVariation2D variation)
        {
            if (!_isInitialized || !variation.IsValid)
            {
                return;
            }

            _despawnRequested = false;
            _health.ResetToMaximum();
            _motor.Begin(spawnPosition, in variation);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_health == null)
            {
                reason = "未配置生命组件。";
                return false;
            }

            if (_motor == null)
            {
                reason = "未配置运动组件。";
                return false;
            }

            if (!_motor.TryValidateConfiguration(out reason))
            {
                reason = $"运动组件无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void OnHealthDepleted(HealthComponent health)
        {
            TryRequestDespawn(EnemyDespawnReason.Defeated);
        }

        private void OnExitedPlayfield(EnemyMotor2D motor)
        {
            TryRequestDespawn(EnemyDespawnReason.ExitedPlayfield);
        }

        public bool TryRequestDespawn(EnemyDespawnReason reason)
        {
            return TryRequestDespawn(reason, transform.position);
        }

        public bool TryRequestDespawn(
            EnemyDespawnReason reason,
            Vector2 effectPosition)
        {
            if (_despawnRequested)
            {
                return false;
            }

            _despawnRequested = true;
            _motor.Stop();
            EnemyDespawnRequest2D request = new EnemyDespawnRequest2D(
                reason,
                effectPosition,
                transform.eulerAngles.z,
                _motor.TravelDirection);
            DespawnRequested?.Invoke(this, request);
            return true;
        }
    }
}
