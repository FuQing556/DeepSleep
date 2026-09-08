using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Players.LifeCycle
{
    /// <summary>
    /// 将生命耗尽转换为玩家宕机状态，同时保留玩家根对象与稳定身份。
    /// 热重连负责如何离开 Downed，后续通过独立系统接入。
    /// </summary>
    public sealed class PlayerLifeStateController2D : MonoBehaviour
    {
        [SerializeField] private HealthComponent _health;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private DamageHitbox2D _damageHitbox;
        [SerializeField] private PlayerDamageReceiver2D _damageReceiver;
        [SerializeField] private PlayerDownedVisual2D _downedVisual;
        [SerializeField] private Behaviour[] _disabledWhileDowned;

        private bool _isInitialized;
        private bool[] _enabledBeforeDowned;

        public event Action<PlayerLifeStateController2D, PlayerLifeState>
            StateChanged;

        public PlayerLifeState State { get; private set; } =
            PlayerLifeState.Alive;

        public float MaximumHealth => _health != null
            ? _health.MaximumHealth
            : 0f;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerLifeStateController2D)}] " +
                    $"玩家生命状态装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
            _enabledBeforeDowned =
                new bool[_disabledWhileDowned.Length];
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                _health.Depleted += OnHealthDepleted;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Depleted -= OnHealthDepleted;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_health == null || _body == null ||
                _damageHitbox == null || _damageReceiver == null ||
                _downedVisual == null)
            {
                reason = "生命、刚体、受伤判定、伤害门和宕机表现必须全部配置。";
                return false;
            }

            if (!_downedVisual.TryValidateConfiguration(out reason))
            {
                reason = $"宕机表现无效：{reason}";
                return false;
            }

            if (_disabledWhileDowned == null ||
                _disabledWhileDowned.Length == 0)
            {
                reason = "至少需要配置一个宕机时停用的玩法组件。";
                return false;
            }

            for (int index = 0;
                 index < _disabledWhileDowned.Length;
                 index++)
            {
                Behaviour behaviour = _disabledWhileDowned[index];

                if (behaviour == null || behaviour == this ||
                    behaviour == _downedVisual)
                {
                    reason = $"停用列表第 {index} 项为空或包含状态系统自身。";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public bool TryRevive(
            float revivedHealth,
            float invulnerabilitySeconds)
        {
            if (!_isInitialized || State != PlayerLifeState.Downed ||
                revivedHealth <= 0f || invulnerabilitySeconds <= 0f ||
                !_health.TryRevive(revivedHealth))
            {
                return false;
            }

            State = PlayerLifeState.Alive;
            _downedVisual.ShowAlivePose();

            for (int index = 0;
                 index < _disabledWhileDowned.Length;
                 index++)
            {
                if (_enabledBeforeDowned[index])
                {
                    _disabledWhileDowned[index].enabled = true;
                }
            }

            _damageReceiver.BeginInvulnerability(invulnerabilitySeconds);
            _damageHitbox.enabled = true;
            StateChanged?.Invoke(this, State);
            return true;
        }

        private void OnHealthDepleted(HealthComponent health)
        {
            if (!_isInitialized || State == PlayerLifeState.Downed)
            {
                return;
            }

            // 先截取死亡瞬间；后续组件的 OnDisable 可以放心清理姿态。
            _downedVisual.CaptureCurrentPose();
            State = PlayerLifeState.Downed;

            for (int index = 0;
                 index < _disabledWhileDowned.Length;
                 index++)
            {
                _enabledBeforeDowned[index] =
                    _disabledWhileDowned[index].enabled;
                _disabledWhileDowned[index].enabled = false;
            }

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _damageHitbox.enabled = false;
            _downedVisual.ShowDownedPose();
            StateChanged?.Invoke(this, State);
        }
    }
}
