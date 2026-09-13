using UnityEngine;
namespace DeepSleep.Runtime.Presentation.DamageNumbers
{
    /// <summary>
    /// 一名角色造成伤害时的跳字视觉参数。任意数值由角色专属 Sprite
    /// 字形逐位拼接，表现配置不进入伤害结算。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_DamageNumberStyle_",
        menuName = "DeepSleep/Presentation/Damage Number Style")]
    public sealed class DamageNumberStyleConfig : ScriptableObject
    {
        [Header("Glyphs")]
        [SerializeField] private Sprite[] _digits = new Sprite[10];
        [SerializeField] private Sprite _decimalPoint;
        [SerializeField, Min(1f)] private float _glyphHeightPixels = 54f;
        [SerializeField] private float _glyphSpacingPixels = -3f;
        [SerializeField, Range(0.5f, 2f)] private float _visualScale = 1f;
        [SerializeField, Range(0f, 2f)] private float _displayScale = 1f;
        [SerializeField, Range(1f, 3f)] private float _decimalScale = 1.6f;
        [SerializeField, Min(0f)] private float _decimalSpacingPixels = 1f;
        [SerializeField, Range(0f, 1f)] private float _decimalBaselineFromTop01 = 378f / 440f;

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float _durationSeconds = 0.72f;
        [SerializeField, Range(0f, 1f)] private float _fadeStart01 = 0.48f;
        [SerializeField] private Vector2 _screenOffset = new(0f, 20f);
        [SerializeField, Min(0f)] private float _horizontalScatterPixels = 12f;
        [SerializeField] private float _risePixels = 54f;
        [SerializeField] private Vector2 _rotationRangeDegrees = new(-3f, 3f);
        [SerializeField, Min(0.01f)] private float _startScale = 0.72f;
        [SerializeField, Min(0.01f)] private float _peakScale = 1.12f;
        [SerializeField, Min(0.01f)] private float _endScale = 0.94f;
        [SerializeField, Range(0.01f, 0.99f)] private float _peakTime01 = 0.18f;

        public float GlyphHeightPixels => _glyphHeightPixels;
        public float GlyphSpacingPixels => _glyphSpacingPixels;
        public float VisualScale => _visualScale;
        public float DisplayScale => Mathf.Clamp(_displayScale, 0f, 2f);
        public float DecimalScale => _decimalScale;
        public void SetSizePercent(float percent) =>
            _displayScale = Mathf.Clamp(percent * 0.01f, 0f, 2f);

        public float GetGap(char left, char right) =>
            left == '.' || right == '.' ? _decimalSpacingPixels : _glyphSpacingPixels;

        // Decimal artwork shares the 440px canvas and a 378px baseline.
        // Enlarge about that baseline instead of the canvas centre.
        public float GetGlyphHeight(char character) =>
            _glyphHeightPixels * (character == '.' ? _decimalScale : 1f);
        public float GetGlyphOffsetY(char character) => character == '.'
            ? (_decimalScale - 1f) * _glyphHeightPixels * (_decimalBaselineFromTop01 - 0.5f)
            : 0f;
        public float DurationSeconds => _durationSeconds;
        public float FadeStart01 => _fadeStart01;
        public Vector2 ScreenOffset => _screenOffset;
        public float HorizontalScatterPixels => _horizontalScatterPixels;
        public float RisePixels => _risePixels;
        public Vector2 RotationRangeDegrees => _rotationRangeDegrees;
        public float StartScale => _startScale;
        public float PeakScale => _peakScale;
        public float EndScale => _endScale;
        public float PeakTime01 => _peakTime01;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            if (character == '.')
            {
                glyph = _decimalPoint;
                return glyph != null;
            }

            int index = character - '0';
            glyph = index >= 0 && index < _digits.Length
                ? _digits[index]
                : null;
            return glyph != null;
        }

        public bool TryValidate(out string reason)
        {
            if (_digits == null || _digits.Length != 10 ||
                _decimalPoint == null)
            {
                reason = "数字字形必须完整配置为 0-9 和小数点。";
                return false;
            }

            for (int index = 0; index < _digits.Length; index++)
            {
                if (_digits[index] == null)
                {
                    reason = $"数字 {index} 的字形为空。";
                    return false;
                }
            }

            if (_glyphHeightPixels <= 0f || _visualScale <= 0f)
            {
                reason = "字形高度或视觉缩放无效。";
                return false;
            }

            if (_displayScale < 0f || _displayScale > 2f ||
                _decimalScale < 1f || _decimalScale > 3f ||
                _decimalSpacingPixels < 0f)
            {
                reason = "显示倍率应在0%—200%，小数点倍率应在1—3，标点间距不能为负。";
                return false;
            }

            if (_durationSeconds <= 0f || _peakTime01 <= 0f ||
                _peakTime01 >= 1f || _startScale <= 0f ||
                _peakScale <= 0f || _endScale <= 0f)
            {
                reason = "跳字动画参数无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
