using System;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// 终端激光的校准线和锁定环视图。
    /// </summary>
    [Serializable]
    public sealed class HarnessLaserTargetingView2D
    {
        [SerializeField] private BeamTiledMeshView2D _aimGuide;
        [SerializeField] private SpriteRenderer _targetReticle;

        public void Render(
            Vector2 beamOrigin,
            Vector2 targetPosition,
            HarnessTerminalLaserState state,
            float stateProgress,
            HarnessTerminalLaserPresentationConfig config,
            float presentationTime)
        {
            if (state == HarnessTerminalLaserState.Calibrating)
            {
                Vector2 guideOffset = targetPosition - beamOrigin;
                float guideTextureOffset =
                    presentationTime *
                    config.AimGuideTextureScrollWorldUnitsPerSecond /
                    config.AimGuideTextureRepeatWorldLength;
                _aimGuide.Show(
                    beamOrigin,
                    guideOffset,
                    guideOffset.magnitude,
                    config.AimGuideWidth,
                    config.AimGuideTextureRepeatWorldLength,
                    guideTextureOffset,
                    config.AimGuideColor);
                ShowReticle(
                    targetPosition,
                    config.CalibratingReticleColor,
                    stateProgress,
                    config,
                    presentationTime);
                return;
            }

            _aimGuide.Hide();

            if (state == HarnessTerminalLaserState.Cooldown)
            {
                ShowReticle(
                    targetPosition,
                    config.QueuedReticleColor,
                    0f,
                    config,
                    presentationTime);
                return;
            }

            _targetReticle.enabled = false;
        }

        public void Hide()
        {
            _aimGuide?.Hide();

            if (_targetReticle != null)
            {
                _targetReticle.enabled = false;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_aimGuide == null)
            {
                reason = "未配置校准引导线。";
                return false;
            }

            if (!_aimGuide.TryValidateConfiguration(out reason))
            {
                reason = $"校准引导线无效：{reason}";
                return false;
            }

            if (_targetReticle == null || _targetReticle.sprite == null)
            {
                reason = "未配置锁定环渲染器或贴图。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void ShowReticle(
            Vector2 targetPosition,
            Color color,
            float calibrationProgress,
            HarnessTerminalLaserPresentationConfig config,
            float presentationTime)
        {
            float pulsePhase =
                presentationTime *
                config.ReticlePulseCyclesPerSecond *
                Mathf.PI * 2f;
            float pulse = (Mathf.Sin(pulsePhase) + 1f) * 0.5f;
            float diameterMultiplier =
                1f + pulse * config.ReticlePulseScale;
            float progressScale = Mathf.Lerp(
                config.ReticleStartScaleMultiplier,
                1f,
                Mathf.Clamp01(calibrationProgress));

            WorldSpriteGeometry2D.ShowWithWorldDiameter(
                _targetReticle,
                targetPosition,
                config.ReticleWorldDiameter *
                diameterMultiplier *
                progressScale,
                presentationTime *
                config.ReticleRotationDegreesPerSecond,
                color);
        }
    }
}
