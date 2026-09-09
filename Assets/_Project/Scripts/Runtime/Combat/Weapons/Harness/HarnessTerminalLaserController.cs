using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Targeting;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// 把 Harness 的手动目标命令驱动为一次终端激光开火请求。
    /// 本组件不做命中查询、不扣血，也不创建任何视觉对象。
    /// </summary>
    public sealed class HarnessTerminalLaserController :
        MonoBehaviour,
        IPlayerActionCommandConsumer,
        IPlayerReviveStartBlocker
    {
        [SerializeField] private Transform _beamOrigin;
        [SerializeField] private PlayerFacingController2D _facingController;
        [SerializeField] private CombatPlayfieldConfig _playfield;
        [SerializeField] private HarnessTerminalLaserConfig _config;

        private readonly HarnessTerminalLaserCycle _cycle = new();
        private ManualTargetFinder2D _targetFinder;
        private Predicate<Collider2D> _targetEligibility;
        private Collider2D _selectedTargetCollider;
        private DamageHitbox2D _selectedTargetHitbox;
        private bool _hasAimPoint;
        private Vector2 _lastKnownTargetPosition;
        private Vector2 _lastAimDirection;
        private uint _nextFireSequence;
        private bool _isInitialized;
        private bool _inputSuppressed;

        public void SetInputSuppressed(bool suppressed)
        {
            _inputSuppressed = suppressed;
            if (suppressed) CancelSelection();
        }

        public PlayerActionBlock ActionCategory =>
            PlayerActionBlock.ActiveCombat;

        public bool BlocksReviveStart =>
            State == HarnessTerminalLaserState.Calibrating;

        public event Action<HarnessTerminalLaserFireRequest> FireRequested;
        public event Action<HarnessTerminalLaserState> StateChanged;
        public event Action<DamageHitbox2D> TargetChanged;

        public HarnessTerminalLaserState State => _cycle.State;
        public float RemainingStateSeconds => _cycle.RemainingSeconds;
        public float StateProgress01 => _cycle.PhaseProgress01;
        public DamageHitbox2D SelectedTarget => _selectedTargetHitbox;
        public bool HasAimPoint => _hasAimPoint;
        public Vector2 BeamOriginPosition => _beamOrigin != null
            ? _beamOrigin.position
            : transform.position;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(HarnessTerminalLaserController)}] " +
                    $"终端激光装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _targetFinder = new ManualTargetFinder2D(
                _config.TargetLayers,
                _config.MaximumTargetCandidates);
            _targetEligibility = IsSelectableDamageTarget;
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

            if (_inputSuppressed)
            {
                AdvanceCycle(Mathf.Max(0f, deltaTime));
                return;
            }

            if (WasPressed(command.CancelAim))
            {
                CancelSelection();
            }
            else if (WasPressed(command.ConfirmAim))
            {
                SelectTarget(command.Aim);
            }

            AdvanceCycle(Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// 将点选结果提交给本轮瞄准。真人/AI的输入统一通过PlayerCommand进入。
        /// </summary>
        public bool TrySelectTarget(Collider2D target)
        {
            if (!_isInitialized || _inputSuppressed || !IsSelectableDamageTarget(target))
            {
                return false;
            }

            FaceTarget(target.bounds.center);
            SetSelectedTarget(target);

            if (_cycle.State != HarnessTerminalLaserState.Cooldown)
            {
                HarnessTerminalLaserState previousState = _cycle.State;
                _cycle.TryStartOrRestartCalibration(
                    _config.CalibrationSeconds);
                PublishStateChange(previousState);
            }

            return true;
        }

        public void CancelSelection()
        {
            if (!_isInitialized)
            {
                return;
            }

            ClearSelectedTarget();
            HarnessTerminalLaserState previousState = _cycle.State;
            _cycle.TryCancelCalibration();
            PublishStateChange(previousState);
        }

        public bool TryGetSelectedTargetPosition(
            out Vector2 targetPosition)
        {
            if (!_isInitialized || !_hasAimPoint)
            {
                targetPosition = default;
                return false;
            }

            targetPosition = _lastKnownTargetPosition;
            return true;
        }

        private void SelectTarget(AimIntent aim)
        {
            Collider2D selectedTarget = null;
            Vector2 origin = _beamOrigin.position;

            bool foundTarget = aim.Reference switch
            {
                AimReference.WorldPosition =>
                    _targetFinder.TryFindNearPoint(
                        aim.Value,
                        origin,
                        _config.PointSelectionRadius,
                        _config.MaximumLockDistance,
                        _targetEligibility,
                        out selectedTarget),
                AimReference.Direction =>
                    _targetFinder.TryFindInDirection(
                        origin,
                        aim.Value,
                        _config.MaximumLockDistance,
                        _config.MinimumDirectionDot,
                        _targetEligibility,
                        out selectedTarget),
                _ => false,
            };

            if (foundTarget)
            {
                TrySelectTarget(selectedTarget);
                return;
            }

            CancelSelection();
        }

        private void AdvanceCycle(float deltaTime)
        {
            UpdateTrackedAimPoint();
            HarnessTerminalLaserState previousState = _cycle.State;
            _cycle.Advance(deltaTime);
            PublishStateChange(previousState);

            if (_cycle.State == HarnessTerminalLaserState.Ready &&
                _hasAimPoint)
            {
                previousState = _cycle.State;
                _cycle.TryStartOrRestartCalibration(
                    _config.CalibrationSeconds);
                PublishStateChange(previousState);
            }

            if (_cycle.State != HarnessTerminalLaserState.Calibrating)
            {
                return;
            }

            if (!_hasAimPoint)
            {
                CancelSelection();
                return;
            }

            if (_cycle.IsCalibrationComplete)
            {
                TryRequestFire();
            }
        }

        private void TryRequestFire()
        {
            Vector2 sourceOrigin = _beamOrigin.position;
            Vector2 targetPosition = _lastKnownTargetPosition;
            Vector2 aimDirection = targetPosition - sourceOrigin;
            if (aimDirection.sqrMagnitude <= 0.000001f) aimDirection = _lastAimDirection;

            if (!HarnessTerminalLaserSnapshotFactory.TryCreate(
                    _nextFireSequence,
                    sourceOrigin,
                    aimDirection,
                    _playfield,
                    _config,
                    out var snapshot,
                    out string reason))
            {
                Debug.LogError(
                    $"[{nameof(HarnessTerminalLaserController)}] " +
                    $"无法创建终端激光快照：{reason}",
                    this);
                CancelSelection();
                return;
            }

            var request = new HarnessTerminalLaserFireRequest(
                gameObject,
                _selectedTargetHitbox,
                targetPosition,
                snapshot);

            unchecked
            {
                _nextFireSequence++;
            }

            FireRequested?.Invoke(request);
            ClearSelectedTarget();

            HarnessTerminalLaserState previousState = _cycle.State;
            _cycle.TryEnterCooldown(_config.FireCooldownSeconds);
            PublishStateChange(previousState);
        }

        private void SetSelectedTarget(Collider2D target)
        {
            target.TryGetComponent(out DamageHitbox2D hitbox);
            bool changed = _selectedTargetHitbox != hitbox || !_hasAimPoint;

            if (_selectedTargetHitbox != null)
                _selectedTargetHitbox.BecameUnavailable -= OnTargetUnavailable;

            _selectedTargetCollider = target;
            _selectedTargetHitbox = hitbox;
            _selectedTargetHitbox.BecameUnavailable += OnTargetUnavailable;
            _hasAimPoint = true;
            _lastKnownTargetPosition = target.bounds.center;
            _lastAimDirection = (_lastKnownTargetPosition - BeamOriginPosition).normalized;
            if (_lastAimDirection.sqrMagnitude <= 0.000001f) _lastAimDirection = _facingController.Forward;

            if (changed)
            {
                TargetChanged?.Invoke(_selectedTargetHitbox);
            }
        }

        private void FaceTarget(Vector2 targetPosition)
        {
            FacingDirection direction =
                targetPosition.x < transform.position.x
                    ? FacingDirection.Left
                    : FacingDirection.Right;
            _facingController.SetDirection(direction);
        }

        private void ClearSelectedTarget()
        {
            bool changed = _hasAimPoint;
            if (_selectedTargetHitbox != null)
                _selectedTargetHitbox.BecameUnavailable -= OnTargetUnavailable;
            _selectedTargetCollider = null;
            _selectedTargetHitbox = null;
            _hasAimPoint = false;

            if (changed)
            {
                TargetChanged?.Invoke(null);
            }
        }

        private void UpdateTrackedAimPoint()
        {
            if (!_hasAimPoint || _selectedTargetHitbox == null) return;
            if (!IsSelectableDamageTarget(_selectedTargetCollider))
            {
                OnTargetUnavailable(_selectedTargetHitbox);
                return;
            }
            _lastKnownTargetPosition = _selectedTargetCollider.bounds.center;
            Vector2 direction = _lastKnownTargetPosition - BeamOriginPosition;
            if (direction.sqrMagnitude > 0.000001f) _lastAimDirection = direction.normalized;
        }

        private void OnTargetUnavailable(DamageHitbox2D target)
        {
            if (_selectedTargetHitbox != target) return;
            target.BecameUnavailable -= OnTargetUnavailable;
            // 保存本轮瞄准点；立即放开入池对象，防止它再次生成后把激光拉去新位置。
            _selectedTargetCollider = null;
            _selectedTargetHitbox = null;
            TargetChanged?.Invoke(null);
        }

        private bool IsSelectableDamageTarget(Collider2D target)
        {
            if (target == null ||
                !target.isActiveAndEnabled ||
                !target.gameObject.activeInHierarchy ||
                !IsInTargetLayer(target.gameObject.layer) ||
                !target.TryGetComponent(out DamageHitbox2D hitbox) ||
                !hitbox.CanReceiveDamage)
            {
                return false;
            }

            Vector2 offset =
                (Vector2)target.bounds.center -
                (Vector2)_beamOrigin.position;
            float maximumDistance = _config.MaximumLockDistance;

            return offset.sqrMagnitude <=
                   maximumDistance * maximumDistance;
        }

        private bool IsInTargetLayer(int layer)
        {
            return (_config.TargetLayers.value & (1 << layer)) != 0;
        }

        private void PublishStateChange(
            HarnessTerminalLaserState previousState)
        {
            if (_cycle.State != previousState)
            {
                StateChanged?.Invoke(_cycle.State);
            }
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            ClearSelectedTarget();
            HarnessTerminalLaserState previousState = _cycle.State;
            _cycle.Reset();
            PublishStateChange(previousState);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_beamOrigin == null)
            {
                reason = "未配置激光起点。";
                return false;
            }

            if (_facingController == null)
            {
                reason = "未配置角色朝向控制器。";
                return false;
            }

            if (_playfield == null)
            {
                reason = "未配置逻辑战斗区域。";
                return false;
            }

            if (!_playfield.TryValidate(out reason))
            {
                return false;
            }

            if (_config == null)
            {
                reason = "未配置 Harness 终端激光参数。";
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
