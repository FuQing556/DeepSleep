using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Presentation
{
    /// <summary>
    /// 根据玩家的实际水平速度旋转专用视觉节点，制造前倾和后仰效果。
    /// </summary>
    public sealed class PlayerMovementTiltPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _tiltRoot;
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private PlayerMotorConfig _motorConfig;
        [SerializeField] private PlayerMovementTiltConfig _tiltConfig;

        private Quaternion _baseLocalRotation;
        private float _currentTiltDegrees;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerMovementTiltPresenter)}] " +
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

            float normalizedHorizontalSpeed = Mathf.Clamp(
                _body.linearVelocity.x / _motorConfig.MaximumSpeed,
                -1f,
                1f);

            // 向右飞行时顺时针前倾，向左飞行时逆时针后仰。
            float targetTiltDegrees =
                -normalizedHorizontalSpeed * _tiltConfig.MaximumTiltDegrees;

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
            if (_tiltRoot == null)
            {
                reason = "未配置专用倾斜节点。";
                return false;
            }

            if (_body == null)
            {
                reason = "未配置玩家 Rigidbody2D。";
                return false;
            }

            if (_motorConfig == null)
            {
                reason = "未配置玩家移动参数。";
                return false;
            }

            if (!_motorConfig.TryValidate(out reason))
            {
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
