using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;
using DeepSleep.Runtime.Presentation.Effects;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    /// <summary>全屏切割独立模块：同一快照驱动预警/刀光/裂痕/瞬间伤害；遭遇轴显式启动。</summary>
    public sealed class ClaudeSpatialCutPattern2D : MonoBehaviour, IFixedSimulationStep
    {
        public ClaudeSpatialCutConfig Config;
        public CombatPlayfieldConfig Playfield;
        public BossDamageBody2D Boss;
        public CoopSessionController Session;
        public SpritePoseTransition2D PoseTransition;
        public Sprite IdlePose, CastPose;
        public DamageHitbox2D[] Targets;
        public Collider2D[] TargetShapes;
        public PlayerLifeStateController2D[] TargetLives;
        public PlayerActionGate[] Gates;
        public ClaudeCutBatchView2D Warning;
        public ClaudeCutBatchView2D[] Flashes, Fractures;
        public OneShotSpriteEffectPool2D HitEffects;
        private BeamLaneSnapshot[][] _lanes;
        private readonly float[] _cutAt = new float[3]; // 二阶段最多三刀，是已确认技能结构。
        private readonly int[] _counts = new int[3];
        private readonly bool[] _blocked = new bool[2];
        private readonly BeamHitResolver2D _resolver = new();
        private readonly List<BeamResolvedHit2D> _hits = new();
        private readonly HashSet<IDamageReceiver> _victims = new();
        private System.Random _random;
        private float _age, _nextCut;
        private int _total, _count;
        private bool _replica;
        public int CutsFired { get; private set; }
        public int DamageApplications { get; private set; }
        public bool Running { get; private set; }
        public float Age => _age;
        // AI只读当前已显示预警，不读取尚未抽取的下一刀。
        public int PendingLaneCount => Running && CutsFired < _total ? _count : 0;
        public float PendingSeconds => Mathf.Max(0, _nextCut - _age);
        public BeamLaneSnapshot PendingLane(int index) => _lanes[CutsFired][index];
        public event Action Fired, Completed;
        private bool HasAuthority => Session == null || Session.Phase == SessionPhase.Offline || Session.IsAuthority;

        public bool TryValidateConfiguration(out string reason)
        {
            reason = "全屏切割显式引用缺失。";
            if (Config == null || !Config.TryValidate(out reason) || Playfield == null || Boss == null ||
                PoseTransition == null || IdlePose == null || CastPose == null || Warning == null || HitEffects == null ||
                Flashes == null || Flashes.Length != 3 || Fractures == null || Fractures.Length != 3 ||
                Targets == null || Targets.Length != 2 || TargetShapes == null || TargetShapes.Length != 2 ||
                TargetLives == null || TargetLives.Length != 2 || Gates == null || Gates.Length != 2) return false;
            if (!HitEffects.TryValidateConfiguration(out reason) || !Warning.TryValidate(out reason) || Warning.Capacity < Config.LineCount) return false;
            for (int i = 0; i < 3; i++)
                if (Flashes[i] == null || Fractures[i] == null || !Flashes[i].TryValidate(out reason) ||
                    !Fractures[i].TryValidate(out reason) || Flashes[i].Capacity < Config.LineCount || Fractures[i].Capacity < Config.LineCount) return false;
            for (int i = 0; i < 2; i++)
                if (Targets[i] == null || TargetShapes[i] == null || TargetLives[i] == null || Gates[i] == null ||
                    (Config.PlayerLayers.value & (1 << TargetShapes[i].gameObject.layer)) == 0) return false;
            if (Config.LineLength < 2 * Playfield.WorldBounds.size.magnitude)
            { reason = "固定刀线长度不足以覆盖战区任意斜切。"; return false; }
            reason = string.Empty; return true;
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out var reason)) { Debug.LogError(reason, this); enabled = false; return; }
            _lanes = new BeamLaneSnapshot[3][];
            for (int i = 0; i < 3; i++) _lanes[i] = new BeamLaneSnapshot[Config.LineCount];
            Cancel();
        }
        public bool Begin(bool phaseTwo, int seed)
        {
            if (Running || !isActiveAndEnabled || !HasAuthority || !Boss.IsAlive || _lanes == null) return false;
            Cancel(); _random = new System.Random(seed); _total = phaseTwo ? 3 : 1;
            _nextCut = Config.WarningSeconds; Running = true;
            for (int i = 0; i < 2; i++) _blocked[i] = Gates[i].IsBlocked(PlayerActionBlock.Movement);
            Prepare(); PoseTransition.TransitionTo(CastPose); Render(); return true;
        }
        private void Prepare()
        {
            var bounds = Playfield.WorldBounds; _count = 0;
            // 有限采样只为已封移动角色留可见安全口；不偷偷免伤。上限是算法终止条件。
            for (int attempt = 0; _count < Config.LineCount && attempt < Config.LineCount * 64; attempt++)
            {
                float a = (float)_random.NextDouble() * Mathf.PI;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 normal = new Vector2(-direction.y, direction.x);
                float extent = Mathf.Abs(normal.x) * bounds.width * .5f + Mathf.Abs(normal.y) * bounds.height * .5f;
                Vector2 center = bounds.center + normal * ((float)_random.NextDouble() * 2 - 1) * extent * Config.OffsetExtentFraction;
                bool safe = true;
                for (int p = 0; p < 2; p++)
                    if (TargetLives[p].State == PlayerLifeState.Alive && Gates[p].IsBlocked(PlayerActionBlock.Movement) &&
                        Mathf.Abs(Vector2.Dot((Vector2)TargetShapes[p].bounds.center - center, normal)) <
                        TargetShapes[p].bounds.extents.magnitude + Config.FrozenClearance + Config.DamageWidth * .5f) safe = false;
                if (!safe) continue;
                _lanes[CutsFired][_count] = new BeamLaneSnapshot(_count, center - direction * Config.LineLength * .5f,
                    direction, Config.LineLength, Config.DamageWidth, Config.Damage, Config.Damage);
                _count++;
            }
            _counts[CutsFired] = _count;
            Warning.SetLayout(_lanes[CutsFired], _count, Config.WarningWidth);
            Flashes[CutsFired].SetLayout(_lanes[CutsFired], _count, Config.FlashWidth);
            Fractures[CutsFired].SetLayout(_lanes[CutsFired], _count, Config.FractureWidth);
        }
        public void Simulate(float dt)
        {
            if (_replica || !Running || !float.IsFinite(dt) || dt <= 0) return;
            if (!HasAuthority || !Boss.IsAlive) { Cancel(); return; }
            bool newlyBlocked = false;
            for (int i = 0; i < 2; i++)
            { bool b = Gates[i].IsBlocked(PlayerActionBlock.Movement); newlyBlocked |= b && !_blocked[i]; _blocked[i] = b; }
            if (newlyBlocked && CutsFired < _total) { Prepare(); _nextCut = _age + Config.WarningSeconds; }
            float end = _age + dt;
            while (CutsFired < _total && end >= _nextCut)
            {
                _age = _nextCut; _cutAt[CutsFired] = _age; ApplyDamage();
                if (!Running || !Boss.IsAlive) { Cancel(); return; }
                CutsFired++; Fired?.Invoke();
                if (!Running) return; // 遭遇可在事件里取消，不能继续发剩余刀。
                if (CutsFired < _total) { _nextCut += Config.CutIntervalSeconds; Prepare(); }
                else { Warning.Hide(); PoseTransition.TransitionTo(IdlePose); }
            }
            _age = end; Render();
            if (CutsFired == _total && _age >= _cutAt[_total - 1] + Config.FractureDelaySeconds + Config.FractureFadeSeconds)
            { Running = false; Completed?.Invoke(); }
        }
        private void ApplyDamage()
        {
            _victims.Clear(); ulong id = DamageAttackIdAllocator.Next();
            for (int i = 0; i < _count; i++)
            {
                var lane = _lanes[CutsFired][i]; _resolver.Resolve(lane, Config.PlayerLayers, null, _hits);
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
        }
        private void Render()
        {
            if (CutsFired < _total) Warning.Show(Config.WarningColor);
            for (int i = 0; i < 3; i++)
            {
                if (i >= CutsFired) { Flashes[i].Hide(); Fractures[i].Hide(); continue; }
                float elapsed = _age - _cutAt[i];
                Color flash = Config.FlashColor; flash.a *= Mathf.Clamp01(1 - elapsed / Config.FlashFadeSeconds); Flashes[i].Show(flash);
                Color fracture = Config.FractureColor;
                fracture.a *= elapsed < Config.FractureDelaySeconds ? 0 : Mathf.Clamp01(1 - (elapsed - Config.FractureDelaySeconds) / Config.FractureFadeSeconds);
                Fractures[i].Show(fracture);
            }
        }
        public void Cancel()
        {
            bool wasRunning = Running && !_replica;
            _replica = false;
            Running = false; CutsFired = 0; DamageApplications = 0; _age = _nextCut = 0; _total = _count = 0;
            Array.Clear(_counts, 0, _counts.Length); Array.Clear(_cutAt, 0, _cutAt.Length);
            if (Warning != null) Warning.Hide();
            if (Flashes != null) foreach (var v in Flashes) if (v != null) v.Hide();
            if (Fractures != null) foreach (var v in Fractures) if (v != null) v.Hide();
            if (wasRunning && PoseTransition != null) PoseTransition.ResetTo(IdlePose);
        }
        public void CaptureSnapshot(ClaudeEncounterSnapshot f)
        {
            f.CutRunning = Running; f.CutTotal = Running ? _total : 0; f.CutsFired = Running ? CutsFired : 0;
            f.CutAge = _age; f.NextCut = _nextCut;
            for (int i = 0; i < 3; i++)
            {
                f.CutCounts[i] = Running ? _counts[i] : 0; f.CutAt[i] = _cutAt[i];
                if (f.CutCounts[i] > 0) Array.Copy(_lanes[i], f.CutLanes[i], f.CutCounts[i]);
            }
        }
        public void ApplyReplica(ClaudeEncounterSnapshot f)
        {
            if (!f.CutRunning) { Cancel(); return; }
            bool warningChanged = !_replica || CutsFired != f.CutsFired;
            for (int i = 0; i < 3; i++)
            {
                bool changed = !_replica || _counts[i] != f.CutCounts[i];
                for (int j = 0; !changed && j < f.CutCounts[i]; j++)
                    changed = _lanes[i][j].Origin != f.CutLanes[i][j].Origin || _lanes[i][j].Direction != f.CutLanes[i][j].Direction;
                if (!changed) continue;
                Array.Copy(f.CutLanes[i], _lanes[i], f.CutCounts[i]);
                Flashes[i].SetLayout(_lanes[i], f.CutCounts[i], Config.FlashWidth);
                Fractures[i].SetLayout(_lanes[i], f.CutCounts[i], Config.FractureWidth);
                warningChanged |= i == f.CutsFired;
            }
            _replica = true; Running = f.CutRunning; _total = f.CutTotal; CutsFired = f.CutsFired; _age = f.CutAge; _nextCut = f.NextCut;
            for (int i = 0; i < 3; i++)
            {
                _counts[i] = f.CutCounts[i]; _cutAt[i] = f.CutAt[i];
            }
            if (CutsFired < _total)
            { if (warningChanged) Warning.SetLayout(_lanes[CutsFired], _counts[CutsFired], Config.WarningWidth); }
            else Warning.Hide();
            Render(); // 不调用ApplyDamage、Fired或Completed。
        }
        private void OnDisable() => Cancel();
    }
}
