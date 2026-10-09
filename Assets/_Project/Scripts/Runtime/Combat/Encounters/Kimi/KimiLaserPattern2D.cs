using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    public enum KimiLaserState : byte { Idle, Charging, Firing, Recovery, Complete }

    /// <summary>连续锁向激光：每轮蓄力首帧锁定，预警和伤害共用几何；每束对每人至多伤害一次。</summary>
    public sealed class KimiLaserPattern2D : MonoBehaviour, IFixedSimulationStep
    {
        public KimiLaserConfig Config;
        public KimiBoss2D Boss;
        public Transform Muzzle;
        public BeamTiledMeshView2D Warning, Beam;
        public BeamTiledMeshView2D[] BranchWarnings, BranchBeams;
        public int RayCount { get; private set; } = 1;
        public SpriteRenderer Focus;
        public SpriteRenderer TargetMarker;
        public Vector2 MarkerPosition { get; private set; }
        public int FiredShots { get; private set; }
        public OneShotSpriteEffectPool2D HitEffects;
        public KimiLaserState State { get; private set; }
        public BeamLaneSnapshot Lane { get; private set; }
        public float Elapsed { get; private set; }
        public event Action Completed;
        public event Action Fired;
        public event Action ChargeStarted;
        private CoopSessionController _session;
        private readonly BeamHitResolver2D _resolver = new();
        private readonly List<BeamResolvedHit2D> _hits = new(8);
        private readonly HashSet<IDamageReceiver> _struck = new(2);
        private ulong _attackId;
        private bool _replica;
        private DamageHitbox2D[] _targets;
        private readonly PlayerLifeStateController2D[] _lives = new PlayerLifeStateController2D[2];
        private Transform _markedTarget;
        private int _nextTarget;
        private Vector2 _fixedTarget;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config == null || Boss == null || Muzzle == null || Warning == null || Beam == null || Focus == null ||
                TargetMarker == null || TargetMarker.sprite == null || HitEffects == null)
            { reason = "激光配置、本体/炮口、两个网格、聚光Sprite及命中池必须显式装配。"; return false; }
            if (BranchWarnings == null || BranchBeams == null || BranchWarnings.Length != Config.PhaseTwoRayCount-1 || BranchBeams.Length != BranchWarnings.Length)
            { reason = "二阶段分叉预警/束体必须预先装配。"; return false; }
            for (int i=0;i<BranchWarnings.Length;i++)
                if (BranchWarnings[i] == null || BranchBeams[i] == null || !BranchWarnings[i].TryValidateConfiguration(out reason) || !BranchBeams[i].TryValidateConfiguration(out reason))
                { reason = "激光分叉网格配置缺失。"; return false; }
            return Config.TryValidate(out reason) && Warning.TryValidateConfiguration(out reason) &&
                Beam.TryValidateConfiguration(out reason) && HitEffects.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason)) { Debug.LogError("[KimiLaser] " + reason, this); enabled = false; return; }
            Cancel();
        }

        /// <summary>首轮按选择器目标；后续轮换存活角色，每轮蓄力中保持射线方向不变。</summary>
        public bool Begin(Vector2 targetPosition, CoopSessionController session, DamageHitbox2D[] targets = null, int firstTarget = 0)
        {
            if (!isActiveAndEnabled || !Boss.IsAlive || !float.IsFinite(targetPosition.x) || !float.IsFinite(targetPosition.y) ||
                (session != null && session.Phase != SessionPhase.Offline && !session.IsAuthority)) return false;
            Vector2 origin = Muzzle.position, direction = targetPosition - origin;
            if (direction.sqrMagnitude < .000001f) return false;
            if (targets != null && (targets.Length != 2 || targets[0] == null || targets[1] == null || firstTarget < 0 || firstTarget > 1)) return false;
            Cancel(); _session = session; _targets = targets; _nextTarget = firstTarget; _fixedTarget = targetPosition;
            if (_targets != null) for (int i = 0; i < 2; i++) _targets[i].TryGetComponent(out _lives[i]);
            return StartCharge();
        }

        private bool StartCharge()
        {
            Vector2 target = _fixedTarget; _markedTarget = null;
            if (_targets != null)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int i = (_nextTarget + attempt) % 2;
                    if (!_targets[i].IsActiveTarget || (_lives[i] != null && _lives[i].State != PlayerLifeState.Alive)) continue;
                    _markedTarget = _targets[i].transform; target = _markedTarget.position; _nextTarget = (i + 1) % 2; break;
                }
                if (_markedTarget == null) { Finish(); return false; }
            }
            Vector2 origin = Muzzle.position, direction = target - origin;
            if (direction.sqrMagnitude < .000001f) { Finish(); return false; }
            MarkerPosition = target;
            RayCount = Boss.PhaseTwo ? Config.PhaseTwoRayCount : 1;
            Lane = new BeamLaneSnapshot(0, origin, direction, Config.Length, Config.DamageWidth, Config.Damage, Config.Damage);
            _struck.Clear(); _attackId = DamageAttackIdAllocator.Next(); Elapsed = 0;
            State = KimiLaserState.Charging; Boss.SetPose(KimiPose.LaserCharge); Render(); ChargeStarted?.Invoke(); return true;
        }
        private void Finish()
        { State = KimiLaserState.Complete; _markedTarget = null; Boss.SetPose(KimiPose.Idle); Render(); Completed?.Invoke(); }

        public void Simulate(float dt)
        {
            if (_replica || !isActiveAndEnabled || dt <= 0 || !float.IsFinite(dt) || State == KimiLaserState.Idle || State == KimiLaserState.Complete) return;
            if (!Boss.IsAlive || (_session != null && _session.Phase != SessionPhase.Offline && !_session.IsAuthority)) { Cancel(); return; }
            // 消费跨阶段余量，长帧既不延长蓄力也不跳过发射伤害。
            float remaining = dt;
            while (remaining > 0 && State != KimiLaserState.Complete)
            {
                float duration = State == KimiLaserState.Charging ? Config.ChargeSeconds :
                    State == KimiLaserState.Firing ? Config.FireSeconds + (RayCount - 1) * Config.PhaseTwoRayDelaySeconds : Config.RecoverySeconds;
                float step = Mathf.Min(remaining, Mathf.Max(0, duration - Elapsed));
                float previousElapsed = Elapsed;
                Elapsed += step; remaining -= step;
                if (State == KimiLaserState.Firing) ResolveDamage(previousElapsed, Elapsed);
                if (Elapsed < duration) break;
                Elapsed = 0;
                if (State == KimiLaserState.Charging)
                { State = KimiLaserState.Firing; FiredShots++; Boss.SetPose(KimiPose.LaserRelease); Fired?.Invoke(); ResolveDamage(0, 0); }
                else if (State == KimiLaserState.Firing) State = KimiLaserState.Recovery;
                else if (FiredShots < Config.ShotCount) { if (!StartCharge()) return; }
                else { Finish(); return; }
            }
            Render();
        }

        private void ResolveDamage(float from, float to)
        {
            for (int i=0;i<RayCount;i++)
            {
                float start = GetRayStart(i);
                if (to >= start && from < start + Config.FireSeconds) ResolveLane(GetLane(i));
            }
        }

        /// <summary>按世界Y方向排序，保证朝左或朝右都从屏幕上方向下递进；客机共用同一算法。</summary>
        public float GetRayStart(int index)
        {
            float height = GetLane(index).Direction.y;
            int rank = 0;
            for (int i = 0; i < RayCount; i++)
            {
                float other = GetLane(i).Direction.y;
                if (other > height || (other == height && i < index)) rank++;
            }
            return rank * Config.PhaseTwoRayDelaySeconds;
        }

        /// <summary>0是中心，随后左右成对展开；预警、束体和伤害使用同一快照。</summary>
        public BeamLaneSnapshot GetLane(int index)
        {
            if (index < 0 || index >= RayCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (index == 0) return Lane;
            int step=(index+1)/2;
            float angle=step * Config.PhaseTwoRaySpacingDegrees * (index%2==1 ? -1 : 1);
            Vector2 direction=Quaternion.Euler(0,0,angle)*Lane.Direction;
            return new BeamLaneSnapshot(index,Lane.Origin,direction,Lane.Length,Lane.Width,Lane.PrimaryTargetDamage,Lane.PrimaryTargetDamage);
        }

        private void ResolveLane(BeamLaneSnapshot lane)
        {
            _resolver.Resolve(lane, Config.PlayerLayers, null, _hits);
            foreach (var hit in _hits)
            {
                if (!hit.Hitbox.TryGetReceiver(out var receiver) || _struck.Contains(receiver)) continue;
                var packet = new DamagePacket(lane.PrimaryTargetDamage, hit.HitPoint, lane.Direction,
                    Boss.gameObject, _attackId, DamageInterceptionPolicy.Blockable);
                if (hit.Hitbox.TryReceiveDamage(in packet))
                { _struck.Add(receiver); HitEffects.TryPlay(hit.HitPoint, lane.RotationDegrees); }
            }
        }

        private void Render()
        {
            Warning.Hide(); Beam.Hide(); Focus.enabled = false;
            foreach (var warning in BranchWarnings) warning.Hide();
            foreach (var beam in BranchBeams) beam.Hide();
            TargetMarker.enabled = State == KimiLaserState.Charging;
            if (TargetMarker.enabled)
            {
                // The beam direction and marker share the position captured by StartCharge.
                // Moving after that lock must evade both the warning and the eventual beam.
                TargetMarker.transform.SetPositionAndRotation(MarkerPosition, Quaternion.Euler(0, 0, Elapsed * Config.TargetMarkerSpinDegrees));
                float width = TargetMarker.sprite.bounds.size.x;
                TargetMarker.transform.localScale = Vector3.one * (Config.TargetMarkerDiameter / width);
            }
            if (State != KimiLaserState.Charging && State != KimiLaserState.Firing) return;
            Focus.enabled = true;
            float progress = State == KimiLaserState.Charging ? Mathf.Clamp01(Elapsed / Config.ChargeSeconds) : 1;
            // 锁定束线方向作为终点，蓄力由偏角转正；发射阶段保持终点，不依赖归零的阶段计时。
            float focusAngle=Lane.RotationDegrees+Config.FocusAlignedAngleOffsetDegrees+
                Mathf.Lerp(Config.FocusStartAngleOffsetDegrees,0,Mathf.SmoothStep(0,1,progress));
            Focus.transform.SetPositionAndRotation(Lane.Origin, Quaternion.Euler(0, 0, focusAngle));
            Focus.transform.localScale = Vector3.one * Mathf.Lerp(Config.FocusStartScale, Config.FocusEndScale, progress);
            Focus.color = new Color(1, 1, 1, Mathf.Lerp(.4f, 1, progress));
            for (int i=0;i<RayCount;i++)
            {
                var lane=GetLane(i);
                float start = GetRayStart(i);
                if (State == KimiLaserState.Charging || Elapsed < start)
                    (i==0 ? Warning : BranchWarnings[i-1]).Show(lane.Origin, lane.Direction, lane.Length, lane.Width, Config.TextureRepeatLength, 0, Config.WarningColor);
                else if (Elapsed < start + Config.FireSeconds)
                    (i==0 ? Beam : BranchBeams[i-1]).Show(lane.Origin, lane.Direction, lane.Length, Config.VisualWidth,
                        Config.TextureRepeatLength, (Elapsed - start) * Config.TextureScrollSpeed, Config.BeamColor);
            }
        }

        public void Cancel()
        {
            _replica = false;
            State = KimiLaserState.Idle; Elapsed = 0; Lane = default; _struck.Clear(); _hits.Clear();
            FiredShots = 0; _targets = null; _markedTarget = null; MarkerPosition = default;
            RayCount = 1;
            if (BranchWarnings != null) foreach(var warning in BranchWarnings) if(warning != null) warning.Hide();
            if (BranchBeams != null) foreach(var beam in BranchBeams) if(beam != null) beam.Hide();
            if (TargetMarker != null) TargetMarker.enabled = false;
            if (Warning != null) Warning.Hide(); if (Beam != null) Beam.Hide(); if (Focus != null) Focus.enabled = false;
        }
        public void ApplyReplica(KimiLaserState state, float elapsed, Vector2 origin, Vector2 direction, Vector2 markerPosition = default)
        {
            _replica = true; State = state; Elapsed = elapsed;
            RayCount = Boss.PhaseTwo ? Config.PhaseTwoRayCount : 1;
            MarkerPosition = markerPosition;
            Lane = new BeamLaneSnapshot(0, origin, direction, Config.Length, Config.DamageWidth, 0, 0);
            Render();
        }
        private void OnDisable() => Cancel();
    }
}
