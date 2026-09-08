using DeepSleep.Runtime.Presentation.Poses;
using UnityEngine;

namespace DeepSleep.Runtime.Players.LifeCycle
{
    /// <summary>
    /// 将当前角色姿态复制为跟随实体平移的残影，并立即显示独立睡眠贴图。
    /// 不负责决定玩家何时宕机。
    /// </summary>
    public sealed class PlayerDownedVisual2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _characterRenderer;
        [SerializeField] private SpriteRenderer _ghostRenderer;
        [SerializeField] private Sprite _downedSprite;
        [SerializeField] private PlayerDownedVisualConfig _config;
        [SerializeField, Min(0.01f), Tooltip("存活姿态相对 Visual 初始缩放的倍率。")]
        private float _aliveScaleMultiplier = 1f;
        [SerializeField, Min(0.01f), Tooltip("倒地姿态相对 Visual 初始缩放的倍率。")]
        private float _downedScaleMultiplier = 1f;

        private readonly SpritePoseGhost2D _ghost = new();
        private Sprite _aliveSprite;
        private Vector3 _baseVisualScale;
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
            _baseVisualScale = _characterRenderer.transform.localScale;
            ApplyPoseScale(_aliveScaleMultiplier);
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

        /// <summary>
        /// 必须在玩法组件清理姿态之前调用，保存死亡瞬间的贴图与世界姿态。
        /// </summary>
        public void CaptureCurrentPose()
        {
            if (_isInitialized)
            {
                _ghost.Capture(_characterRenderer, _ghostRenderer,
                    _config.GhostStartAlpha, _config.GhostFadeSeconds);
            }
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
            ApplyPoseScale(_downedScaleMultiplier);
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
            ApplyPoseScale(_aliveScaleMultiplier);
        }

        private void ApplyPoseScale(float multiplier)
        {
            // 总是从初始尺寸计算，重复切换不会累乘；Z 保持原值。
            _characterRenderer.transform.localScale = new Vector3(
                _baseVisualScale.x * multiplier,
                _baseVisualScale.y * multiplier,
                _baseVisualScale.z);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (float.IsNaN(_aliveScaleMultiplier) || float.IsInfinity(_aliveScaleMultiplier) ||
                float.IsNaN(_downedScaleMultiplier) || float.IsInfinity(_downedScaleMultiplier) ||
                _aliveScaleMultiplier <= 0f || _downedScaleMultiplier <= 0f)
            {
                reason = "姿态缩放倍率必须为有限正数。";
                return false;
            }

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

    }
}
