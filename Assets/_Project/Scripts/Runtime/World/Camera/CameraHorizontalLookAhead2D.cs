using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.World.Cameras
{
    /// <summary>
    /// 根据本机所控角色的水平速度产生轻微镜头前视，并统一合成本地受击平移。
    /// 这是纯本地表现，不改变角色边界、玩法坐标或网络状态。
    /// </summary>
    public sealed class CameraHorizontalLookAhead2D : MonoBehaviour
    {
        [SerializeField] private PlayerControlAssignment _assignment;
        [SerializeField, Min(0f)] private float _maximumOffset = 0.45f;
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _velocityDeadZone = 0.08f;

        [Header("本机受击表现（不参与瞄准）")]
        [SerializeField, Range(0f, 0.06f)] private float _hitAmplitude = 0.045f;
        [SerializeField, Min(0.01f)] private float _hitDuration = 0.16f;
        [SerializeField, Range(0f, 1f)] private float _hitShakeStrength = 1f;

        private Vector3 _restPosition;
        private float _currentOffset;
        private float _smoothVelocity;
        private Vector2 _hitDirection;
        private float _hitElapsed;
        private bool _hitActive;
        private Vector3 _presentationOffset;

        /// <summary>当前已应用的纯受击偏移；不包含原有水平前视。</summary>
        public Vector3 PresentationOffset => _presentationOffset;

        /// <summary>保留原有水平前视、剥离受击表现的镜头位置。</summary>
        public Vector3 StablePosition => transform.position - _presentationOffset;

        /// <summary>本地舒适度强度。设为零立即清除当前震动；不改变玩法或网络数据。</summary>
        public float ShakeStrength
        {
            get => _hitShakeStrength;
            set
            {
                _hitShakeStrength = Mathf.Clamp01(value);
                if (_hitShakeStrength <= 0f) ResetHit();
            }
        }

        /// <summary>用新的一次受击替换当前短震动，不累加振幅。</summary>
        public void PlayHit(Vector2 direction)
        {
            if (!isActiveAndEnabled || _hitShakeStrength <= 0f) return;
            _hitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _hitElapsed = 0f;
            _hitActive = true;
        }

        /// <summary>停止受击表现并立即撤去已应用偏移，原有前视保持不变。</summary>
        public void ResetHit()
        {
            transform.position -= _presentationOffset;
            _presentationOffset = Vector3.zero;
            _hitActive = false;
            _hitElapsed = 0f;
        }

        /// <summary>保持真实相机投影与前视，只从射线原点扣除受击平移，避免震动改动瞄准。</summary>
        public Ray ScreenPointToStableRay(Camera camera, Vector2 screenPosition)
        {
            Ray ray = camera.ScreenPointToRay(screenPosition);
            // 显式装配只能引用本相机，错误引用不能把另一个相机的震动扣进来。
            if (camera.transform == transform) ray.origin -= _presentationOffset;
            return ray;
        }

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
            UpdatePresentation(Time.deltaTime);
        }

        private void UpdatePresentation(float deltaTime)
        {
            // 暂停冻结前视和震动，不用未缩放时间；零 dt 也避免 SmoothDamp 的除零分支。
            if (deltaTime > 0f)
            {
                float targetOffset = CalculateTargetOffset();
                _currentOffset = Mathf.SmoothDamp(
                    _currentOffset,
                    targetOffset,
                    ref _smoothVelocity,
                    _smoothTime,
                    Mathf.Infinity,
                    deltaTime);

                _hitElapsed += deltaTime;
                if (_hitActive && _hitElapsed >= _hitDuration) _hitActive = false;
                float t = Mathf.Clamp01(_hitElapsed / Mathf.Max(0.01f, _hitDuration));
                // 固定两个衰减来回：这是响应曲线，不另加随机偏移，保证振幅不会相加越界。
                float decay = (1f - t) * (1f - t);
                float displacement = _hitActive
                    ? Mathf.Clamp(_hitAmplitude, 0f, 0.06f) * Mathf.Clamp01(_hitShakeStrength) * decay * Mathf.Cos(t * Mathf.PI * 4f)
                    : 0f;
                _presentationOffset = (Vector3)(_hitDirection * displacement);
            }

            Vector3 position = _restPosition;
            position.x += _currentOffset;
            transform.position = position + _presentationOffset;
        }

        private void OnDisable()
        {
            ResetHit();
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
