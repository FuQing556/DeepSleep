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
                    State == KimiLaserState.Firing ? Config.FireSeconds : Config.RecoverySeconds;
                float step = Mathf.Min(remaining, Mathf.Max(0, duration - Elapsed));
                Elapsed += step; remaining -= step;
                if (State == KimiLaserState.Firing) ResolveDamage();
                if (Elapsed < duration) break;
                Elapsed = 0;
                if (State == KimiLaserState.Charging)
                { State = KimiLaserState.Firing; FiredShots++; Boss.SetPose(KimiPose.LaserRelease); Fired?.Invoke(); ResolveDamage(); }
                else if (State == KimiLaserState.Firing) State = KimiLaserState.Recovery;
                else if (FiredShots < Config.ShotCount) { if (!StartCharge()) return; }
                else { Finish(); return; }
            }
            Render();
        }

        private void ResolveDamage()
        {
            _resolver.Resolve(Lane, Config.PlayerLayers, null, _hits);
            foreach (var hit in _hits)
            {
                if (!hit.Hitbox.TryGetReceiver(out var receiver) || _struck.Contains(receiver)) continue;
                var packet = new DamagePacket(Lane.PrimaryTargetDamage, hit.HitPoint, Lane.Direction,
                    Boss.gameObject, _attackId, DamageInterceptionPolicy.Blockable);
                if (hit.Hitbox.TryReceiveDamage(in packet))
                { _struck.Add(receiver); HitEffects.TryPlay(hit.HitPoint, Lane.RotationDegrees); }
            }
        }

        private void Render()
        {
            Warning.Hide(); Beam.Hide(); Focus.enabled = false;
            TargetMarker.enabled = State == KimiLaserState.Charging;
            if (TargetMarker.enabled)
            {
                if (!_replica && _markedTarget != null) MarkerPosition = _markedTarget.position;
                TargetMarker.transform.SetPositionAndRotation(MarkerPosition, Quaternion.Euler(0, 0, Elapsed * Config.TargetMarkerSpinDegrees));
                float width = TargetMarker.sprite.bounds.size.x;
                TargetMarker.transform.localScale = Vector3.one * (Config.TargetMarkerDiameter / width);
            }
            if (State != KimiLaserState.Charging && State != KimiLaserState.Firing) return;
            Focus.enabled = true;
            float progress = State == KimiLaserState.Charging ? Mathf.Clamp01(Elapsed / Config.ChargeSeconds) : 1;
            Focus.transform.SetPositionAndRotation(Lane.Origin, Quaternion.Euler(0, 0, Elapsed * Config.FocusSpinDegrees));
            Focus.transform.localScale = Vector3.one * Mathf.Lerp(Config.FocusStartScale, Config.FocusEndScale, progress);
            Focus.color = new Color(1, 1, 1, Mathf.Lerp(.4f, 1, progress));
            if (State == KimiLaserState.Charging)
                Warning.Show(Lane.Origin, Lane.Direction, Lane.Length, Lane.Width, Config.TextureRepeatLength, 0, Config.WarningColor);
            else
                Beam.Show(Lane.Origin, Lane.Direction, Lane.Length, Config.VisualWidth,
                    Config.TextureRepeatLength, Elapsed * Config.TextureScrollSpeed, Config.BeamColor);
        }

        public void Cancel()
        {
            _replica = false;
            State = KimiLaserState.Idle; Elapsed = 0; Lane = default; _struck.Clear(); _hits.Clear();
            FiredShots = 0; _targets = null; _markedTarget = null; MarkerPosition = default;
            if (TargetMarker != null) TargetMarker.enabled = false;
            if (Warning != null) Warning.Hide(); if (Beam != null) Beam.Hide(); if (Focus != null) Focus.enabled = false;
        }
        public void ApplyReplica(KimiLaserState state, float elapsed, Vector2 origin, Vector2 direction, Vector2 markerPosition = default)
        {
            _replica = true; State = state; Elapsed = elapsed;
            MarkerPosition = markerPosition;
            Lane = new BeamLaneSnapshot(0, origin, direction, Config.Length, Config.DamageWidth, 0, 0);
            Render();
        }
        private void OnDisable() => Cancel();
    }
}
