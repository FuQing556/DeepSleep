using DeepSleep.Runtime.Players.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    /// <summary>
    /// 数据蛇蓝色移动形态的前倾表现。红色瞄准形态会平滑回正，
    /// 不改变刚体、碰撞体、炮口或移动方向。
    /// </summary>
    public sealed class DataCrawlerSnakeMovementTiltPresenter2D : MonoBehaviour
    {
        [SerializeField] private DataCrawlerSnakeMotor2D _motor;
        [SerializeField] private DataCrawlerSnakeVisual2D _visual;
        [SerializeField] private Transform _tiltRoot;
        [SerializeField] private PlayerMovementTiltConfig _tiltConfig;

        private Quaternion _baseLocalRotation;
        private float _currentTiltDegrees;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DataCrawlerSnakeMovementTiltPresenter2D)}] " +
                    $"移动倾斜装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _baseLocalRotation = _tiltRoot.localRotation;
            _isInitialized = true;
        }

        private void LateUpdate()
        {
            if (!_isInitialized || Time.deltaTime <= 0f)
            {
                return;
            }

            Vector2 direction = _motor.TravelDirection;
            float targetTiltDegrees = 0f;
            if (_motor.IsRunning && !_visual.IsAiming &&
                Mathf.Abs(direction.x) > 0.001f)
            {
                float horizontalAmount = Mathf.Clamp01(
                    Mathf.Abs(direction.x));
                targetTiltDegrees = direction.x > 0f
                    ? -horizontalAmount * _tiltConfig.MaximumTiltDegrees
                    : horizontalAmount * _tiltConfig.MaximumTiltDegrees;
            }

            float rotationSpeed = Mathf.Approximately(
                targetTiltDegrees,
                0f)
                ? _tiltConfig.ReturnSpeedDegreesPerSecond
                : _tiltConfig.TiltSpeedDegreesPerSecond;
            _currentTiltDegrees = Mathf.MoveTowardsAngle(
                _currentTiltDegrees,
                targetTiltDegrees,
                rotationSpeed * Time.deltaTime);
            _tiltRoot.localRotation =
                _baseLocalRotation *
                Quaternion.Euler(0f, 0f, _currentTiltDegrees);
        }

        private void OnDisable()
        {
            if (!_isInitialized || _tiltRoot == null)
            {
                return;
            }

            _currentTiltDegrees = 0f;
            _tiltRoot.localRotation = _baseLocalRotation;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_motor == null || _visual == null || _tiltRoot == null)
            {
                reason = "未配置运动、视觉状态或倾斜节点。";
                return false;
            }

            if (_tiltConfig == null)
            {
                reason = "未配置移动倾斜参数。";
                return false;
            }

            return _tiltConfig.TryValidate(out reason);
        }
    }
}
