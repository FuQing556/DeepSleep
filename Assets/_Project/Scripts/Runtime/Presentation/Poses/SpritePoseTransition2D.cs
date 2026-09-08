using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Poses
{
    /// <summary>
    /// 通用单残影姿态切换：新姿态同帧显示，旧姿态跟随实体平移并淡出。
    /// 快速连续切换时复用唯一残影，避免无限叠图。
    /// </summary>
    public sealed class SpritePoseTransition2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _subjectRenderer;
        [SerializeField] private SpriteRenderer _ghostRenderer;
        [SerializeField] private SpritePoseTransitionConfig _config;

        private readonly SpritePoseGhost2D _ghost = new();
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(SpritePoseTransition2D)}] " +
                    $"姿态残影装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _ghostRenderer.enabled = false;
            _isInitialized = true;
        }

        private void LateUpdate()
        {
            if (_isInitialized) _ghost.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            _ghost.Clear();
        }

        public void TransitionTo(Sprite nextSprite)
        {
            if (!_isInitialized || nextSprite == null ||
                _subjectRenderer.sprite == nextSprite)
            {
                return;
            }

            CaptureCurrentPose();
            _subjectRenderer.sprite = nextSprite;
        }

        public void ResetTo(Sprite sprite)
        {
            if (sprite != null)
            {
                _subjectRenderer.sprite = sprite;
            }

            _ghost.Clear();
            if (_ghostRenderer != null)
            {
                _ghostRenderer.enabled = false;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_subjectRenderer == null || _ghostRenderer == null)
            {
                reason = "未配置主体与残影渲染器。";
                return false;
            }

            if (_subjectRenderer == _ghostRenderer)
            {
                reason = "主体与残影不能使用同一个渲染器。";
                return false;
            }

            if (_ghostRenderer.transform.parent == null)
            {
                reason = "残影渲染器必须具有跟随父节点。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置姿态切换参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }

        private void CaptureCurrentPose()
        {
            _ghost.Capture(_subjectRenderer, _ghostRenderer, _config.StartAlpha, _config.FadeSeconds);
        }

    }
}
