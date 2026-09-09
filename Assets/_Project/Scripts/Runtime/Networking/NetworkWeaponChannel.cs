using System.IO;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>技能状态与一次性开火事件分开。束体共用主机的几何快照，客人不会执行伤害查询。</summary>
    public sealed class NetworkWeaponChannel : MonoBehaviour
    {
        private const byte STATE = 36, LASER = 37, WAVE = 38, BLOCK = 39;
        public CoopSessionController Session;
        public NetworkSpriteCatalog Catalog;
        public HarnessTerminalLaserController Laser;
        public HarnessMeleeController Melee;
        public DeepSeekRiceGuardController Guard;
        public DeepSleep.Runtime.Combat.Weapons.Harness.Presentation.HarnessTerminalLaserPresenter LaserView;
        public HarnessMeleePresenter2D MeleeView;
        private float _next;
        private uint _stateSequence, _lastState, _eventSequence, _lastEvent;
        private bool _hasState, _hasEvent;
        private void OnEnable()
        {
            Session.AuthorityMessage += Read; Session.SessionOpened += Reset;
            Laser.FireRequested += Fire; Melee.WaveRequested += Wave; Guard.ChargeConsumed += Block;
        }
        private void OnDisable()
        {
            Session.AuthorityMessage -= Read; Session.SessionOpened -= Reset;
            Laser.FireRequested -= Fire; Melee.WaveRequested -= Wave; Guard.ChargeConsumed -= Block;
        }
        private void Reset(bool authority)
        {
            _stateSequence = _lastState = _eventSequence = _lastEvent = 0; _hasState = _hasEvent = false;
            LaserView.CharacterPoseExternallyDriven = MeleeView.CharacterPoseExternallyDriven = !authority;
        }
        private void LateUpdate()
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f / Session.Config.SnapshotRate;
            Session.SendAuthority(STATE, w =>
            {
                w.Write(++_stateSequence); w.Write((byte)Laser.State); w.Write(Laser.RemainingStateSeconds);
                w.Write(Laser.StateProgress01); w.Write(Laser.HasAimPoint);
                Laser.TryGetSelectedTargetPosition(out var target); Vec(w, target);
                w.Write(Melee.IsMelee); w.Write(Melee.IsSwinging); w.Write(Melee.SwingSequence);
                w.Write(Melee.Attack == null ? 0 : Catalog.GetId(Melee.Attack.CharacterPose));
                w.Write(Melee.Progress); w.Write(Melee.AimDegrees); w.Write(Melee.ModeRemaining); w.Write(Melee.CooldownRemaining);
                w.Write(Guard.IsActive); w.Write(Guard.IsWarning); w.Write(Guard.RemainingCharges);
                w.Write((float)Guard.RemainingSeconds); w.Write((float)Guard.CooldownRemaining);
            }, true);
        }
        private void Fire(HarnessTerminalLaserFireRequest request)
        {
            if (!Session.IsAuthority || request == null || !request.IsValid) return;
            Session.SendAuthority(LASER, w =>
            {
                w.Write(++_eventSequence); var b = request.BeamSnapshot;
                w.Write(b.Sequence); Vec(w, b.SourceOrigin); Vec(w, b.AimDirection); w.Write(b.TargetLayers.value);
                Vec(w, request.PrimaryTargetPosition); w.Write((byte)b.LaneCount);
                for (int i = 0; i < b.LaneCount; i++)
                {
                    var lane = b.GetLane(i); w.Write(lane.LaneIndex); Vec(w, lane.Origin); Vec(w, lane.Direction);
                    w.Write(lane.Length); w.Write(lane.Width); w.Write(lane.PrimaryTargetDamage); w.Write(lane.PiercingDamage);
                }
            }, true);
        }
        private void Wave(HarnessMeleeAttackConfig attack, Vector2 origin, float angle)
        {
            if (!Session.IsAuthority) return;
            Session.SendAuthority(WAVE, w => { w.Write(++_eventSequence); w.Write(Catalog.GetId(attack.CharacterPose));
                Vec(w, origin); w.Write(angle); }, true);
        }
        private void Block(PlayerDamageReceiver2D target, DamagePacket packet, int remaining)
        {
            if (!Session.IsAuthority) return;
            Session.SendAuthority(BLOCK, w => { w.Write(++_eventSequence); w.Write(packet.Amount);
                Vec(w, packet.HitPoint); Vec(w, packet.Direction); w.Write(remaining); }, true);
        }
        private void Read(byte kind, BinaryReader r)
        {
            if (kind < STATE || kind > BLOCK) return;
            uint seq = r.ReadUInt32();
            if (kind == STATE)
            {
                if (_hasState && !RemoteCommandSource.IsNewer(seq, _lastState)) return;
                var state = (HarnessTerminalLaserState)r.ReadByte(); float seconds = r.ReadSingle(), progress = r.ReadSingle();
                bool aiming = r.ReadBoolean(); Vector2 target = Vec(r);
                bool melee = r.ReadBoolean(), swinging = r.ReadBoolean(); uint swing = r.ReadUInt32(), poseId = r.ReadUInt32();
                float swingProgress = r.ReadSingle(), aim = r.ReadSingle(), duration = r.ReadSingle(), cooldown = r.ReadSingle();
                bool active = r.ReadBoolean(), warning = r.ReadBoolean(); int charges = r.ReadInt32();
                float guardSeconds = r.ReadSingle(), guardCooldown = r.ReadSingle();
                Laser.ApplyReplicaState(state, seconds, progress, aiming, target);
                Melee.ApplyReplicaState(melee, swinging, swing, ResolveAttack(poseId), swingProgress, aim, duration, cooldown);
                Guard.ApplyReplicaState(active, warning, charges, guardSeconds, guardCooldown);
                _hasState = true; _lastState = seq; return;
            }
            if (_hasEvent && !RemoteCommandSource.IsNewer(seq, _lastEvent)) return;
            switch (kind)
            {
                case LASER:
                    uint shot = r.ReadUInt32(); Vector2 origin = Vec(r), direction = Vec(r); int layers = r.ReadInt32();
                    Vector2 primary = Vec(r); int count = r.ReadByte(); if (count < 1 || count > 32) return;
                    var lanes = new BeamLaneSnapshot[count];
                    for (int i = 0; i < count; i++) lanes[i] = new BeamLaneSnapshot(r.ReadInt32(), Vec(r), Vec(r),
                        r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    Laser.PresentReplicaFire(new HarnessTerminalLaserFireRequest(Laser.gameObject, null, primary,
                        new BeamFireSnapshot(shot, origin, direction, layers, lanes))); break;
                case WAVE:
                    var attack = ResolveAttack(r.ReadUInt32()); Vector2 waveOrigin = Vec(r); float angle = r.ReadSingle();
                    Melee.PresentReplicaWave(attack, waveOrigin, angle); break;
                case BLOCK:
                    var packet = new DamagePacket(r.ReadSingle(), Vec(r), Vec(r), null); int remaining = r.ReadInt32();
                    Guard.PresentReplicaBlock(packet, remaining); break;
            }
            _hasEvent = true; _lastEvent = seq;
        }
        private HarnessMeleeAttackConfig ResolveAttack(uint poseId)
        {
            if (poseId == 0) return null;
            foreach (var attack in Melee.Config.Attacks) if (Catalog.GetId(attack.CharacterPose) == poseId) return attack;
            return null;
        }
        private static void Vec(BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        private static Vector2 Vec(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle());
    }
}
