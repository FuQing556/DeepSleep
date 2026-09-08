using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DeepSleep.Runtime.Presentation.Effects
{
    /// <summary>
    /// 在固定世界位置播放一次缩放、旋转和淡出的精灵。
    /// </summary>
    public sealed class OneShotSpriteEffect2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [FormerlySerializedAs("_hitEffectScaleSettings")]
        [SerializeField]
        private CombatEffectScaleSettings _effectScaleSettings;
        [FormerlySerializedAs("_hitEffectTarget")]
        [SerializeField] private CombatEffectSource _effectSource;

        private OneShotSpriteEffectConfig _config;
        private Vector2 _worldPosition;
        private float _baseRotationDegrees;
        private float _rotationDeltaDegrees;
        private float _elapsedSeconds;
        private bool _isPlaying;

        public event Action<OneShotSpriteEffect2D> Finished;

        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!_isPlaying)
            {
                return;
            }

            _elapsedSeconds += Mathf.Max(0f, Time.deltaTime);
            float progress = Mathf.Clamp01(
                _elapsedSeconds / _config.DurationSeconds);
            Render(progress);

            if (progress >= 1f)
            {
                Complete();
            }
        }

        public void Play(
            Vector2 worldPosition,
            float baseRotationDegrees,
            float rotationDeltaDegrees,
            OneShotSpriteEffectConfig config)
        {
            _config = config;
            _worldPosition = worldPosition;
            _baseRotationDegrees =
                baseRotationDegrees + config.BaseRotationOffsetDegrees;
            _rotationDeltaDegrees = rotationDeltaDegrees;
            _elapsedSeconds = 0f;
            _isPlaying = true;
            gameObject.SetActive(true);
            Render(0f);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_renderer == null || _renderer.sprite == null)
            {
                reason = "未配置精灵渲染器与贴图。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void PrepareForPool()
        {
            _isPlaying = false;
            _elapsedSeconds = 0f;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }

            gameObject.SetActive(false);
        }

        private void Render(float progress)
        {
            float scaleMultiplier = Mathf.SmoothStep(
                _config.StartScaleMultiplier,
                _config.EndScaleMultiplier,
                progress);
            float fadeProgress = Mathf.InverseLerp(
                _config.FadeStart01,
                1f,
                progress);
            float easedRotationProgress =
                1f - Mathf.Pow(1f - progress, 3f);
            float spriteDiameter = Mathf.Max(
                _renderer.sprite.bounds.size.x,
                _renderer.sprite.bounds.size.y);
            float scale =
                _config.WorldDiameter / spriteDiameter * scaleMultiplier *
                (_effectScaleSettings != null
                    ? _effectScaleSettings.GetScale(_effectSource)
                    : 1f);
            Color color = _config.Color;
            color.a *= 1f - fadeProgress;

            transform.SetPositionAndRotation(
                _worldPosition,
                Quaternion.Euler(
                    0f,
                    0f,
                    _baseRotationDegrees +
                    _rotationDeltaDegrees * easedRotationProgress));
            transform.localScale = Vector3.one * scale;
            _renderer.color = color;
            _renderer.enabled = true;
        }

        private void Complete()
        {
            _isPlaying = false;
            _renderer.enabled = false;
            Finished?.Invoke(this);
        }
    }
}
