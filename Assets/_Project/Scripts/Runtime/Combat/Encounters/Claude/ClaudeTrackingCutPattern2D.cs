using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    public enum ClaudeTrackingCutState : byte { Idle, Tracking, Locked, Fading, Complete }

    /// <summary>三轮局部斜斩；二阶段双目标交叉斩，刀槽与光标固定复用。</summary>
    public sealed class ClaudeTrackingCutPattern2D : MonoBehaviour, IFixedSimulationStep
    {
        public ClaudeTrackingCutConfig Config;
        public BossDamageBody2D Boss;
        public CoopSessionController Session;
        public SpritePoseTransition2D PoseTransition;
        public Sprite IdlePose, CastPose;
        public DamageHitbox2D[] Targets;
        public Collider2D[] TargetShapes;
        public PlayerLifeStateController2D[] TargetLives;
        public PlayerActionGate[] Gates;
        public PlayerMovementMotor2D[] TargetMotors;
        public SpriteRenderer Marker;
        public SpriteRenderer SecondaryMarker;
        public SpriteRenderer[] Blades;
        public SpriteRenderer[] CrossBlades;
        public OneShotSpriteEffectPool2D HitEffects;
        private readonly BeamHitResolver2D _resolver = new();
        private readonly List<BeamResolvedHit2D> _hits = new();
        private readonly HashSet<IDamageReceiver> _victims = new();
        private readonly BeamLaneSnapshot[] _firedLanes = new BeamLaneSnapshot[6];
        private readonly float[] _firedAt = new float[6];
        private readonly bool[] _lastPositiveSlope = { true, true };
        private System.Random _random;
        private float _clock, _phaseAge, _markerRotation;
        private int _total;
        private bool _replica;
        private bool _dual;
        public int SecondaryTargetIndex { get; private set; } = -1;
        public Vector2 SecondaryMarkerPosition { get; private set; }
        public BeamLaneSnapshot SecondaryLane { get; private set; }
        public int TargetIndex { get; private set; } = -1;
        public int ShotsFired { get; private set; }
        public int DamageApplications { get; private set; }
        public Vector2 MarkerPosition { get; private set; }
        public BeamLaneSnapshot Lane { get; private set; }
        public ClaudeTrackingCutState State { get; private set; }
        public float LockedSecondsRemaining => State == ClaudeTrackingCutState.Locked
            ? Mathf.Max(0, Config.LockedSeconds - _phaseAge) : 0;
        public event Action Fired, Completed;
        private bool HasAuthority => Session == null || Session.Phase == SessionPhase.Offline || Session.IsAuthority;
        private bool Active => State == ClaudeTrackingCutState.Tracking || State == ClaudeTrackingCutState.Locked || State == ClaudeTrackingCutState.Fading;
        public bool TryValidateConfiguration(out string reason)
        {
            reason = "追踪切割显式引用缺失。";
            if (Config == null || !Config.TryValidate(out reason) || Boss == null ||
                PoseTransition == null || IdlePose == null || CastPose == null || HitEffects == null ||
                Marker == null || Marker.sprite == null || SecondaryMarker == null || SecondaryMarker.sprite == null || Blades == null || Blades.Length != 6 || CrossBlades == null || CrossBlades.Length != 6 ||
                Targets == null || Targets.Length != 2 || TargetShapes == null || TargetShapes.Length != 2 ||
                TargetLives == null || TargetLives.Length != 2 || Gates == null || Gates.Length != 2 ||
                TargetMotors == null || TargetMotors.Length != 2) return false;
            for (int i = 0; i < 6; i++) if (Blades[i] == null || Blades[i].sprite == null) return false;
            for (int i = 0; i < 6; i++) if (CrossBlades[i] == null || CrossBlades[i].sprite == null) return false;
            for (int i = 0; i < 2; i++)
                if (Targets[i] == null || TargetShapes[i] == null || TargetLives[i] == null || Gates[i] == null || TargetMotors[i] == null ||
                    (Config.PlayerLayers.value & (1 << TargetShapes[i].gameObject.layer)) == 0) return false;
            if (transform.lossyScale != Vector3.one) { reason = "追踪切割根须保持单位缩放。"; return false; }
            return HitEffects.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out var reason)) { Debug.LogError(reason, this); enabled = false; return; }
            Cancel();
        }
        public bool Begin(bool phaseTwo, int seed)
        {
            if (Active || !isActiveAndEnabled || !HasAuthority || !Boss.IsAlive) return false;
            Cancel(); _total = 3; _dual = phaseTwo; _random = new System.Random(seed);
            if (!StartShot()) { Cancel(); return false; }
            PoseTransition.TransitionTo(CastPose); Render(); return true;
        }
        private bool Eligible(int i) => TargetLives[i].State == PlayerLifeState.Alive &&
            !Gates[i].IsBlocked(PlayerActionBlock.Movement) && Targets[i].IsActiveTarget;
        private BeamLaneSnapshot MakeLane(int i)
        {
            Vector2 direction = new Vector2(1, PositiveSlope(i) ? 1 : -1).normalized;
            Vector2 origin = (Vector2)TargetShapes[i].bounds.center - direction * (Config.Length * .5f);
            return new BeamLaneSnapshot(ShotsFired, origin, direction, Config.Length, Config.DamageWidth, Config.Damage, Config.Damage);
        }
        private bool PositiveSlope(int i)
        {
            // 实际世界速度，包含场景颠倒后的方向；轴向/静止保留最近有效象限。
            Vector2 velocity = TargetMotors[i].Velocity;
            if (Mathf.Abs(velocity.x) <= Config.MovementDirectionEpsilon ||
                Mathf.Abs(velocity.y) <= Config.MovementDirectionEpsilon) return _lastPositiveSlope[i];
            return (velocity.x > 0) == (velocity.y > 0);
        }
        private bool SafeForFrozen(in BeamLaneSnapshot lane)
            => SafeSingleLane(lane) && (!_dual || SafeSingleLane(CrossLane(lane)));
        public static BeamLaneSnapshot CrossLane(in BeamLaneSnapshot lane)
        {
            Vector2 direction = new(-lane.Direction.y, lane.Direction.x);
            Vector2 center = lane.Origin + lane.Direction * (lane.Length * .5f);
            return new BeamLaneSnapshot(lane.LaneIndex, center - direction * (lane.Length * .5f), direction,
                lane.Length, lane.Width, lane.PrimaryTargetDamage, lane.PiercingDamage);
        }
        private bool SafeSingleLane(in BeamLaneSnapshot lane)
        {
            if (!lane.IsValid) return false;
            Vector2 normal = new Vector2(-lane.Direction.y, lane.Direction.x);
            for (int p = 0; p < 2; p++)
            {
                if (TargetLives[p].State != PlayerLifeState.Alive || !Gates[p].IsBlocked(PlayerActionBlock.Movement)) continue;
                Vector2 delta = (Vector2)TargetShapes[p].bounds.center - lane.Origin;
                float along = Vector2.Dot(delta, lane.Direction), r = TargetShapes[p].bounds.extents.magnitude + Config.FrozenClearance;
                if (along > -r && along < lane.Length + r && Mathf.Abs(Vector2.Dot(delta, normal)) < r + lane.Width * .5f) return false;
            }
            return true;
        }
        private bool StartShot()
        {
            int chosen = -1, count = 0;
            for (int i = 0; i < 2; i++)
                if (Eligible(i) && SafeForFrozen(MakeLane(i))) { count++; if (_random.Next(count) == 0) chosen = i; }
            if (chosen < 0) return false;
            TargetIndex = chosen; _phaseAge = _markerRotation = 0; State = ClaudeTrackingCutState.Tracking;
            int other = 1 - chosen;
            SecondaryTargetIndex = _dual && Eligible(other) && SafeForFrozen(MakeLane(other)) ? other : -1;
            SecondaryLane = default;
            Follow(); return true;
        }
        public bool HasEligibleTarget()
        {
            for (int i = 0; i < 2; i++)
                if (Eligible(i) && SafeForFrozen(MakeLane(i))) return true;
            return false;
        }
        private void Follow()
        {
            var actor = Boss.GetComponent<ClaudeBossActor2D>();
            if (actor != null) actor.FaceAt(TargetShapes[TargetIndex].bounds.center);
            _lastPositiveSlope[TargetIndex] = PositiveSlope(TargetIndex);
            Lane = MakeLane(TargetIndex); MarkerPosition = TargetShapes[TargetIndex].bounds.center;
            if (SecondaryTargetIndex >= 0)
            {
                _lastPositiveSlope[SecondaryTargetIndex] = PositiveSlope(SecondaryTargetIndex);
                SecondaryLane = MakeLane(SecondaryTargetIndex);
                SecondaryMarkerPosition = TargetShapes[SecondaryTargetIndex].bounds.center;
            }
        }
        public void Simulate(float dt)
        {
            if (_replica || !Active || dt <= 0 || !float.IsFinite(dt) || !isActiveAndEnabled) return;
            if (!HasAuthority || !Boss.IsAlive) { Cancel(); return; }
            float remaining = dt;
            while (remaining > 0 && Active)
            {
                if (State != ClaudeTrackingCutState.Fading &&
                    (!Eligible(TargetIndex) || !SafeForFrozen(State == ClaudeTrackingCutState.Tracking ? MakeLane(TargetIndex) : Lane)))
                { if (!StartShot()) { Finish(); return; } }
                if (State != ClaudeTrackingCutState.Fading && SecondaryTargetIndex >= 0 &&
                    (!Eligible(SecondaryTargetIndex) || !SafeForFrozen(State == ClaudeTrackingCutState.Tracking ? MakeLane(SecondaryTargetIndex) : SecondaryLane)))
                { SecondaryTargetIndex = -1; SecondaryLane = default; }
                float duration = State == ClaudeTrackingCutState.Tracking ? Config.TrackingSeconds :
                    State == ClaudeTrackingCutState.Locked ? Config.LockedSeconds : Config.FadeSeconds;
                float step = Mathf.Min(remaining, Mathf.Max(0, duration - _phaseAge));
                _clock += step; _phaseAge += step; remaining -= step;
                if (State == ClaudeTrackingCutState.Tracking) { Follow(); _markerRotation += step * Config.MarkerSpin; }
                if (_phaseAge < duration) break;
                _phaseAge = 0;
                if (State == ClaudeTrackingCutState.Tracking) State = ClaudeTrackingCutState.Locked;
                else if (State == ClaudeTrackingCutState.Locked)
                {
                    _firedLanes[ShotsFired] = Lane; _firedAt[ShotsFired] = _clock;
                    _victims.Clear(); ulong id = DamageAttackIdAllocator.Next(); ApplyDamage(Lane, id);
                    if(_dual) ApplyDamage(CrossLane(Lane),id);
                    if (SecondaryTargetIndex >= 0)
                    {
                        _firedLanes[3 + ShotsFired] = SecondaryLane; _firedAt[3 + ShotsFired] = _clock;
                        ApplyDamage(SecondaryLane, id);
                        if(_dual) ApplyDamage(CrossLane(SecondaryLane),id);
                    }
                    if (!Active || !Boss.IsAlive) { Cancel(); return; }
                    ShotsFired++; Fired?.Invoke(); if (!Active) return;
                    if (ShotsFired < _total) { if (!StartShot()) { Finish(); return; } }
                    else { State = ClaudeTrackingCutState.Fading; PoseTransition.TransitionTo(IdlePose); }
                }
                else { Finish(); return; }
            }
            Render();
        }
        private void ApplyDamage(BeamLaneSnapshot lane, ulong id)
        {
            _resolver.Resolve(lane, Config.PlayerLayers, null, _hits);
            foreach (var h in _hits)
                for (int p = 0; p < 2; p++)
                    if (h.Hitbox == Targets[p] && TargetLives[p].State == PlayerLifeState.Alive &&
                        h.Hitbox.TryGetReceiver(out var receiver) && _victims.Add(receiver))
                    {
                        if (h.Hitbox.TryReceiveDamage(new DamagePacket(Config.Damage, h.HitPoint, lane.Direction, gameObject,
                            id, DamageInterceptionPolicy.Blockable)))
                            HitEffects.TryPlay(h.HitPoint, lane.RotationDegrees);
                        DamageApplications++;
                    }
        }
        private void Render()
        {
            bool warning = State == ClaudeTrackingCutState.Tracking || State == ClaudeTrackingCutState.Locked;
            Marker.enabled = warning;
            SecondaryMarker.enabled = warning && SecondaryTargetIndex >= 0;
            if (SecondaryMarker.enabled)
            {
                SecondaryMarker.transform.SetPositionAndRotation(SecondaryMarkerPosition, Quaternion.Euler(0, 0, _markerRotation));
                SecondaryMarker.transform.localScale = Vector3.one * (Config.MarkerDiameter / SecondaryMarker.sprite.bounds.size.x);
                SecondaryMarker.color = new Color(1, 1, 1, State == ClaudeTrackingCutState.Locked ? 1 : .75f);
            }
            if (warning)
            {
                Marker.transform.SetPositionAndRotation(MarkerPosition, Quaternion.Euler(0, 0, _markerRotation));
                Marker.transform.localScale = Vector3.one * (Config.MarkerDiameter / Marker.sprite.bounds.size.x);
                Marker.color = new Color(1, 1, 1, State == ClaudeTrackingCutState.Locked ? 1 : .75f);
            }
            for (int i = 0; i < 6; i++)
            {
                float alpha = i % 3 < ShotsFired && _firedLanes[i].IsValid ? Mathf.Clamp01(1 - (_clock - _firedAt[i]) / Config.FadeSeconds) : 0;
                RenderBlade(Blades[i],_firedLanes[i],alpha);
                RenderBlade(CrossBlades[i],CrossLane(_firedLanes[i]),_dual ? alpha : 0);
            }
        }
        private void RenderBlade(SpriteRenderer visual, BeamLaneSnapshot lane, float alpha)
        {
                visual.enabled = alpha > 0;
                if (alpha <= 0) return;
                var sprite = visual.sprite;
                Vector2 size = sprite.rect.size, start = Vector2.Scale(Config.BladeAxisStart, size) - sprite.pivot;
                Vector2 axis = Vector2.Scale(Config.BladeAxisEnd - Config.BladeAxisStart, size);
                float scale = lane.Length * sprite.pixelsPerUnit / axis.magnitude;
                float angle = lane.RotationDegrees - Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0, 0, angle);
                visual.transform.SetPositionAndRotation((Vector3)lane.Origin - rotation * (Vector3)(start / sprite.pixelsPerUnit * scale), rotation);
                visual.transform.localScale = Vector3.one * scale; visual.color = new Color(1, 1, 1, alpha);
        }
        private void Finish()
        {
            State = ClaudeTrackingCutState.Complete; TargetIndex = -1; Marker.enabled = false;
            SecondaryTargetIndex = -1; SecondaryMarker.enabled = false;
            foreach (var blade in Blades) blade.enabled = false;
            foreach (var blade in CrossBlades) blade.enabled = false;
            PoseTransition.ResetTo(IdlePose); Completed?.Invoke();
        }
        public void Cancel()
        {
            bool active = Active && !_replica; State = ClaudeTrackingCutState.Idle; TargetIndex = -1;
            _replica = false;
            _dual = false; SecondaryTargetIndex = -1; SecondaryLane = default; SecondaryMarkerPosition = default;
            Array.Clear(_firedLanes, 0, 6); Array.Clear(_firedAt, 0, 6);
            ShotsFired = DamageApplications = 0; _clock = _phaseAge = _markerRotation = 0;
            _lastPositiveSlope[0] = _lastPositiveSlope[1] = true;
            if (Marker != null) Marker.enabled = false;
            if (SecondaryMarker != null) SecondaryMarker.enabled = false;
            if (Blades != null) foreach (var b in Blades) if (b != null) b.enabled = false;
            if (CrossBlades != null) foreach (var b in CrossBlades) if (b != null) b.enabled = false;
            if (active && PoseTransition != null) PoseTransition.ResetTo(IdlePose);
        }
        private void OnDisable() => Cancel();
        public void CaptureSnapshot(ClaudeEncounterSnapshot f)
        {
            f.TrackingState = State; f.TrackingTarget = TargetIndex; f.TrackingShots = ShotsFired;
            f.TrackingClock = _clock; f.TrackingPhaseAge = _phaseAge; f.MarkerRotation = _markerRotation;
            f.MarkerPosition = MarkerPosition; f.TrackingLane = Lane;
            f.SecondaryTrackingTarget = SecondaryTargetIndex; f.SecondaryMarkerPosition = SecondaryMarkerPosition; f.SecondaryTrackingLane = SecondaryLane;
            Array.Copy(_firedLanes, f.TrackingLanes, 6); Array.Copy(_firedAt, f.TrackingFiredAt, 6);
        }
        public void ApplyReplica(ClaudeEncounterSnapshot f)
        {
            _replica = true; _dual = f.PhaseTwo; State = f.TrackingState; TargetIndex = f.TrackingTarget; ShotsFired = f.TrackingShots;
            _clock = f.TrackingClock; _phaseAge = f.TrackingPhaseAge; _markerRotation = f.MarkerRotation;
            MarkerPosition = f.MarkerPosition; Lane = f.TrackingLane;
            SecondaryTargetIndex = f.SecondaryTrackingTarget; SecondaryMarkerPosition = f.SecondaryMarkerPosition; SecondaryLane = f.SecondaryTrackingLane;
            Array.Copy(f.TrackingLanes, _firedLanes, 6); Array.Copy(f.TrackingFiredAt, _firedAt, 6);
            Render(); // 不重新选玩家、不发事件、不做本地伤害。
        }
    }
}
