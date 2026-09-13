using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Orientation;
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
        [SerializeField] private PlayerFacingController2D _facing;

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

            float forwardSpeed = Vector2.Dot(
                _body.linearVelocity,
                _facing.Forward);
            float normalizedForwardSpeed = Mathf.Clamp(
                forwardSpeed / _motorConfig.MaximumSpeed,
                -1f,
                1f);

            // 倾斜节点位于朝向节点内部。沿当前朝向移动始终前倾，
            // 逆着当前朝向移动则后仰；父节点翻转会自动镜像旋转。
            float targetTiltDegrees =
                -normalizedForwardSpeed * _tiltConfig.MaximumTiltDegrees;

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

            if (_facing == null)
            {
                reason = "未配置角色朝向控制器。";
                return false;
            }

            return _tiltConfig.TryValidate(out reason);
        }
    }
}
