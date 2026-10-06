using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.UI.Combat;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>章节只登记本适配器一次；四技能及其专用池由 Encounter 推进，不能重复登记。</summary>
    public sealed class KimiChapterEncounterDriver2D : MonoBehaviour,
        IFixedSimulationStep, IChapterCombatTakeover, IChapterCombatLifecycle
    {
        public ChapterRunController Chapter;
        public KimiEncounter2D Encounter;
        public CoopSessionController Session;
        public DamageHitbox2D[] Targets;
        public CombatPerceptionRegistry2D Perception;
        public CompanionObstacleRegistry2D Obstacles;
        public KimiBossHudView Hud;
        public SpriteRenderer Backdrop;
        public Sprite NightBackdrop;
        [Min(1)] public int SegmentNumber;
        private Sprite _dayBackdrop;
        private bool _armed;
        public bool HasTakenOver { get; private set; }
        public bool IsComplete => Encounter.State == KimiEncounterState.Complete;
        public int DisplaySeconds => Encounter.Config.DisplaySeconds;
        public string DisplayTitle => Encounter.Config.RevealedTitle;
        public string ObjectiveText => Encounter.Config.ObjectiveText;
        private bool CanAuthor => Session.Phase == SessionPhase.Offline || Session.IsAuthority;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Chapter == null || Chapter.CombatWorld == null || Encounter == null || Session == null ||
                Perception == null || Obstacles == null || Hud == null || Backdrop == null || NightBackdrop == null ||
                SegmentNumber < 1 || Targets == null || Targets.Length != 2 || Targets[0] == null || Targets[1] == null ||
                Targets[0] == Targets[1] || Array.IndexOf(Chapter.CombatWorld.ParticipantComponents, this) < 0)
            { reason = "章节、遭遇、会话、双角色、感知、障碍、HUD、昼夜背景及生命周期登记必须显式配置。"; return false; }
            return Encounter.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[KimiChapter] " + reason, this); enabled = false; return; }
            _dayBackdrop = Backdrop.sprite;
            Hud.Bind(Encounter.Boss, Encounter.Ultimate.Curtain);
        }
        private void OnEnable()
        { if (Encounter != null) Encounter.TakeoverRequested += OnTakeover; }
        public bool IsRequiredForSegment(int segmentNumber) => segmentNumber == SegmentNumber;
        public void ResetForSegment(int segmentNumber)
        {
            StopCombat(ChapterCombatStopReason.EncounterTakeover);
            _armed = IsRequiredForSegment(segmentNumber);
        }
        public void Simulate(float deltaTime)
        {
            if (!isActiveAndEnabled || !_armed || !CanAuthor || Chapter.Phase != ChapterRunPhase.Combat ||
                !IsRequiredForSegment(Chapter.SegmentNumber) || !float.IsFinite(deltaTime) || deltaTime <= 0) return;
            if (Encounter.State == KimiEncounterState.Idle &&
                !Encounter.Begin(true, UnityEngine.Random.Range(0, int.MaxValue), Session, Targets, Perception, Obstacles))
            {
                _armed = false;
                Debug.LogError("[KimiChapter] 遭遇启动失败，停止本适配器。", this);
                return;
            }
            Encounter.Simulate(deltaTime);
        }
        private void OnTakeover()
        {
            if (!_armed || !CanAuthor || Chapter.Phase != ChapterRunPhase.Combat)
            { Encounter.Cancel(); return; }
            Chapter.CombatWorld.TakeOverCombat(this);
            HasTakenOver = true;
            Backdrop.sprite = NightBackdrop;
        }
        public void StopCombat(ChapterCombatStopReason reason)
        {
            _armed = false;
            HasTakenOver = false;
            if (Encounter != null) Encounter.Cancel();
            if (Backdrop != null && _dayBackdrop != null) Backdrop.sprite = _dayBackdrop;
        }
        /// <summary>客机仅切背景/任务展示；不能清权威实体、推进技能或自行判完成。</summary>
        public void ApplyReplica(bool takenOver)
        {
            HasTakenOver = takenOver;
            Backdrop.sprite = takenOver ? NightBackdrop : _dayBackdrop;
        }
        private void OnDisable()
        {
            if (Encounter != null) Encounter.TakeoverRequested -= OnTakeover;
            StopCombat(ChapterCombatStopReason.SceneExit);
        }
    }
}
