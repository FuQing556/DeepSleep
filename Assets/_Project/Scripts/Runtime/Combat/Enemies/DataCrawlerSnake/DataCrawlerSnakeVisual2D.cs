using UnityEngine;
using DeepSleep.Runtime.Presentation.Poses;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    /// <summary>
    /// 只负责数据蛇的贴图、水平朝向与炮口显隐。
    /// 攻击状态机通过 SetAiming 驱动它，不直接操纵渲染器。
    /// </summary>
    public sealed class DataCrawlerSnakeVisual2D : MonoBehaviour
    {
        [SerializeField] private DataCrawlerSnakeMotor2D _motor;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite _idleSprite;
        [SerializeField] private Sprite _aimSprite;
        [SerializeField] private GameObject _muzzleView;
        [SerializeField] private Transform _muzzleAnchor;
        [SerializeField] private SpritePoseTransition2D _poseTransition;

        private float _absoluteVisualScaleX;
        private float _absoluteMuzzleOffsetX;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DataCrawlerSnakeVisual2D)}] " +
                    $"表现装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _absoluteVisualScaleX =
                Mathf.Abs(_visualRoot.localScale.x);
            _absoluteMuzzleOffsetX =
                Mathf.Abs(_muzzleAnchor.localPosition.x);
            _isInitialized = true;
            _poseTransition.ResetTo(_idleSprite);
            SetAiming(false);
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                SetAiming(false);
            }
        }

        // 炮口位置参与弹体生成，必须在攻击模拟前同步，不能依赖渲染帧。
        public void SynchronizeFacing()
        {
            if (!_isInitialized)
            {
                return;
            }

            Vector2 direction = _motor.TravelDirection;
            if (Mathf.Abs(direction.x) < 0.001f)
            {
                return;
            }

            Vector3 scale = _visualRoot.localScale;
            scale.x = direction.x >= 0f
                ? _absoluteVisualScaleX
                : -_absoluteVisualScaleX;
            _visualRoot.localScale = scale;

            Vector3 muzzlePosition = _muzzleAnchor.localPosition;
            muzzlePosition.x = direction.x >= 0f
                ? _absoluteMuzzleOffsetX
                : -_absoluteMuzzleOffsetX;
            _muzzleAnchor.localPosition = muzzlePosition;
        }

        public void SetAiming(bool isAiming)
        {
            if (!_isInitialized)
            {
                return;
            }

            _poseTransition.TransitionTo(
                isAiming ? _aimSprite : _idleSprite);
            _muzzleView.SetActive(isAiming);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_motor == null || _visualRoot == null ||
                _renderer == null)
            {
                reason = "未配置运动、视觉根节点或精灵渲染器。";
                return false;
            }

            if (_idleSprite == null || _aimSprite == null)
            {
                reason = "未配置常态与瞄准贴图。";
                return false;
            }

            if (_muzzleView == null || _muzzleAnchor == null)
            {
                reason = "未配置蓄能炮口对象或独立炮口锚点。";
                return false;
            }

            if (_poseTransition == null)
            {
                reason = "未配置通用姿态残影组件。";
                return false;
            }

            if (!_poseTransition.TryValidateConfiguration(out reason))
            {
                reason = $"姿态残影无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
