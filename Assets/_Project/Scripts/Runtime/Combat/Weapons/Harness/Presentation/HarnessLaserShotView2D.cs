using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>复用已装配的束体/炮口模板；每轮连射保留自己的世界快照与淡出计时。</summary>
    [Serializable]
    public sealed class HarnessLaserShotView2D
    {
        [SerializeField] private BeamTiledMeshView2D[] _beamLaneViews;
        [SerializeField] private SpriteRenderer _muzzle;
        private readonly List<BeamTiledMeshView2D> _lanes = new();
        private readonly List<SpriteRenderer> _muzzles = new();
        private readonly List<Shot> _shots = new();
        private sealed class Shot { public BeamFireSnapshot Snapshot; public float Elapsed; }
        public bool IsPlaying => _shots.Count > 0;

        private void Initialize()
        {
            if (_lanes.Count > 0) return;
            _lanes.AddRange(_beamLaneViews); _muzzles.Add(_muzzle);
        }
        private BeamTiledMeshView2D Lane(int index)
        {
            while (_lanes.Count <= index)
                _lanes.Add(UnityEngine.Object.Instantiate(_beamLaneViews[0], _beamLaneViews[0].transform.parent));
            return _lanes[index];
        }
        private SpriteRenderer Muzzle(int index)
        {
            while (_muzzles.Count <= index)
                _muzzles.Add(UnityEngine.Object.Instantiate(_muzzle, _muzzle.transform.parent));
            return _muzzles[index];
        }
        public void Play(HarnessTerminalLaserFireRequest request, UnityEngine.Object diagnosticContext)
        {
            Initialize();
            _shots.Add(new Shot { Snapshot = request.BeamSnapshot });
        }
        public bool Tick(float dt, HarnessTerminalLaserPresentationConfig config)
        {
            Initialize(); bool finished = false; int laneIndex = 0, muzzleIndex = 0;
            if (!IsPlaying) return false;
            for (int s = 0; s < _shots.Count;)
            {
                var shot = _shots[s]; shot.Elapsed += Mathf.Max(0f, dt);
                if (shot.Elapsed >= config.BeamTotalSeconds)
                { _shots.RemoveAt(s); finished = true; continue; }
                float alpha = config.EvaluateBeamAlpha(shot.Elapsed);
                var color = WorldSpriteGeometry2D.WithMultipliedAlpha(config.BeamColor, alpha);
                float scroll = shot.Elapsed * config.BeamTextureScrollWorldUnitsPerSecond / config.BeamTextureRepeatWorldLength;
                for (int i = 0; i < shot.Snapshot.LaneCount; i++)
                {
                    var lane = shot.Snapshot.GetLane(i);
                    Lane(laneIndex++).ShowLayered(lane, config.BeamTextureRepeatWorldLength, scroll, color);
                    // 分叉来自敌人；只有主束才生成实体发射装置。
                    if (!lane.IsBranch)
                        WorldSpriteGeometry2D.ShowWithWorldDiameter(Muzzle(muzzleIndex++), lane.Origin,
                            config.MuzzleWorldDiameter, WorldSpriteGeometry2D.DirectionToAngle(lane.Direction),
                            WorldSpriteGeometry2D.WithMultipliedAlpha(config.MuzzleColor, alpha));
                }
                s++;
            }
            for (int i = laneIndex; i < _lanes.Count; i++) _lanes[i].Hide();
            for (int i = muzzleIndex; i < _muzzles.Count; i++) _muzzles[i].enabled = false;
            return finished && !IsPlaying;
        }
        public void Stop()
        {
            Initialize(); _shots.Clear();
            foreach (var lane in _lanes) lane.Hide();
            foreach (var muzzle in _muzzles) muzzle.enabled = false;
        }
        public void ShowChargingMuzzle(Vector2 origin, Vector2 direction, float progress,
            HarnessTerminalLaserPresentationConfig config, int ports = 1, float spacing = .45f)
        {
            float t = Mathf.Clamp01(progress);
            ShowArray(origin, direction, ports, spacing,
                config.MuzzleWorldDiameter * Mathf.Lerp(config.MuzzleChargingStartScaleMultiplier, 1f, t),
                WorldSpriteGeometry2D.WithMultipliedAlpha(config.MuzzleColor,
                    Mathf.Lerp(config.MuzzleChargingStartAlpha, 1f, t)));
        }
        public void ShowQueuedMuzzle(Vector2 origin, Vector2 direction,
            HarnessTerminalLaserPresentationConfig config, int ports = 1, float spacing = .45f)
        {
            ShowArray(origin, direction, ports, spacing,
                config.MuzzleWorldDiameter * config.QueuedMuzzleScaleMultiplier,
                WorldSpriteGeometry2D.WithMultipliedAlpha(config.MuzzleColor, config.QueuedMuzzleAlpha));
        }
        private void ShowArray(Vector2 origin, Vector2 targetDelta, int ports, float spacing, float diameter, Color color)
        {
            if (IsPlaying || targetDelta.sqrMagnitude < .00001f) return;
            Initialize(); Vector2 dir = targetDelta.normalized, normal = new(-dir.y, dir.x);
            for (int i = 0; i < ports; i++)
            {
                Vector2 position = origin + normal * ((i - (ports - 1) * .5f) * spacing);
                WorldSpriteGeometry2D.ShowWithWorldDiameter(Muzzle(i), position, diameter,
                    WorldSpriteGeometry2D.DirectionToAngle(origin + targetDelta - position), color);
            }
            for (int i = ports; i < _muzzles.Count; i++) _muzzles[i].enabled = false;
        }
        public void HideMuzzleIfIdle()
        {
            if (IsPlaying) return;
            Initialize(); foreach (var muzzle in _muzzles) muzzle.enabled = false;
        }
        public bool TryValidateConfiguration(out string reason)
        {
            if (_beamLaneViews == null || _beamLaneViews.Length == 0 || _muzzle == null || _muzzle.sprite == null)
            { reason = "束体模板或炮口未配置。"; return false; }
            foreach (var lane in _beamLaneViews)
            {
                if (lane == null) { reason = "束体模板为空。"; return false; }
                if (!lane.TryValidateConfiguration(out reason)) return false;
            }
            reason = string.Empty; return true;
        }
    }
}
