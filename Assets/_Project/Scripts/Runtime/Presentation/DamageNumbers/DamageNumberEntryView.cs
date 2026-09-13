using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Presentation.DamageNumbers
{
    /// <summary>一条池化伤害跳字；始终把世界命中点投影到当前屏幕。</summary>
    public sealed class DamageNumberEntryView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Image[] _glyphImages;

        private DamageNumberStyleConfig _style;
        private RectTransform _canvasRect;
        private Camera _worldCamera;
        private Camera _uiCamera;
        private Vector2 _worldPosition;
        private Vector2 _randomOffset;
        private float _rotationDegrees;
        private float _elapsedSeconds;
        private bool _isPlaying;

        public event Action<DamageNumberEntryView> Finished;
        public bool IsPlaying => _isPlaying;

        public bool TryValidateConfiguration(out string reason)
        {
            if (_rectTransform == null || _glyphImages == null ||
                _glyphImages.Length == 0)
            {
                reason = "跳字预制体引用不完整。";
                return false;
            }

            foreach (Image image in _glyphImages)
            {
                if (image == null)
                {
                    reason = "跳字预制体存在空字形槽。";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public void Play(
            Vector2 worldPosition,
            float amount,
            DamageNumberStyleConfig style,
            RectTransform canvasRect,
            Camera worldCamera,
            Camera uiCamera,
            float horizontalRandom01,
            float rotationRandom01)
        {
            _style = style;
            _canvasRect = canvasRect;
            _worldCamera = worldCamera;
            _uiCamera = uiCamera;
            _worldPosition = worldPosition;
            _randomOffset = new Vector2(
                Mathf.Lerp(-style.HorizontalScatterPixels,
                    style.HorizontalScatterPixels, horizontalRandom01),
                0f);
            _rotationDegrees = Mathf.Lerp(
                style.RotationRangeDegrees.x,
                style.RotationRangeDegrees.y,
                rotationRandom01);
            _elapsedSeconds = 0f;
            _isPlaying = true;

            ConfigureGlyphs(FormatAmount(amount));

            gameObject.SetActive(true);
            Render(0f);
        }

        public void PrepareForPool()
        {
            _isPlaying = false;
            _elapsedSeconds = 0f;
            _style = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_isPlaying)
            {
                return;
            }

            _elapsedSeconds += Mathf.Max(0f, Time.deltaTime);
            float progress = Mathf.Clamp01(
                _elapsedSeconds / _style.DurationSeconds);
            Render(progress);

            if (progress >= 1f)
            {
                _isPlaying = false;
                Finished?.Invoke(this);
            }
        }

        private void Render(float progress)
        {
            Vector3 screenPoint = _worldCamera.WorldToScreenPoint(
                new Vector3(_worldPosition.x, _worldPosition.y, 0f));
            Vector2 canvasPoint = default;
            bool visible = screenPoint.z >= 0f;
            if (visible)
            {
                visible = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    _uiCamera,
                    out canvasPoint);
            }

            if (!visible)
            {
                SetGlyphsVisible(false);
                return;
            }

            SetGlyphsVisible(true);
            float easedRise = 1f - Mathf.Pow(1f - progress, 2f);
            _rectTransform.anchoredPosition = canvasPoint +
                _style.ScreenOffset + _randomOffset +
                Vector2.up * (_style.RisePixels * easedRise);
            _rectTransform.localRotation = Quaternion.Euler(
                0f, 0f, _rotationDegrees * (1f - progress));

            float animatedScale = progress <= _style.PeakTime01
                ? Mathf.Lerp(
                    _style.StartScale,
                    _style.PeakScale,
                    Mathf.SmoothStep(0f, 1f,
                        progress / _style.PeakTime01))
                : Mathf.Lerp(
                    _style.PeakScale,
                    _style.EndScale,
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(
                            _style.PeakTime01, 1f, progress)));
            _rectTransform.localScale = Vector3.one *
                (animatedScale * _style.VisualScale * _style.DisplayScale);

            float alpha = 1f - Mathf.InverseLerp(
                _style.FadeStart01, 1f, progress);
            foreach (Image image in _glyphImages)
            {
                if (image.gameObject.activeSelf)
                {
                    SetGraphicAlpha(image, alpha);
                }
            }
        }

        private void ConfigureGlyphs(string value)
        {
            int count = Mathf.Min(value.Length, _glyphImages.Length);
            float[] widths = new float[count];
            float totalWidth = 0f;

            for (int index = 0; index < _glyphImages.Length; index++)
            {
                Image image = _glyphImages[index];
                Sprite glyph = null;
                bool active = index < count &&
                    _style.TryGetGlyph(value[index], out glyph);
                image.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                image.sprite = glyph;
                image.color = Color.white;
                float aspect = glyph.rect.width / glyph.rect.height;
                widths[index] = _style.GetGlyphHeight(value[index]) * aspect;
                totalWidth += widths[index];
                if (index > 0)
                    totalWidth += _style.GetGap(value[index - 1], value[index]);
            }

            float cursor = -totalWidth * 0.5f;
            for (int index = 0; index < count; index++)
            {
                Image image = _glyphImages[index];
                if (!image.gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform glyphRect = image.rectTransform;
                glyphRect.sizeDelta = new Vector2(
                    widths[index], _style.GetGlyphHeight(value[index]));
                glyphRect.anchoredPosition = new Vector2(
                    cursor + widths[index] * 0.5f, _style.GetGlyphOffsetY(value[index]));
                cursor += widths[index];
                if (index + 1 < count)
                    cursor += _style.GetGap(value[index], value[index + 1]);
            }
        }

        private void SetGlyphsVisible(bool visible)
        {
            foreach (Image image in _glyphImages)
            {
                image.enabled = visible;
            }
        }

        private static string FormatAmount(float amount)
        {
            float rounded = Mathf.Round(amount);
            return Mathf.Abs(amount - rounded) < 0.01f
                ? rounded.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                : amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
