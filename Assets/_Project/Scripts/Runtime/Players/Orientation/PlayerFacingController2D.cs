using UnityEngine;

namespace DeepSleep.Runtime.Players.Orientation
{
    /// <summary>
    /// 保存玩家角色的玩法朝向，并把朝向变化应用到专用视觉节点。
    /// 目标选择系统只提交朝向决定，不直接修改贴图或发射点。
    /// </summary>
    public sealed class PlayerFacingController2D : MonoBehaviour
    {
        [SerializeField] private Transform _facingRoot;
        [SerializeField] private FacingDirection _initialDirection =
            FacingDirection.Right;

        private Vector3 _baseLocalScale;
        private FacingDirection _currentDirection;
        private bool _isInitialized;

        public FacingDirection CurrentDirection => _currentDirection;

        public Vector2 Forward => _currentDirection == FacingDirection.Left
            ? Vector2.left
            : Vector2.right;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerFacingController2D)}] " +
                    $"角色朝向装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _baseLocalScale = _facingRoot.localScale;
            _isInitialized = true;
            SetDirection(_initialDirection);
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                SetDirection(_currentDirection);
            }
        }

        public void SetDirection(FacingDirection direction)
        {
            if (!_isInitialized || !IsValidDirection(direction))
            {
                return;
            }

            _currentDirection = direction;

            Vector3 scale = _baseLocalScale;
            scale.x = Mathf.Abs(_baseLocalScale.x) * (int)direction;
            _facingRoot.localScale = scale;
        }

        public void ResetToInitialDirection()
        {
            SetDirection(_initialDirection);
        }

        private void OnDisable()
        {
            if (!_isInitialized || _facingRoot == null)
            {
                return;
            }

            _currentDirection = _initialDirection;
            _facingRoot.localScale = _baseLocalScale;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_facingRoot == null)
            {
                reason = "未配置朝向根节点。";
                return false;
            }

            if (Mathf.Approximately(_facingRoot.localScale.x, 0f))
            {
                reason = "朝向根节点的 X 缩放不能为 0。";
                return false;
            }

            if (!IsValidDirection(_initialDirection))
            {
                reason = "初始朝向不是有效的左或右。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool IsValidDirection(FacingDirection direction)
        {
            return direction == FacingDirection.Right ||
                direction == FacingDirection.Left;
        }
    }
}
