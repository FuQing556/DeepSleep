using System;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive
{
    /// <summary>
    /// 管理玩家正在进行的复活引导及其行动限制。
    /// 范围、静止起步和进度由后续复活协调器负责。
    /// </summary>
    public sealed class PlayerReviveActionChannel :
        MonoBehaviour,
        IPlayerCommandPreprocessor
    {
        private const float TimeComparisonTolerance = 0.00001f;

        private const PlayerActionBlock ChannelBlocks =
            PlayerActionBlock.Movement |
            PlayerActionBlock.AutomaticCombat |
            PlayerActionBlock.ActiveCombat;

        [SerializeField] private PlayerActionGate _actionGate;
        [SerializeField] private PlayerReviveChannelConfig _config;
        [SerializeField] private PlayerDamageReceiver2D _damageReceiver;
        [SerializeField] private MonoBehaviour[] _startBlockerComponents;

        private IPlayerReviveStartBlocker[] _startBlockers;
        private PlayerLifeStateController2D _offeredTarget;
        private float _remainingAutomaticStartDelay;
        private bool _isInitialized;

        public event Action ChannelStarted;
        public event Action ChannelCancelled;
        public event Action ChannelCompleted;

        public bool IsChanneling { get; private set; }

        public PlayerLifeStateController2D OfferedTarget => _offeredTarget;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerReviveActionChannel)}] " +
                    $"复活引导装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            ResolveStartBlockers();

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                _damageReceiver.DamageAccepted += OnDamageAccepted;
            }
        }

        public bool TryOfferTarget(PlayerLifeStateController2D target)
        {
            if (!_isInitialized || !isActiveAndEnabled || target == null ||
                target.State != PlayerLifeState.Downed)
            {
                return false;
            }

            if (_offeredTarget != null && _offeredTarget != target)
            {
                return false;
            }

            _offeredTarget = target;
            ResetAutomaticStartDelay();
            return true;
        }

        public void WithdrawTarget(PlayerLifeStateController2D target)
        {
            if (_offeredTarget != target)
            {
                return;
            }

            CancelChannel();
            _offeredTarget = null;
            ResetAutomaticStartDelay();
        }

        public bool TryBeginChannel()
        {
            if (!_isInitialized || !isActiveAndEnabled || IsChanneling ||
                _offeredTarget == null ||
                _offeredTarget.State != PlayerLifeState.Downed ||
                IsBlockedByActiveAction())
            {
                return false;
            }

            IsChanneling = true;
            _actionGate.SetBlock(this, ChannelBlocks);
            ChannelStarted?.Invoke();
            return true;
        }

        public void CancelChannel()
        {
            if (!IsChanneling)
            {
                return;
            }

            IsChanneling = false;
            _actionGate.ClearBlock(this);
            ResetAutomaticStartDelay();
            ChannelCancelled?.Invoke();
        }

        public bool TryCompleteChannel(PlayerLifeStateController2D target)
        {
            if (!IsChanneling || _offeredTarget != target)
            {
                return false;
            }

            IsChanneling = false;
            _actionGate.ClearBlock(this);
            ResetAutomaticStartDelay();
            ChannelCompleted?.Invoke();
            return true;
        }

        public void PreprocessCommand(
            in PlayerCommand command,
            float deltaTime)
        {
            bool hasActiveIntent = HasCancellingIntent(in command);

            if (IsChanneling)
            {
                if (hasActiveIntent)
                {
                    // 在普通消费者运行前解除限制，因此本帧操作不会被吞掉。
                    CancelChannel();
                }

                return;
            }

            if (_offeredTarget == null ||
                _offeredTarget.State != PlayerLifeState.Downed ||
                hasActiveIntent ||
                IsBlockedByActiveAction())
            {
                ResetAutomaticStartDelay();
                return;
            }

            _remainingAutomaticStartDelay = Mathf.Max(
                0f,
                _remainingAutomaticStartDelay - Mathf.Max(0f, deltaTime));

            if (_remainingAutomaticStartDelay <= TimeComparisonTolerance)
            {
                TryBeginChannel();
            }
        }

        private bool HasCancellingIntent(in PlayerCommand command)
        {
            float deadZone = _config.MovementCancelDeadZone;

            return command.Move.sqrMagnitude > deadZone * deadZone ||
                IsActive(command.PrimarySkill) ||
                IsActive(command.SecondarySkill) ||
                IsActive(command.ConfirmAim) ||
                IsActive(command.CancelAim);
        }

        private void OnDisable()
        {
            if (_damageReceiver != null)
            {
                _damageReceiver.DamageAccepted -= OnDamageAccepted;
            }

            CancelChannel();
            _offeredTarget = null;
            ResetAutomaticStartDelay();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_actionGate == null || _damageReceiver == null)
            {
                reason = "未配置玩家行动限制器或受伤门。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置复活引导参数。";
                return false;
            }

            int blockerCount = _startBlockerComponents?.Length ?? 0;

            for (int index = 0; index < blockerCount; index++)
            {
                if (_startBlockerComponents[index] is
                    IPlayerReviveStartBlocker)
                {
                    continue;
                }

                reason = $"第 {index} 个开始阻断器未实现" +
                    $"{nameof(IPlayerReviveStartBlocker)}。";
                return false;
            }

            return _config.TryValidate(out reason);
        }

        private void ResolveStartBlockers()
        {
            int count = _startBlockerComponents?.Length ?? 0;
            _startBlockers = new IPlayerReviveStartBlocker[count];

            for (int index = 0; index < count; index++)
            {
                _startBlockers[index] =
                    (IPlayerReviveStartBlocker)_startBlockerComponents[index];
            }
        }

        private bool IsBlockedByActiveAction()
        {
            for (int index = 0; index < _startBlockers.Length; index++)
            {
                if (_startBlockers[index].BlocksReviveStart)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetAutomaticStartDelay()
        {
            _remainingAutomaticStartDelay =
                _config != null ? _config.AutomaticStartDelaySeconds : 0f;
        }

        private void OnDamageAccepted(
            PlayerDamageReceiver2D receiver,
            DeepSleep.Runtime.Combat.Damage.DamagePacket damage)
        {
            CancelChannel();
        }

        private static bool IsActive(CommandButtonState state)
        {
            const CommandButtonState activeStates =
                CommandButtonState.Pressed | CommandButtonState.Held;
            return (state & activeStates) != 0;
        }
    }
}
