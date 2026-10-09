using System;
using System.IO;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Encounters;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Run;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>Claude单一完整覆盖通道。先验证全帧再提交；客机不推进攻击、不裁决伤害。</summary>
    public sealed class ClaudeEncounterNetworkChannel : MonoBehaviour
    {
        public CoopSessionController Session;
        public ClaudeChapterEncounterDriver2D ChapterDriver;
        public ClaudeEncounter2D Encounter;
        public BossBarrierFeedback2D Barrier;
        public SpriteHitFlash2D[] BookFlashes;
        // 3刀×20线是本消息容量，不是独立技能平衡参数。
        public const int MinimumPayloadBytes = 436, MaximumPayloadBytes = 1396;
        private readonly ClaudeEncounterSnapshot _outgoing = new(), _incoming = new();
        private uint _frame, _lastReceived;
        private bool _received;
        private float _nextSend;
        private Action<BinaryWriter> _write;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Session == null || ChapterDriver == null || Encounter == null || Barrier == null ||
                ChapterDriver.Session != Session || ChapterDriver.Encounter != Encounter || Encounter.Session != Session ||
                Barrier.Boss != Encounter.Actor.Body || Encounter.SpatialCut.Config.LineCount > ClaudeEncounterSnapshot.MaximumCutLanes ||
                BookFlashes == null || BookFlashes.Length != 4)
            { reason = "Claude会话、章节、遭遇、护罩和消息切线容量须一致。"; return false; }
            for (int i = 0; i < 4; i++)
                if (BookFlashes[i] == null || BookFlashes[i].DamageSource != Encounter.Permissions.Books[i])
                { reason = "权限书短闪必须逐书显式绑定。"; return false; }
            reason = string.Empty; return true;
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out var reason)) { Debug.LogError("[ClaudeNetwork] " + reason, this); enabled = false; return; }
            _write = Write;
        }
        private void OnEnable()
        {
            if (Session == null) return;
            Session.AuthorityMessage += Read; Session.SessionOpened += Open; Session.SessionClosed += Clear;
        }
        private void OnDisable()
        {
            if (Session != null)
            { Session.AuthorityMessage -= Read; Session.SessionOpened -= Open; Session.SessionClosed -= Clear; }
            Clear();
        }
        private void Open(bool authority) => Clear();
        private void LateUpdate()
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 1 / Session.Config.SnapshotRate;
            ++_frame; Session.SendAuthority(NetworkMessageCatalog.Authority.ClaudeSnapshot, _write, true);
        }
        public void Capture(ClaudeEncounterSnapshot f)
        {
            var actor = Encounter.Actor; var effects = ChapterDriver.SceneEffects;
            f.Sequence = _frame; f.TakenOver = ChapterDriver.HasTakenOver; f.EncounterState = Encounter.State;
            f.Shown = actor.Body.IsShown; f.Position = actor.transform.position; f.Health = actor.Body.CurrentHealth;
            f.PhaseTwo = actor.Body.PhaseTwo; f.Pose = actor.Pose;
            f.BarrierHit = Barrier.Sequence; f.BarrierHitAge = Barrier.NormalizedAge;
            f.BossClock = effects.BossClockActive; f.BossStates = effects.BossStatesActive;
            f.BossSeed = effects.BossClockSeed; f.BossElapsed = effects.BossClockElapsed;
            Encounter.Presentation.CaptureSnapshot(f); Encounter.Energy.CaptureSnapshot(f);
            Encounter.SecondaryEnergy.CaptureSnapshot(f,true);
            Encounter.TrackingCut.CaptureSnapshot(f); Encounter.SpatialCut.CaptureSnapshot(f); Encounter.Permissions.CaptureSnapshot(f.Books);
            for (int i = 0; i < 4; i++) { f.BookHits[i] = BookFlashes[i].Sequence; f.BookHitAges[i] = BookFlashes[i].NormalizedAge; }
        }
        private void Write(BinaryWriter writer) { Capture(_outgoing); WriteFrame(writer, _outgoing); }
        public static void WriteFrame(BinaryWriter w, ClaudeEncounterSnapshot f)
        {
            w.Write(f.Sequence); w.Write(f.TakenOver); w.Write((byte)f.EncounterState); w.Write(f.Shown);
            V(w, f.Position); w.Write(f.Health); w.Write(f.PhaseTwo); w.Write((byte)f.Pose);
            w.Write(f.BarrierHit); w.Write(f.BarrierHitAge);
            w.Write((byte)f.PresentationState); w.Write(f.NightAlpha); w.Write(f.FigureAlpha); w.Write(f.RainAlpha); w.Write(f.ShieldAlpha);
            w.Write(f.BossClock); w.Write(f.BossStates); w.Write(f.BossSeed); w.Write(f.BossElapsed);
            w.Write((byte)f.EnergyState); V(w, f.EnergyPosition); V(w, f.EnergyDirection);
            w.Write(f.EnergyAge); w.Write(f.EnergySpinAge); w.Write(f.EnergyHealth);
            w.Write((byte)f.SecondEnergyState); V(w,f.SecondEnergyPosition); V(w,f.SecondEnergyDirection);
            w.Write(f.SecondEnergyAge);w.Write(f.SecondEnergySpinAge);w.Write(f.SecondEnergyHealth);
            w.Write((byte)f.TrackingState); w.Write((byte)(f.TrackingTarget + 1)); w.Write((byte)f.TrackingShots);
            w.Write(f.TrackingClock); w.Write(f.TrackingPhaseAge); w.Write(f.MarkerRotation); V(w, f.MarkerPosition); L(w, f.TrackingLane);
            w.Write((byte)(f.SecondaryTrackingTarget + 1)); V(w, f.SecondaryMarkerPosition); L(w, f.SecondaryTrackingLane);
            for (int i = 0; i < 6; i++) { L(w, f.TrackingLanes[i]); w.Write(f.TrackingFiredAt[i]); }
            w.Write(f.CutRunning); w.Write((byte)f.CutTotal); w.Write((byte)f.CutsFired); w.Write(f.CutAge); w.Write(f.NextCut);
            for (int i = 0; i < 3; i++)
            {
                w.Write((byte)f.CutCounts[i]); w.Write(f.CutAt[i]);
                for (int j = 0; j < f.CutCounts[i]; j++) L(w, f.CutLanes[i][j]);
            }
            for (int i = 0; i < 4; i++)
            {
                var b = f.Books[i]; V(w, b.Position); w.Write((byte)b.Role); w.Write((byte)b.Permission);
                w.Write((byte)b.State); w.Write(b.Health); w.Write(b.RemainingSeconds); w.Write(b.Mirrored);
                w.Write(f.BookHits[i]); w.Write(f.BookHitAges[i]);
            }
        }
        public static void ReadFrame(BinaryReader r, ClaudeEncounterSnapshot f, ClaudeEncounter2D encounter)
        {
            f.Sequence = r.ReadUInt32(); f.TakenOver = r.ReadBoolean(); f.EncounterState = (ClaudeEncounterState)r.ReadByte();
            f.Shown = r.ReadBoolean(); f.Position = V(r); f.Health = r.ReadSingle(); f.PhaseTwo = r.ReadBoolean(); f.Pose = (ClaudePose)r.ReadByte();
            f.BarrierHit = r.ReadUInt32(); f.BarrierHitAge = r.ReadSingle();
            f.PresentationState = (ClaudePresentationState)r.ReadByte(); f.NightAlpha = r.ReadSingle(); f.FigureAlpha = r.ReadSingle(); f.RainAlpha = r.ReadSingle(); f.ShieldAlpha = r.ReadSingle();
            f.BossClock = r.ReadBoolean(); f.BossStates = r.ReadBoolean(); f.BossSeed = r.ReadInt32(); f.BossElapsed = r.ReadSingle();
            f.EnergyState = (ClaudeEnergyState)r.ReadByte(); f.EnergyPosition = V(r); f.EnergyDirection = V(r);
            f.EnergyAge = r.ReadSingle(); f.EnergySpinAge = r.ReadSingle(); f.EnergyHealth = r.ReadSingle();
            f.SecondEnergyState=(ClaudeEnergyState)r.ReadByte();f.SecondEnergyPosition=V(r);f.SecondEnergyDirection=V(r);
            f.SecondEnergyAge=r.ReadSingle();f.SecondEnergySpinAge=r.ReadSingle();f.SecondEnergyHealth=r.ReadSingle();
            f.TrackingState = (ClaudeTrackingCutState)r.ReadByte(); f.TrackingTarget = r.ReadByte() - 1; f.TrackingShots = r.ReadByte();
            f.TrackingClock = r.ReadSingle(); f.TrackingPhaseAge = r.ReadSingle(); f.MarkerRotation = r.ReadSingle(); f.MarkerPosition = V(r);
            var tracking = encounter.TrackingCut.Config; var cut = encounter.SpatialCut.Config;
            f.TrackingLane = L(r, 0, tracking.Length, tracking.DamageWidth, tracking.Damage);
            f.SecondaryTrackingTarget = r.ReadByte() - 1; f.SecondaryMarkerPosition = V(r);
            f.SecondaryTrackingLane = L(r, 0, tracking.Length, tracking.DamageWidth, tracking.Damage);
            for (int i = 0; i < 6; i++)
            { f.TrackingLanes[i] = L(r, i, tracking.Length, tracking.DamageWidth, tracking.Damage); f.TrackingFiredAt[i] = r.ReadSingle(); }
            f.CutRunning = r.ReadBoolean(); f.CutTotal = r.ReadByte(); f.CutsFired = r.ReadByte(); f.CutAge = r.ReadSingle(); f.NextCut = r.ReadSingle();
            for (int i = 0; i < 3; i++)
            {
                f.CutCounts[i] = r.ReadByte(); f.CutAt[i] = r.ReadSingle();
                for (int j = 0; j < f.CutCounts[i]; j++) f.CutLanes[i][j] = L(r, j, cut.LineLength, cut.DamageWidth, cut.Damage);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 position = V(r); var role = (PlayerRole)r.ReadByte(); var permission = (ClaudePermission)r.ReadByte();
                var state = (ClaudeBookState)r.ReadByte(); float health = r.ReadSingle(), seconds = r.ReadSingle(); bool mirror = r.ReadBoolean();
                f.Books[i] = new ClaudeBookSnapshot(position, role, permission, state, health, seconds, mirror);
                f.BookHits[i] = r.ReadUInt32(); f.BookHitAges[i] = r.ReadSingle();
            }
        }
        private void Read(byte kind, BinaryReader reader)
        {
            if (kind != NetworkMessageCatalog.Authority.ClaudeSnapshot || Session.IsAuthority || Session.Phase != SessionPhase.Playing ||
                !NetworkMessageCatalog.TryValidatePayload(kind, NetworkMessageCatalog.Direction.AuthorityToPeer, reader, out _)) return;
            ReadFrame(reader, _incoming, Encounter);
            if (_received && !RemoteCommandSource.IsNewer(_incoming.Sequence, _lastReceived) || !_incoming.IsValid(Encounter)) return;
            // 权限书也预检权威/客户端边界，提交前无可见对象变化。
            if (!Encounter.Permissions.ApplyReplica(_incoming.Books)) return;
            _received = true; _lastReceived = _incoming.Sequence;
            Apply(_incoming);
        }
        private void Apply(ClaudeEncounterSnapshot f)
        {
            Encounter.SpatialCut.ApplyReplica(f); Encounter.Energy.ApplyReplica(f); Encounter.TrackingCut.ApplyReplica(f);
            Encounter.SecondaryEnergy.ApplyReplica(f,true);
            Encounter.ApplyReplicaState(f.EncounterState); ChapterDriver.ApplyReplica(f.TakenOver);
            var actor = Encounter.Actor;
            actor.transform.position = f.Position; actor.Body.ApplyReplica(actor.Config.MaximumHealth, f.Health, f.Shown, f.PhaseTwo);
            actor.SetPose(f.Pose); Barrier.ApplyReplica(f.BarrierHit, f.BarrierHitAge);
            if (f.Pose == ClaudePose.EnergyCharge || f.Pose == ClaudePose.EnergyRelease)
                actor.FaceAt(f.Position + (f.SecondEnergyState==ClaudeEnergyState.Charging || f.SecondEnergyState==ClaudeEnergyState.Flying
                    ? f.SecondEnergyDirection : f.EnergyDirection));
            else if (f.EncounterState == ClaudeEncounterState.Casting &&
                (f.TrackingState == ClaudeTrackingCutState.Tracking || f.TrackingState == ClaudeTrackingCutState.Locked || f.TrackingState == ClaudeTrackingCutState.Fading))
                actor.FaceAt(f.MarkerPosition);
            else actor.FaceDefault();
            for (int i = 0; i < 4; i++)
                if (f.Books[i].State == ClaudeBookState.Hidden) BookFlashes[i].ResetFeedback();
                else BookFlashes[i].ApplyReplica(f.BookHits[i], f.BookHitAges[i]);
            Encounter.Presentation.ApplyReplica(f);
            ChapterDriver.SceneEffects.ApplyBossClockReplica(f.BossClock, f.BossStates, f.BossSeed, f.BossElapsed);
        }
        private static void V(BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        private static Vector2 V(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle());
        private static void L(BinaryWriter w, in BeamLaneSnapshot lane) { V(w, lane.Origin); V(w, lane.Direction); }
        private static BeamLaneSnapshot L(BinaryReader r, int i, float length, float width, float damage) =>
            new(i, V(r), V(r), length, width, damage, damage);
        private void Clear()
        {
            _frame = _lastReceived = 0; _received = false; _nextSend = 0;
            if (ChapterDriver != null) ChapterDriver.StopCombat(ChapterCombatStopReason.SceneExit);
            if (Barrier != null) Barrier.ResetFeedback();
            if (BookFlashes != null) foreach (var flash in BookFlashes) if (flash != null) flash.ResetFeedback();
        }
    }
}
