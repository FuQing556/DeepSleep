using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Effects
{
    /// <summary>
    /// 单张精灵一次性特效的纯表现参数。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_OneShotSpriteEffect_",
        menuName = "DeepSleep/配置/表现/一次性精灵特效")]
    public sealed class OneShotSpriteEffectConfig : ScriptableObject
    {
        [Header("时间与尺寸")]
        [SerializeField, Min(0.01f)] private float _durationSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float _worldDiameter = 2f;
        [SerializeField, Min(0.01f)] private float _startScaleMultiplier = 0.6f;
        [SerializeField, Min(0.01f)] private float _endScaleMultiplier = 1.1f;
        [SerializeField, Range(0f, 1f)] private float _fadeStart01 = 0.25f;

        [Header("旋转与颜色")]
        [SerializeField] private float _baseRotationOffsetDegrees;
        [SerializeField, Min(0f)] private float _minimumRotationDegrees = 15f;
        [SerializeField, Min(0f)] private float _maximumRotationDegrees = 35f;
        [SerializeField] private Color _color = Color.white;

        [Header("对象池")]
        [SerializeField, Min(1)] private int _prewarmCount = 4;
        [SerializeField, Min(1)] private int _maximumCount = 12;

        public float DurationSeconds => _durationSeconds;
        public float WorldDiameter => _worldDiameter;
        public float StartScaleMultiplier => _startScaleMultiplier;
        public float EndScaleMultiplier => _endScaleMultiplier;
        public float FadeStart01 => _fadeStart01;
        public float BaseRotationOffsetDegrees => _baseRotationOffsetDegrees;
        public float MinimumRotationDegrees => _minimumRotationDegrees;
        public float MaximumRotationDegrees => _maximumRotationDegrees;
        public Color Color => _color;
        public int PrewarmCount => _prewarmCount;
        public int MaximumCount => _maximumCount;

        public bool TryValidate(out string reason)
        {
            if (_durationSeconds <= 0f || _worldDiameter <= 0f ||
                _startScaleMultiplier <= 0f || _endScaleMultiplier <= 0f)
            {
                reason = "持续时间、世界尺寸和缩放必须大于 0。";
                return false;
            }

            if (_minimumRotationDegrees < 0f ||
                _maximumRotationDegrees < _minimumRotationDegrees)
            {
                reason = "旋转范围无效。";
                return false;
            }

            if (_prewarmCount < 1 || _maximumCount < _prewarmCount)
            {
                reason = "对象池容量无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
