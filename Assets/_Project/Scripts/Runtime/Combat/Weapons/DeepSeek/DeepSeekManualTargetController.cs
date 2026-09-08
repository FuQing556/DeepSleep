using DeepSleep.Runtime.Combat.Targeting;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Orientation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek
{
    /// <summary>
    /// 维护 DeepSeek 的免费手动目标锁定与回正状态。
    /// 集中检索的资源和伤害增益由后续独立技能组件处理。
    /// </summary>
    public sealed class DeepSeekManualTargetController :
        MonoBehaviour,
        IPlayerActionCommandConsumer
    {
        [SerializeField] private Transform _targetingOrigin;
        [SerializeField] private PlayerFacingController2D _facingController;
        [SerializeField] private DeepSeekManualTargetingConfig _config;

        private ManualTargetFinder2D _targetFinder;
        private Collider2D _lockedTarget;
        private float _remainingReturnDelay;
        private bool _isInitialized;

        public PlayerActionBlock ActionCategory =>
            PlayerActionBlock.ActiveCombat;

        public bool HasLockedTarget => IsTargetValid(_lockedTarget);

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DeepSeekManualTargetController)}] " +
                    $"手动锁定装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _targetFinder = new ManualTargetFinder2D(
                _config.TargetLayers,
                _config.MaximumTargetCandidates);
            _isInitialized = true;
        }

        public void ConsumeCommand(
            in PlayerCommand command,
            float deltaTime)
        {
            if (!_isInitialized || !isActiveAndEnabled)
            {
                return;
            }

            if (WasPressed(command.CancelAim))
            {
                ClearTarget();
            }
            else if (WasPressed(command.ConfirmAim))
            {
                SelectTarget(command.Aim);
            }

            UpdateLockState(Mathf.Max(0f, deltaTime));
        }

        public bool TryGetLockedTargetPosition(out Vector2 targetPosition)
        {
            if (!IsTargetValid(_lockedTarget))
            {
                targetPosition = default;
                return false;
            }

            targetPosition = _lockedTarget.bounds.center;
            return true;
        }

        public bool TryLockTarget(Collider2D target)
        {
            if (!_isInitialized || !IsTargetValid(target))
            {
                return false;
            }

            _lockedTarget = target;
            _remainingReturnDelay = 0f;
            UpdateFacing(target.bounds.center);
            return true;
        }

        public void ClearTarget()
        {
            if (!_isInitialized)
            {
                return;
            }

            _lockedTarget = null;
            _remainingReturnDelay = _config.ReturnDelaySeconds;
        }

        private void SelectTarget(AimIntent aim)
        {
            Collider2D selectedTarget = null;
            Vector2 origin = _targetingOrigin.position;

            bool foundTarget = aim.Reference switch
            {
                AimReference.WorldPosition =>
                    _targetFinder.TryFindNearPoint(
                        aim.Value,
                        origin,
                        _config.PointSelectionRadius,
                        _config.MaximumLockDistance,
                        out selectedTarget),
                AimReference.Direction =>
                    _targetFinder.TryFindInDirection(
                        origin,
                        aim.Value,
                        _config.MaximumLockDistance,
                        _config.MinimumDirectionDot,
                        out selectedTarget),
                _ => false
            };

            if (foundTarget)
            {
                TryLockTarget(selectedTarget);
                return;
            }

            ClearTarget();
        }

        private void UpdateLockState(float deltaTime)
        {
            if (_lockedTarget != null)
            {
                if (!IsTargetValid(_lockedTarget))
                {
                    ClearTarget();
                    return;
                }

                UpdateFacing(_lockedTarget.bounds.center);
                return;
            }

            if (_remainingReturnDelay <= 0f)
            {
                return;
            }

            _remainingReturnDelay = Mathf.Max(
                0f,
                _remainingReturnDelay - deltaTime);

            if (_remainingReturnDelay <= 0f)
            {
                _facingController.ResetToInitialDirection();
            }
        }

        private void UpdateFacing(Vector2 targetPosition)
        {
            float horizontalOffset =
                targetPosition.x - _targetingOrigin.position.x;

            if (Mathf.Abs(horizontalOffset) <= _config.FacingDeadZone)
            {
                return;
            }

            _facingController.SetDirection(
                horizontalOffset < 0f
                    ? FacingDirection.Left
                    : FacingDirection.Right);
        }

        private bool IsTargetValid(Collider2D target)
        {
            if (target == null ||
                !target.isActiveAndEnabled ||
                !target.gameObject.activeInHierarchy ||
                !IsInTargetLayer(target.gameObject.layer))
            {
                return false;
            }

            Vector2 offset =
                (Vector2)target.bounds.center -
                (Vector2)_targetingOrigin.position;

            return offset.sqrMagnitude <=
                   _config.MaximumLockDistance *
                   _config.MaximumLockDistance;
        }

        private bool IsInTargetLayer(int layer)
        {
            return (_config.TargetLayers.value & (1 << layer)) != 0;
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _lockedTarget = null;
            _remainingReturnDelay = 0f;
            _facingController.ResetToInitialDirection();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_targetingOrigin == null)
            {
                reason = "未配置索敌原点。";
                return false;
            }

            if (_facingController == null)
            {
                reason = "未配置角色朝向控制器。";
                return false;
            }

            if (!_facingController.TryValidateConfiguration(out reason))
            {
                return false;
            }

            if (_config == null)
            {
                reason = "未配置手动锁定参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }

        private static bool WasPressed(CommandButtonState state)
        {
            return (state & CommandButtonState.Pressed) != 0;
        }
    }
}
