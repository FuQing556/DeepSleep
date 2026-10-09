using System;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    public enum KimiMoonState : byte { Idle, Warning, Flying, Gap, Complete }

    /// <summary>
    /// 十通道月光刃：固定刻选择互异通道、预警、发射，整波回收后才允许下一波。
    /// 弹体、挡伤、命中特效、AI危险感知仍由已有 EnemyProjectilePool 链路负责。
    /// </summary>
    public sealed class KimiMoonBladePattern2D : MonoBehaviour, IFixedSimulationStep
    {
        public KimiMoonBladeConfig Config;
        public CombatPlayfieldConfig Playfield;
        public EnemyProjectilePool2D Projectiles;
        public EnemyProjectileConfig ProjectileConfig;
        public KimiBoss2D Boss;
        public LineRenderer[] Warnings;
        private CoopSessionController _session;
        private System.Random _random;
        private int[] _laneBag;
        private int[] _lanes;
        private bool[] _fromLeft;
        private int _count;
        private float _elapsed;
        private bool _authoring;
        public KimiMoonState State { get; private set; }
        public int FiredVolleys { get; private set; }
        public int FiredBlades { get; private set; }
        public int ConcurrentLimit => _count;
        public int WarningCount => State == KimiMoonState.Warning ? _count : 0;
        public float WarningProgress => Mathf.Clamp01(_elapsed / Config.WarningSeconds);
        public event Action Completed;
        public event Action WarningStarted, VolleyFired;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[KimiMoonBlade] " + reason, this); enabled = false; return; }
            _laneBag = new int[Config.LaneCount];
            _lanes = new int[Config.PhaseTwoBlades];
            _fromLeft = new bool[Config.PhaseTwoBlades];
            HideWarnings();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config == null || Playfield == null || Projectiles == null || ProjectileConfig == null || Boss == null)
            { reason = "配置、固定玩法区、专属敌弹池或Boss引用缺失。"; return false; }
            if (!Config.TryValidate(out reason) || !Playfield.TryValidate(out reason) ||
                !ProjectileConfig.TryValidate(out reason) || !Projectiles.TryValidateConfiguration(out reason)) return false;
            if (Warnings == null || Warnings.Length != Config.PhaseTwoBlades ||
                Array.Exists(Warnings, item => item == null) || ProjectileConfig.InitialPoolSize < Config.PhaseTwoBlades ||
                ProjectileConfig.LifetimeSeconds * ProjectileConfig.Speed <= Playfield.WorldBounds.width + 2 * Config.EdgePadding)
            { reason = "预警/预热容量不足，或弹体寿命不足以横贯完整战区。"; return false; }
            reason = string.Empty;
            return true;
        }

        public bool Begin(bool phaseTwo, int seed, CoopSessionController session)
        {
            if (!enabled || _laneBag == null || !Boss.IsAlive ||
                (session != null && session.Phase != SessionPhase.Offline && !session.IsAuthority)) return false;
            Cancel();
            _session = session;
            _authoring = true;
            _random = new System.Random(seed);
            _count = phaseTwo ? Config.PhaseTwoBlades : Config.PhaseOneBlades;
            FiredVolleys = FiredBlades = 0;
            Boss.SetPose(KimiPose.MoonCast);
            PrepareVolley();
            return true;
        }

        public int GetWarningLane(int index) => _lanes[index];
        public bool IsWarningFromLeft(int index) => _fromLeft[index];
        public float LaneY(int lane) => Playfield.WorldBounds.yMin +
            (lane + .5f) * Playfield.WorldBounds.height / Config.LaneCount;

        public void Simulate(float deltaTime)
        {
            if (!_authoring || !isActiveAndEnabled || deltaTime <= 0 ||
                (_session != null && _session.Phase != SessionPhase.Offline && !_session.IsAuthority)) return;
            if (!Boss.IsAlive) { Cancel(); return; }
            _elapsed += deltaTime;
            switch (State)
            {
                case KimiMoonState.Warning:
                    RenderWarnings();
                    if (_elapsed >= Config.WarningSeconds) FireVolley();
                    break;
                case KimiMoonState.Flying:
                    if (Projectiles.ActiveCount != 0) break;
                    _elapsed = 0;
                    if (FiredVolleys >= Config.VolleyCount)
                    {
                        State = KimiMoonState.Complete;
                        _authoring = false;
                        Boss.SetPose(KimiPose.Idle);
                        Completed?.Invoke();
                    }
                    else State = KimiMoonState.Gap;
                    break;
                case KimiMoonState.Gap:
                    if (_elapsed >= Config.VolleyGapSeconds) PrepareVolley();
                    break;
            }
        }

        private void PrepareVolley()
        {
            for (int i = 0; i < _laneBag.Length; i++) _laneBag[i] = i;
            bool firstFromLeft = _random.Next(2) == 0;
            for (int i = 0; i < _count; i++)
            {
                int pick = _random.Next(i, _laneBag.Length);
                (_laneBag[i], _laneBag[pick]) = (_laneBag[pick], _laneBag[i]);
                _lanes[i] = _laneBag[i];
                // 同波两侧都有来刃；首方向随机，避免整次技能只从同一侧进入。
                _fromLeft[i] = (i % 2 == 0) == firstFromLeft;
            }
            _elapsed = 0;
            State = KimiMoonState.Warning;
            RenderWarnings();
            WarningStarted?.Invoke();
        }

        private void FireVolley()
        {
            HideWarnings();
            Rect bounds = Playfield.WorldBounds;
            for (int i = 0; i < _count; i++)
            {
                Vector2 start = new(_fromLeft[i] ? bounds.xMin - Config.EdgePadding : bounds.xMax + Config.EdgePadding, LaneY(_lanes[i]));
                if (Projectiles.TryRent(start, _fromLeft[i] ? Vector2.right : Vector2.left, Boss.gameObject, out _)) FiredBlades++;
            }
            FiredVolleys++;
            VolleyFired?.Invoke();
            State = KimiMoonState.Flying;
            _elapsed = 0;
        }

        private void RenderWarnings()
        {
            Rect bounds = Playfield.WorldBounds;
            for (int i = 0; i < Warnings.Length; i++)
            {
                LineRenderer line = Warnings[i];
                line.enabled = i < _count;
                if (!line.enabled) continue;
                float y = LaneY(_lanes[i]);
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.SetPosition(0, new Vector3(_fromLeft[i] ? bounds.xMin : bounds.xMax, y, 0));
                line.SetPosition(1, new Vector3(_fromLeft[i] ? bounds.xMax : bounds.xMin, y, 0));
                line.startWidth = line.endWidth = Config.WarningWidth;
                Color color = Config.WarningColor;
                color.a *= Mathf.Lerp(.35f, 1f, WarningProgress);
                line.startColor = color;
                color.a *= .25f;
                line.endColor = color;
            }
        }

        private void HideWarnings()
        {
            if (Warnings == null) return;
            foreach (LineRenderer line in Warnings) if (line != null) line.enabled = false;
        }

        /// <summary>只重建预警；不租弹、不推进技能，实际弹体由通用网络实体通道显示。</summary>
        public void ApplyReplica(int count, float progress, int[] lanes, bool[] fromLeft)
        {
            _authoring = false; _count = count; _elapsed = progress * Config.WarningSeconds;
            State = count > 0 ? KimiMoonState.Warning : KimiMoonState.Idle;
            for (int i = 0; i < count; i++) { _lanes[i] = lanes[i]; _fromLeft[i] = fromLeft[i]; }
            RenderWarnings();
        }

        public void Cancel()
        {
            _authoring = false;
            State = KimiMoonState.Idle;
            _elapsed = 0;
            HideWarnings();
            if (Projectiles != null) Projectiles.ReturnAllActive();
        }

        private void OnDisable() => Cancel();
    }
}
