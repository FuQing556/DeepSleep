using System;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Simulation;
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
        [SerializeField] private MonoBehaviour[] _simulationStepComponents;
        private IFixedSimulationStep[] _simulationSteps;

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

            int count = _simulationStepComponents?.Length ?? 0;
            _simulationSteps = new IFixedSimulationStep[count];
            for (int index = 0; index < count; index++)
            {
                _simulationSteps[index] = (IFixedSimulationStep)_simulationStepComponents[index];
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
            int count = _simulationStepComponents?.Length ?? 0;
            for (int index = 0; index < count; index++)
            {
                MonoBehaviour component = _simulationStepComponents[index];
                if (component is not IFixedSimulationStep ||
                    component.gameObject != gameObject ||
                    Array.IndexOf(_simulationStepComponents, component) != index)
                {
                    reason = $"敌人模拟步骤 {index} 必须是同根节点的唯一 IFixedSimulationStep。";
                    return false;
                }
            }
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

        /// <summary>由所属对象池推进：选目标、攻击、移动。回收立即终止后续步骤。</summary>
        public void Simulate(float deltaTime)
        {
            if (!_isInitialized || !isActiveAndEnabled || _despawnRequested || deltaTime <= 0f)
            {
                return;
            }
            _motor.PrepareSimulation(deltaTime);
            for (int index = 0; index < _simulationSteps.Length; index++)
            {
                if (_despawnRequested) return;
                if (_simulationStepComponents[index].isActiveAndEnabled)
                {
                    _simulationSteps[index].Simulate(deltaTime);
                }
            }
            if (!_despawnRequested) _motor.Simulate(deltaTime);
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
