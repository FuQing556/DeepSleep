using System;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// 一次已裁决终端激光的束体与枪口视图。
    /// 沿线命中特效由后续伤害裁决结果逐命中点播放，不能在此处伪造。
    /// </summary>
    [Serializable]
    public sealed class HarnessLaserShotView2D
    {
        [SerializeField] private BeamTiledMeshView2D[] _beamLaneViews;
        [SerializeField] private SpriteRenderer _muzzle;

        private BeamFireSnapshot _activeSnapshot;
        private float _elapsedSeconds;

        public bool IsPlaying => _activeSnapshot != null;

        public void Play(
            HarnessTerminalLaserFireRequest request,
            UnityEngine.Object diagnosticContext)
        {
            _activeSnapshot = request.BeamSnapshot;
            _elapsedSeconds = 0f;

            if (_beamLaneViews.Length < _activeSnapshot.LaneCount)
            {
                Debug.LogWarning(
                    $"[{nameof(HarnessLaserShotView2D)}] " +
                    $"开火快照包含 {_activeSnapshot.LaneCount} 条束线，" +
                    $"但只配置了 {_beamLaneViews.Length} 个表现通道。",
                    diagnosticContext);
            }
        }

        public bool Tick(
            float deltaTime,
            HarnessTerminalLaserPresentationConfig config)
        {
            if (_activeSnapshot == null)
            {
                return false;
            }

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            float alpha = config.EvaluateBeamAlpha(_elapsedSeconds);

            if (alpha <= 0f &&
                _elapsedSeconds >= config.BeamTotalSeconds)
            {
                Stop();
                return true;
            }

            Color beamColor =
                WorldSpriteGeometry2D.WithMultipliedAlpha(
                    config.BeamColor,
                    alpha);
            float textureOffset =
                _elapsedSeconds *
                config.BeamTextureScrollWorldUnitsPerSecond /
                config.BeamTextureRepeatWorldLength;
            int visibleLaneCount = Mathf.Min(
                _activeSnapshot.LaneCount,
                _beamLaneViews.Length);

            for (int index = 0; index < visibleLaneCount; index++)
            {
                BeamLaneSnapshot lane = _activeSnapshot.GetLane(index);
                _beamLaneViews[index].Show(
                    lane.Origin,
                    lane.Direction,
                    lane.Length,
                    lane.Width,
                    config.BeamTextureRepeatWorldLength,
                    textureOffset,
                    beamColor);
            }

            for (int index = visibleLaneCount;
                 index < _beamLaneViews.Length;
                 index++)
            {
                _beamLaneViews[index].Hide();
            }

            WorldSpriteGeometry2D.ShowWithWorldDiameter(
                _muzzle,
                _activeSnapshot.SourceOrigin,
                config.MuzzleWorldDiameter,
                WorldSpriteGeometry2D.DirectionToAngle(
                    _activeSnapshot.AimDirection),
                WorldSpriteGeometry2D.WithMultipliedAlpha(
                    config.MuzzleColor,
                    alpha));

            return false;
        }

        public void Stop()
        {
            if (_beamLaneViews != null)
            {
                for (int index = 0;
                     index < _beamLaneViews.Length;
                     index++)
                {
                    _beamLaneViews[index]?.Hide();
                }
            }

            if (_muzzle != null)
            {
                _muzzle.enabled = false;
            }

            _activeSnapshot = null;
            _elapsedSeconds = 0f;
        }

        public void ShowChargingMuzzle(
            Vector2 origin,
            Vector2 aimDirection,
            float calibrationProgress,
            HarnessTerminalLaserPresentationConfig config)
        {
            if (IsPlaying || aimDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            float progress = Mathf.Clamp01(calibrationProgress);
            float diameter = config.MuzzleWorldDiameter * Mathf.Lerp(
                config.MuzzleChargingStartScaleMultiplier,
                1f,
                progress);
            Color color = config.MuzzleColor;
            color.a *= Mathf.Lerp(
                config.MuzzleChargingStartAlpha,
                1f,
                progress);

            WorldSpriteGeometry2D.ShowWithWorldDiameter(
                _muzzle,
                origin,
                diameter,
                WorldSpriteGeometry2D.DirectionToAngle(aimDirection),
                color);
        }

        public void ShowQueuedMuzzle(
            Vector2 origin,
            Vector2 aimDirection,
            HarnessTerminalLaserPresentationConfig config)
        {
            if (IsPlaying || aimDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Color color = config.MuzzleColor;
            color.a *= config.QueuedMuzzleAlpha;

            WorldSpriteGeometry2D.ShowWithWorldDiameter(
                _muzzle,
                origin,
                config.MuzzleWorldDiameter *
                config.QueuedMuzzleScaleMultiplier,
                WorldSpriteGeometry2D.DirectionToAngle(aimDirection),
                color);
        }

        public void HideMuzzleIfIdle()
        {
            if (!IsPlaying && _muzzle != null)
            {
                _muzzle.enabled = false;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_beamLaneViews == null || _beamLaneViews.Length == 0)
            {
                reason = "至少需要一个束线表现通道。";
                return false;
            }

            for (int index = 0;
                 index < _beamLaneViews.Length;
                 index++)
            {
                if (_beamLaneViews[index] == null)
                {
                    reason = $"束线表现通道 {index} 为空。";
                    return false;
                }

                if (!_beamLaneViews[index]
                        .TryValidateConfiguration(out string laneReason))
                {
                    reason =
                        $"束线表现通道 {index} 无效：{laneReason}";
                    return false;
                }
            }

            if (_muzzle == null || _muzzle.sprite == null)
            {
                reason = "未配置枪口特效的渲染器与贴图。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
