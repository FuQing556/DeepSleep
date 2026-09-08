using UnityEngine;

namespace DeepSleep.Runtime.Players.LifeCycle
{
    /// <summary>
    /// 将当前角色姿态冻结为世界空间残影，并立即显示独立睡眠贴图。
    /// 不负责决定玩家何时宕机。
    /// </summary>
    public sealed class PlayerDownedVisual2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _characterRenderer;
        [SerializeField] private SpriteRenderer _ghostRenderer;
        [SerializeField] private Sprite _downedSprite;
        [SerializeField] private PlayerDownedVisualConfig _config;

        private float _ghostFadeRemainingSeconds;
        private Sprite _aliveSprite;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerDownedVisual2D)}] " +
                    $"宕机表现装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _ghostRenderer.enabled = false;
            _aliveSprite = _characterRenderer.sprite;
            _isInitialized = true;
        }

        private void LateUpdate()
        {
            if (!_isInitialized || !_ghostRenderer.enabled)
            {
                return;
            }

            _ghostFadeRemainingSeconds = Mathf.Max(
                0f,
                _ghostFadeRemainingSeconds - Time.deltaTime);

            Color color = _ghostRenderer.color;
            color.a = _config.GhostStartAlpha *
                (_ghostFadeRemainingSeconds / _config.GhostFadeSeconds);
            _ghostRenderer.color = color;

            if (_ghostFadeRemainingSeconds <= 0f)
            {
                _ghostRenderer.enabled = false;
            }
        }

        /// <summary>
        /// 必须在玩法组件清理姿态之前调用，保存死亡瞬间的贴图与世界姿态。
        /// </summary>
        public void CaptureCurrentPose()
        {
            if (!_isInitialized || _characterRenderer.sprite == null)
            {
                return;
            }

            Transform source = _characterRenderer.transform;
            Transform ghost = _ghostRenderer.transform;
            Transform followRoot = ghost.parent;
            ghost.localPosition =
                followRoot.InverseTransformPoint(source.position);
            ghost.localRotation =
                Quaternion.Inverse(followRoot.rotation) * source.rotation;
            Vector3 rootScale = followRoot.lossyScale;
            Vector3 sourceScale = source.lossyScale;
            ghost.localScale = new Vector3(
                DivideScale(sourceScale.x, rootScale.x),
                DivideScale(sourceScale.y, rootScale.y),
                DivideScale(sourceScale.z, rootScale.z));

            _ghostRenderer.sprite = _characterRenderer.sprite;
            _ghostRenderer.flipX = _characterRenderer.flipX;
            _ghostRenderer.flipY = _characterRenderer.flipY;
            Color ghostColor = _characterRenderer.color;
            ghostColor.a *= _config.GhostStartAlpha;
            _ghostRenderer.color = ghostColor;
            _ghostRenderer.enabled = true;
            _ghostFadeRemainingSeconds = _config.GhostFadeSeconds;
        }

        /// <summary>
        /// 在其他姿态组件完成清理后，立即切换到完整不透明的睡眠状态。
        /// </summary>
        public void ShowDownedPose()
        {
            if (!_isInitialized)
            {
                return;
            }

            Color color = _characterRenderer.color;
            color.a = 1f;
            _characterRenderer.color = color;
            _characterRenderer.sprite = _downedSprite;
        }

        /// <summary>
        /// 睡眠姿态成为旧状态残影，初始存活姿态在同一帧恢复。
        /// </summary>
        public void ShowAlivePose()
        {
            if (!_isInitialized)
            {
                return;
            }

            CaptureCurrentPose();
            Color color = _characterRenderer.color;
            color.a = 1f;
            _characterRenderer.color = color;
            _characterRenderer.sprite = _aliveSprite;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_characterRenderer == null || _ghostRenderer == null)
            {
                reason = "未配置角色渲染器或宕机残影渲染器。";
                return false;
            }

            if (_characterRenderer == _ghostRenderer)
            {
                reason = "角色渲染器与宕机残影不能是同一个组件。";
                return false;
            }

            if (_ghostRenderer.transform.parent == null)
            {
                reason = "宕机残影必须具有跟随父节点。";
                return false;
            }

            if (_downedSprite == null)
            {
                reason = "未配置睡眠状态贴图。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置宕机表现参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }

        private static float DivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= Mathf.Epsilon
                ? value
                : value / divisor;
        }
    }
}
