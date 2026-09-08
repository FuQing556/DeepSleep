using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Poses
{
    /// <summary>
    /// 通用单残影姿态切换：新姿态同帧显示，旧姿态冻结在切换位置淡出。
    /// 快速连续切换时复用唯一残影，避免无限叠图。
    /// </summary>
    public sealed class SpritePoseTransition2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _subjectRenderer;
        [SerializeField] private SpriteRenderer _ghostRenderer;
        [SerializeField] private SpritePoseTransitionConfig _config;

        private float _remainingSeconds;
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
            if (!_isInitialized || !_ghostRenderer.enabled)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(
                0f,
                _remainingSeconds - Mathf.Max(0f, Time.deltaTime));
            Color color = _ghostRenderer.color;
            color.a = _config.StartAlpha *
                (_remainingSeconds / _config.FadeSeconds);
            _ghostRenderer.color = color;

            if (_remainingSeconds <= 0f)
            {
                _ghostRenderer.enabled = false;
            }
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

            _remainingSeconds = 0f;
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
            Sprite previous = _subjectRenderer.sprite;
            if (previous == null)
            {
                return;
            }

            Transform source = _subjectRenderer.transform;
            Transform ghost = _ghostRenderer.transform;
            Transform followRoot = ghost.parent;
            ghost.localPosition =
                followRoot.InverseTransformPoint(source.position);
            ghost.localRotation =
                Quaternion.Inverse(followRoot.rotation) * source.rotation;
            Vector3 rootScale = followRoot.lossyScale;
            Vector3 sourceScale = source.lossyScale;
            ghost.localScale = new Vector3(
                Divide(sourceScale.x, rootScale.x),
                Divide(sourceScale.y, rootScale.y),
                Divide(sourceScale.z, rootScale.z));

            _ghostRenderer.sprite = previous;
            _ghostRenderer.flipX = _subjectRenderer.flipX;
            _ghostRenderer.flipY = _subjectRenderer.flipY;
            Color color = _subjectRenderer.color;
            color.a *= _config.StartAlpha;
            _ghostRenderer.color = color;
            _ghostRenderer.enabled = true;
            _remainingSeconds = _config.FadeSeconds;
        }

        private static float Divide(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= Mathf.Epsilon
                ? value
                : value / divisor;
        }
    }
}
