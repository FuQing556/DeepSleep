using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.World.Cameras
{
    /// <summary>
    /// 根据本机所控角色的水平速度产生轻微镜头前视。
    /// 这是纯本地表现，不改变角色边界、玩法坐标或网络状态。
    /// </summary>
    public sealed class CameraHorizontalLookAhead2D : MonoBehaviour
    {
        [SerializeField] private PlayerControlAssignment _assignment;
        [SerializeField, Min(0f)] private float _maximumOffset = 0.45f;
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _velocityDeadZone = 0.08f;

        private Vector3 _restPosition;
        private float _currentOffset;
        private float _smoothVelocity;

        private void Awake()
        {
            _restPosition = transform.position;
            if (_assignment == null)
            {
                Debug.LogError(
                    $"[{nameof(CameraHorizontalLookAhead2D)}] " +
                    "未配置玩家控制分配组件。",
                    this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            float targetOffset = CalculateTargetOffset();
            _currentOffset = Mathf.SmoothDamp(
                _currentOffset,
                targetOffset,
                ref _smoothVelocity,
                _smoothTime,
                Mathf.Infinity,
                Time.deltaTime);

            Vector3 position = _restPosition;
            position.x += _currentOffset;
            transform.position = position;
        }

        private void OnDisable()
        {
            transform.position = _restPosition;
            _currentOffset = 0f;
            _smoothVelocity = 0f;
        }

        private float CalculateTargetOffset()
        {
            PlayerActor actor = _assignment.CurrentLocalPlayerActor;
            PlayerMovementMotor2D motor = actor != null
                ? actor.MovementMotor
                : null;
            if (motor == null || motor.MaximumSpeed <= 0f)
            {
                return 0f;
            }

            float normalizedVelocity = Mathf.Clamp(
                motor.Velocity.x / motor.MaximumSpeed,
                -1f,
                1f);
            if (Mathf.Abs(normalizedVelocity) < _velocityDeadZone)
            {
                normalizedVelocity = 0f;
            }

            return normalizedVelocity * _maximumOffset;
        }
    }
}
