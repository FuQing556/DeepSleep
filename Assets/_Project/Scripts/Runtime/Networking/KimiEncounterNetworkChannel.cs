using System;
using System.IO;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Run;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>完整覆盖型快照，无客户端技能模拟。弹体/360仍由NetworkWorldSnapshotChannel复制。</summary>
    public sealed class KimiEncounterNetworkChannel : MonoBehaviour
    {
        public CoopSessionController Session;
        public KimiChapterEncounterDriver2D ChapterDriver;
        public KimiEncounter2D Encounter;
        public SpriteHitFlash2D BossFlash, CurtainFlash;
        private uint _frame, _lastReceived;
        private bool _received;
        private float _nextSend;
        private readonly Frame _incoming = new();
        private Action<BinaryWriter> _write;

        // 线格式固定语义槽：4个同时预警、左/右/下/上4镜边、16个反弹球表现槽。
        public const int PayloadBytes = 268;
        private sealed class Frame
        {
            public uint Sequence, BossHit, CurtainHit;
            public bool TakenOver, Shown, PhaseTwo, Prism;
            public Vector2 Position, CurtainPosition, LaserOrigin, LaserDirection, LaserMarker;
            public float Health, BossHitAge, CurtainHitAge, WarningProgress, LaserElapsed;
            public KimiPose Pose;
            public KimiLaserState LaserState;
            public int Remaining, Maximum, WarningCount;
            public Rect Arena;
            public readonly int[] Lanes = new int[4];
            public readonly bool[] FromLeft = new bool[4], OrbShown = new bool[16];
            public readonly float[] MirrorHealth = new float[4];
            public readonly Vector2[] OrbPositions = new Vector2[16];
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[KimiNetwork] " + reason, this); enabled = false; return; }
            _write = Write;
        }
        public bool TryValidateConfiguration(out string reason)
        {
            if (Session == null || ChapterDriver == null || Encounter == null || BossFlash == null || CurtainFlash == null ||
                ChapterDriver.Encounter != Encounter || ChapterDriver.Session != Session || Encounter.Moon == null ||
                Encounter.Prism == null || Encounter.Moon.Warnings.Length != 4 || Encounter.Prism.Orbs.Length != 16)
            { reason = "会话、章节适配器、遭遇、闪光以及固定4预警/16球必须匹配。"; return false; }
            reason = string.Empty; return true;
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
            _nextSend = Time.unscaledTime + 1f / Session.Config.SnapshotRate;
            ++_frame;
            Session.SendAuthority(NetworkMessageCatalog.Authority.KimiSnapshot, _write, true);
        }
        private void Write(BinaryWriter w)
        {
            var boss = Encounter.Boss; var curtain = Encounter.Ultimate.Curtain;
            w.Write(_frame); w.Write(ChapterDriver.HasTakenOver); w.Write(boss.IsShown);
            WriteVector(w, boss.transform.position); w.Write(boss.CurrentHealth); w.Write(boss.PhaseTwo); w.Write((byte)boss.Pose);
            w.Write(BossFlash.Sequence); w.Write(BossFlash.NormalizedAge);
            w.Write(curtain.Remaining > 0); WriteVector(w, curtain.transform.position);
            w.Write((ushort)curtain.Remaining); w.Write((ushort)curtain.Maximum);
            w.Write(CurtainFlash.Sequence); w.Write(CurtainFlash.NormalizedAge);
            var moon = Encounter.Moon;
            w.Write((byte)moon.WarningCount); w.Write(moon.WarningCount > 0 ? moon.WarningProgress : 0);
            for (int i = 0; i < 4; i++)
            { w.Write((byte)(i < moon.WarningCount ? moon.GetWarningLane(i) : 0)); w.Write(i < moon.WarningCount && moon.IsWarningFromLeft(i)); }
            var prism = Encounter.Prism;
            w.Write(prism.Active); w.Write(prism.Arena.x); w.Write(prism.Arena.y); w.Write(prism.Arena.width); w.Write(prism.Arena.height);
            for (int i = 0; i < 4; i++) w.Write(prism.Sides[i].CurrentHealth);
            for (int i = 0; i < 16; i++) { w.Write(prism.Orbs[i].Active); WriteVector(w, prism.Orbs[i].Position); }
            var laser = Encounter.Laser;
            w.Write((byte)laser.State); w.Write(laser.Elapsed); WriteVector(w, laser.Lane.Origin); WriteVector(w, laser.Lane.Direction);
            WriteVector(w, laser.MarkerPosition);
        }
        private static void WriteVector(BinaryWriter w, Vector2 v) { w.Write(v.x); w.Write(v.y); }
        private static Vector2 ReadVector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle());
        private void Read(byte kind, BinaryReader r)
        {
            if (kind != NetworkMessageCatalog.Authority.KimiSnapshot || Session.IsAuthority || Session.Phase != SessionPhase.Playing ||
                !NetworkMessageCatalog.TryValidatePayload(kind, NetworkMessageCatalog.Direction.AuthorityToPeer, r, out _)) return;
            Frame f = _incoming;
            f.Sequence = r.ReadUInt32();
            if (_received && !RemoteCommandSource.IsNewer(f.Sequence, _lastReceived)) return;
            f.TakenOver = r.ReadBoolean(); f.Shown = r.ReadBoolean(); f.Position = ReadVector(r); f.Health = r.ReadSingle();
            f.PhaseTwo = r.ReadBoolean(); f.Pose = (KimiPose)r.ReadByte(); f.BossHit = r.ReadUInt32(); f.BossHitAge = r.ReadSingle();
            r.ReadBoolean(); f.CurtainPosition = ReadVector(r); f.Remaining = r.ReadUInt16(); f.Maximum = r.ReadUInt16();
            f.CurtainHit = r.ReadUInt32(); f.CurtainHitAge = r.ReadSingle();
            f.WarningCount = r.ReadByte(); f.WarningProgress = r.ReadSingle();
            for (int i = 0; i < 4; i++) { f.Lanes[i] = r.ReadByte(); f.FromLeft[i] = r.ReadBoolean(); }
            f.Prism = r.ReadBoolean(); f.Arena = new Rect(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < 4; i++) f.MirrorHealth[i] = r.ReadSingle();
            for (int i = 0; i < 16; i++) { f.OrbShown[i] = r.ReadBoolean(); f.OrbPositions[i] = ReadVector(r); }
            f.LaserState = (KimiLaserState)r.ReadByte(); f.LaserElapsed = r.ReadSingle(); f.LaserOrigin = ReadVector(r); f.LaserDirection = ReadVector(r);
            f.LaserMarker = ReadVector(r);
            if (f.Health > Encounter.Boss.MaximumHealth || f.Maximum > Encounter.Ultimate.Config.PhaseTwoHits ||
                f.WarningCount > Encounter.Moon.Warnings.Length) return;
            for (int i = 0; i < f.WarningCount; i++) if (f.Lanes[i] >= Encounter.Moon.Config.LaneCount) return;
            // 全帧预检及配置边界完成后才提交水位和任何可见对象，坏帧不能制造半幅Boss画面。
            _received = true; _lastReceived = f.Sequence;
            ChapterDriver.ApplyReplica(f.TakenOver);
            Encounter.Boss.ApplyReplica(f.Shown, f.Position, f.Health, f.PhaseTwo, f.Pose);
            BossFlash.ApplyReplica(f.BossHit, f.BossHitAge);
            Encounter.Ultimate.Curtain.ApplyReplica(f.CurtainPosition, f.Remaining, f.Maximum);
            CurtainFlash.ApplyReplica(f.CurtainHit, f.CurtainHitAge);
            Encounter.Moon.ApplyReplica(f.WarningCount, f.WarningProgress, f.Lanes, f.FromLeft);
            Encounter.Prism.ApplyReplica(f.Prism, f.Arena, f.MirrorHealth);
            for (int i = 0; i < 16; i++) Encounter.Prism.Orbs[i].ApplyReplica(f.OrbShown[i], f.OrbPositions[i]);
            Encounter.Laser.ApplyReplica(f.LaserState, f.LaserElapsed, f.LaserOrigin, f.LaserDirection, f.LaserMarker);
        }
        private void Clear()
        {
            _frame = _lastReceived = 0; _received = false; _nextSend = 0;
            if (ChapterDriver != null) ChapterDriver.StopCombat(ChapterCombatStopReason.SceneExit);
            if (BossFlash != null) BossFlash.ResetFeedback();
            if (CurtainFlash != null) CurtainFlash.ResetFeedback();
        }
    }
}
