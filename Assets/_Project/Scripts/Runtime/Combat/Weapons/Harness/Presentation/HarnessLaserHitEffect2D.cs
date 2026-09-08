using System;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;
using UnityEngine.Serialization;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// 一个可复用的命中特效实例。播放期间锁定命中时的世界坐标。
    /// </summary>
    public sealed class HarnessLaserHitEffect2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [FormerlySerializedAs("_hitEffectScaleSettings")]
        [SerializeField]
        private CombatEffectScaleSettings _effectScaleSettings;

        private HarnessTerminalLaserPresentationConfig _config;
        private Vector2 _worldPosition;
        private float _rotationDegrees;
        private float _worldDiameter;
        private float _rotationDeltaDegrees;
        private float _elapsedSeconds;
        private bool _isPlaying;

        public event Action<HarnessLaserHitEffect2D> Finished;

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
                _elapsedSeconds / _config.HitEffectDurationSeconds);
            Render(progress);

            if (progress >= 1f)
            {
                Complete();
            }
        }

        public void Play(
            in HarnessTerminalLaserHitConfirmed hit,
            HarnessTerminalLaserPresentationConfig config,
            float initialRotationOffsetDegrees,
            float rotationDeltaDegrees)
        {
            PlayAt(hit.HitPoint, hit.Direction, hit.BeamWidth, config,
                initialRotationOffsetDegrees, rotationDeltaDegrees);
        }

        public void PlayAt(Vector2 position, Vector2 direction, float width,
            HarnessTerminalLaserPresentationConfig config, float initialRotationOffsetDegrees,
            float rotationDeltaDegrees)
        {
            _config = config;
            _worldPosition = position;
            _rotationDegrees =
                WorldSpriteGeometry2D.DirectionToAngle(direction) +
                initialRotationOffsetDegrees;
            _worldDiameter = Mathf.Max(
                config.HitEffectMinimumWorldDiameter,
                width * config.HitEffectBeamWidthMultiplier);
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
                reason = "未配置命中特效渲染器与贴图。";
                return false;
            }

            if (_effectScaleSettings == null)
            {
                reason = "未配置玩家特效倍率。";
                return false;
            }

            if (!_effectScaleSettings.TryValidate(out reason))
            {
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
                _config.HitEffectStartScaleMultiplier,
                _config.HitEffectEndScaleMultiplier,
                progress);
            float fadeProgress = Mathf.InverseLerp(
                _config.HitEffectFadeStart01,
                1f,
                progress);
            Color color =
                WorldSpriteGeometry2D.WithMultipliedAlpha(
                    _config.HitEffectColor,
                    1f - fadeProgress);
            float remainingRotation = 1f - progress;
            float easedRotationProgress =
                1f - remainingRotation * remainingRotation *
                remainingRotation;

            WorldSpriteGeometry2D.ShowWithWorldDiameter(
                _renderer,
                _worldPosition,
                _worldDiameter * scaleMultiplier *
                _effectScaleSettings.PlayerEffectScale,
                _rotationDegrees +
                _rotationDeltaDegrees * easedRotationProgress,
                color);
        }

        private void Complete()
        {
            _isPlaying = false;
            _renderer.enabled = false;
            Finished?.Invoke(this);
        }
    }
}
