using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard
{
    /// <summary>接收DS技能命令，维护共享六次护航，并在玩家扣血前作出拦截裁决。</summary>
    public sealed class DeepSeekRiceGuardController : MonoBehaviour,
        IPlayerActionCommandConsumer, IFixedSimulationStep,
        IPlayerDamageInterceptor
    {
        [Serializable]
        private sealed class ProtectedPlayer
        {
            [SerializeField] private PlayerDamageReceiver2D _receiver;
            [SerializeField, Tooltip("用于护航范围判断的玩家根节点。")]
            private Transform _root;

            public PlayerDamageReceiver2D Receiver => _receiver;
            public Transform Root => _root;
            public bool IsValid => _receiver != null && _root != null;
        }

        [SerializeField] private DeepSeekRiceGuardConfig _config;
        [SerializeField] private PlayerLifeStateController2D _ownerLifeState;
        [SerializeField] private ProtectedPlayer[] _protectedPlayers;

        private RiceGuardState _state;
        private bool _isInitialized;

        public PlayerActionBlock ActionCategory => PlayerActionBlock.ActiveCombat;
        public bool IsActive => _state?.IsActive == true;
        public bool IsWarning => _state?.IsWarning == true;
        public int Capacity => _config != null ? _config.Charges : 0;
        public int RemainingCharges => _state?.RemainingCharges ?? 0;
        public double RemainingSeconds => _state?.RemainingSeconds ?? 0;
        public double CooldownRemaining => _state?.CooldownRemaining ?? 0;
        public float Radius => _config != null ? _config.Radius : 0f;

        public event Action Activated;
        public event Action<PlayerDamageReceiver2D, DamagePacket, int>
            ChargeConsumed;
        public event Action Ended;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError($"[{nameof(DeepSeekRiceGuardController)}] 护航装配无效：{reason}", this);
                enabled = false;
                return;
            }

            _state = new RiceGuardState(_config);
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized) return;
            _ownerLifeState.StateChanged += OnOwnerLifeStateChanged;
            for (int index = 0; index < _protectedPlayers.Length; index++)
                _protectedPlayers[index].Receiver.RegisterDamageInterceptor(this);
        }

        private void OnDisable()
        {
            if (_ownerLifeState != null)
                _ownerLifeState.StateChanged -= OnOwnerLifeStateChanged;
            if (_protectedPlayers != null)
            {
                for (int index = 0; index < _protectedPlayers.Length; index++)
                    _protectedPlayers[index]?.Receiver?.UnregisterDamageInterceptor(this);
            }
            CancelActiveGuard();
        }

        public void ConsumeCommand(in PlayerCommand command, float deltaTime)
        {
            if ((command.PrimarySkill & CommandButtonState.Pressed) != 0)
                TryActivate();
        }

        /// <summary>供输入、AI与仅编辑器调试入口复用同一个施放门。</summary>
        public bool TryActivate()
        {
            if (!_isInitialized ||
                _ownerLifeState.State != PlayerLifeState.Alive ||
                !_state.TryActivate())
                return false;
            Activated?.Invoke();
            return true;
        }

        public void Simulate(float deltaTime)
        {
            if (!_isInitialized || deltaTime <= 0f) return;
            bool wasActive = _state.IsActive;
            _state.Simulate(deltaTime);
            if (wasActive && !_state.IsActive) Ended?.Invoke();
        }

        public bool TryIntercept(
            PlayerDamageReceiver2D target,
            in DamagePacket damage)
        {
            if (!_isInitialized ||
                damage.InterceptionPolicy != DamageInterceptionPolicy.Blockable ||
                !TryGetProtectedRoot(target, out Transform targetRoot) ||
                ((Vector2)targetRoot.position - (Vector2)transform.position).sqrMagnitude >
                _config.Radius * _config.Radius)
            {
                return false;
            }

            int previousCharges = _state.RemainingCharges;
            if (!_state.TryBlock(damage.AttackId)) return false;

            if (_state.RemainingCharges != previousCharges)
                ChargeConsumed?.Invoke(
                    target,
                    damage,
                    _state.RemainingCharges);
            if (previousCharges > 0 && !_state.IsActive)
                Ended?.Invoke();
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_config == null || !_config.IsValid)
            {
                reason = "未配置有效的护航参数。";
                return false;
            }
            if (_ownerLifeState == null)
            {
                reason = "未配置DS生命状态。";
                return false;
            }
            if (_protectedPlayers == null || _protectedPlayers.Length != 2)
            {
                reason = "必须显式配置DS与HS两个受保护玩家。";
                return false;
            }
            for (int index = 0; index < _protectedPlayers.Length; index++)
            {
                if (_protectedPlayers[index] == null || !_protectedPlayers[index].IsValid)
                {
                    reason = $"受保护玩家第 {index} 项不完整。";
                    return false;
                }
                for (int other = 0; other < index; other++)
                {
                    if (_protectedPlayers[other].Receiver == _protectedPlayers[index].Receiver)
                    {
                        reason = "受保护玩家不能重复。";
                        return false;
                    }
                }
            }
            reason = string.Empty;
            return true;
        }

        private bool TryGetProtectedRoot(
            PlayerDamageReceiver2D target,
            out Transform targetRoot)
        {
            for (int index = 0; index < _protectedPlayers.Length; index++)
            {
                if (_protectedPlayers[index].Receiver != target) continue;
                targetRoot = _protectedPlayers[index].Root;
                return true;
            }
            targetRoot = null;
            return false;
        }

        private void OnOwnerLifeStateChanged(
            PlayerLifeStateController2D owner,
            PlayerLifeState state)
        {
            if (state == PlayerLifeState.Downed) CancelActiveGuard();
        }

        private void CancelActiveGuard()
        {
            if (_state == null || !_state.IsActive) return;
            _state.Cancel();
            Ended?.Invoke();
        }
    }
}
