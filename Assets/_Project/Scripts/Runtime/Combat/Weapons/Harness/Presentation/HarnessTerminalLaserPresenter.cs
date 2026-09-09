using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Players.Orientation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// 连接终端激光运行时事件与三个独立视图，不参与战斗裁决。
    /// </summary>
    public sealed class HarnessTerminalLaserPresenter : MonoBehaviour
    {
        public bool CharacterPoseExternallyDriven { get; set; }
        [Header("数据源")]
        [SerializeField] private HarnessTerminalLaserController _controller;
        [SerializeField] private PlayerFacingController2D _facingController;
        [SerializeField]
        private HarnessTerminalLaserPresentationConfig _config;

        [Header("视图")]
        [SerializeField]
        private HarnessLaserTargetingView2D _targetingView = new();
        [SerializeField]
        private HarnessLaserShotView2D _shotView = new();
        [SerializeField]
        private HarnessLaserPoseView2D _poseView = new();

        private bool _isInitialized;
        private bool _poseOverridden;

        public void SetPoseOverride(bool value)
        {
            _poseOverridden = value;
            if (value)
            {
                if (!CharacterPoseExternallyDriven) _poseView.Release();
                _targetingView.Hide();
                _shotView.HideMuzzleIfIdle();
            }
        }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(HarnessTerminalLaserPresenter)}] " +
                    $"终端激光表现装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
            ResetViews();
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _controller.FireRequested += OnFireRequested;
            _controller.TargetChanged += OnTargetChanged;
            if (!CharacterPoseExternallyDriven) _poseView.SetAiming(
                _controller.HasAimPoint,
                _config);
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            bool shotFinished = _shotView.Tick(Time.deltaTime, _config);
            if (_poseOverridden) return;

            if (shotFinished && !_controller.HasAimPoint)
            {
                _facingController.ResetToInitialDirection();
            }

            UpdateTargetingView();
            if (!CharacterPoseExternallyDriven) _poseView.Tick(
                Time.deltaTime,
                _controller.HasAimPoint,
                _config);
        }

        private void OnDisable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _controller.FireRequested -= OnFireRequested;
            _controller.TargetChanged -= OnTargetChanged;
            ResetViews();
        }

        private void OnFireRequested(
            HarnessTerminalLaserFireRequest request)
        {
            if (request == null || !request.IsValid)
            {
                return;
            }

            _shotView.Play(request, this);
            if (!CharacterPoseExternallyDriven) _poseView.HoldFirePose(
                _config.FirePoseHoldSeconds,
                _config);
        }

        private void OnTargetChanged(DamageHitbox2D target)
        {
            if (_poseOverridden) return;
            if (!CharacterPoseExternallyDriven) _poseView.SetAiming(
                _controller.HasAimPoint,
                _config);
        }

        private void UpdateTargetingView()
        {
            if (!_controller.TryGetSelectedTargetPosition(
                    out Vector2 targetPosition))
            {
                _targetingView.Hide();
                _shotView.HideMuzzleIfIdle();
                return;
            }

            Vector2 beamOrigin = _controller.BeamOriginPosition;
            _targetingView.Render(
                beamOrigin,
                targetPosition,
                _controller.State,
                _controller.StateProgress01,
                _config,
                Time.time);

            if (_controller.State ==
                HarnessTerminalLaserState.Calibrating)
            {
                _shotView.ShowChargingMuzzle(
                    beamOrigin,
                    targetPosition - beamOrigin,
                    _controller.StateProgress01,
                    _config);
            }
            else if (_controller.State ==
                     HarnessTerminalLaserState.Cooldown)
            {
                _shotView.ShowQueuedMuzzle(
                    beamOrigin,
                    targetPosition - beamOrigin,
                    _config);
            }
            else
            {
                _shotView.HideMuzzleIfIdle();
            }
        }

        private void ResetViews()
        {
            _targetingView.Hide();
            _shotView.Stop();
            _poseView.Reset();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_controller == null)
            {
                reason = "未配置 Harness 终端激光控制器。";
                return false;
            }

            if (_facingController == null)
            {
                reason = "未配置角色朝向控制器。";
                return false;
            }

            if (!_controller.TryValidateConfiguration(out reason))
            {
                reason = $"Harness 终端激光控制器无效：{reason}";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置终端激光表现参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                return false;
            }

            if (_targetingView == null)
            {
                reason = "未配置校准视图。";
                return false;
            }

            if (!_targetingView.TryValidateConfiguration(out reason))
            {
                reason = $"校准视图无效：{reason}";
                return false;
            }

            if (_shotView == null)
            {
                reason = "未配置开火视图。";
                return false;
            }

            if (!_shotView.TryValidateConfiguration(out reason))
            {
                reason = $"开火视图无效：{reason}";
                return false;
            }

            if (_poseView == null)
            {
                reason = "未配置角色姿态视图。";
                return false;
            }

            if (!_poseView.TryValidateConfiguration(out reason))
            {
                reason = $"角色姿态视图无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
