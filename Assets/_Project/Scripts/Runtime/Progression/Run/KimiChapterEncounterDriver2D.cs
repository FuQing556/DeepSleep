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
        public SpriteRenderer BackdropTransition;
        public SpriteRenderer[] Foregrounds = Array.Empty<SpriteRenderer>();
        public Sprite[] NightForegrounds = Array.Empty<Sprite>();
        public SpriteRenderer[] ForegroundTransitions = Array.Empty<SpriteRenderer>();
        public KimiBossPresentation2D Presentation;
        public float BackdropFadeSeconds => Presentation.Timing.NightSeconds;
        [Min(1)] public int SegmentNumber;
        private Sprite _dayBackdrop;
        private Sprite[] _dayForegrounds;
        private bool _armed;
        private float _backdropFadeAge;
        public bool HasTakenOver { get; private set; }
        public bool IsComplete => Encounter.State == KimiEncounterState.Complete;
        public int DisplaySeconds => Encounter.Config.DisplaySeconds;
        public string DisplayTitle => Encounter.Config.RevealedTitle;
        public string ObjectiveText => Chapter.IsChallenge ? Encounter.Config.ObjectiveText : "击败 Kimi";
        private bool CanAuthor => Session.Phase == SessionPhase.Offline || Session.IsAuthority;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Chapter == null || Chapter.CombatWorld == null || Encounter == null || Session == null ||
                Perception == null || Obstacles == null || Hud == null || Backdrop == null || NightBackdrop == null ||
                BackdropTransition == null || Presentation == null || Presentation.Timing == null || !float.IsFinite(BackdropFadeSeconds) || BackdropFadeSeconds <= 0 ||
                SegmentNumber < 1 || Targets == null || Targets.Length != 2 || Targets[0] == null || Targets[1] == null ||
                Targets[0] == Targets[1] || Array.IndexOf(Chapter.CombatWorld.ParticipantComponents, this) < 0)
            { reason = "章节、遭遇、会话、双角色、感知、障碍、HUD、昼夜背景及生命周期登记必须显式配置。"; return false; }
            if (Foregrounds == null || NightForegrounds == null || ForegroundTransitions == null ||
                Foregrounds.Length != NightForegrounds.Length || Foregrounds.Length != ForegroundTransitions.Length)
            { reason = "昼夜前景和渐变层必须成对配置。"; return false; }
            for (int i = 0; i < Foregrounds.Length; i++)
                if (Foregrounds[i] == null || NightForegrounds[i] == null || ForegroundTransitions[i] == null ||
                    Foregrounds[i] == ForegroundTransitions[i])
                { reason = "昼夜前景引用不完整。"; return false; }
            return Encounter.TryValidateConfiguration(out reason) && Presentation.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[KimiChapter] " + reason, this); enabled = false; return; }
            _dayBackdrop = Backdrop.sprite;
            CacheDayForegrounds();
            Hud.Bind(Encounter.Boss, Encounter.Ultimate.Curtain);
        }
        private void OnEnable()
        { if (Encounter != null) Encounter.TakeoverRequested += OnTakeover; }
        public bool IsRequiredForSegment(int segmentNumber) => isActiveAndEnabled && segmentNumber == SegmentNumber &&
            (!Chapter.IsChallenge || Chapter.Challenge.ChallengeKind == DeepSleep.Runtime.Progression.Bestiary.BestiaryChallengeKind.Kimi);
        public void ResetForSegment(int segmentNumber)
        {
            StopCombat(ChapterCombatStopReason.EncounterTakeover);
            Presentation.ResetPresentation();
            _armed = IsRequiredForSegment(segmentNumber);
        }
        public void Simulate(float deltaTime)
        {
            if (!isActiveAndEnabled || !_armed || !CanAuthor || Chapter.Phase != ChapterRunPhase.Combat ||
                !IsRequiredForSegment(Chapter.SegmentNumber) || !float.IsFinite(deltaTime) || deltaTime <= 0) return;
            if (Encounter.State == KimiEncounterState.Idle &&
                !Encounter.Begin(true, UnityEngine.Random.Range(0, int.MaxValue), Session, Targets, Perception, Obstacles,
                    Chapter.IsChallenge ? Chapter.Challenge.PreludeSeconds : (float?)null))
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
            _dayBackdrop = Backdrop.sprite;
            FadeBackdropTo(NightBackdrop);
            SetNightForegrounds(true, true);
        }
        public void StopCombat(ChapterCombatStopReason reason)
        {
            _armed = false;
            HasTakenOver = false;
            bool victory = reason == ChapterCombatStopReason.Settlement && Encounter != null &&
                (IsComplete || (!CanAuthor && Chapter.Phase == ChapterRunPhase.Complete));
            if (victory && Presentation != null && Encounter.Boss.IsShown) Presentation.BeginDeparture();
            if (Encounter != null) Encounter.Cancel(victory);
            if (Backdrop != null && _dayBackdrop != null)
            {
                if (victory) FadeBackdropTo(_dayBackdrop);
                else { Backdrop.sprite = _dayBackdrop; if (BackdropTransition != null) BackdropTransition.enabled = false; }
            }
            if (!victory && Presentation != null) Presentation.ResetPresentation();
            SetNightForegrounds(false, victory);
        }
        /// <summary>客机仅切背景/任务展示；不能清权威实体、推进技能或自行判完成。</summary>
        public void ApplyReplica(bool takenOver, bool defeated = false)
        {
            if (defeated && Encounter.Boss.IsAlive) Presentation.BeginDeparture();
            if (takenOver && !HasTakenOver) _dayBackdrop = Backdrop.sprite;
            if (takenOver != HasTakenOver)
            {
                FadeBackdropTo(takenOver ? NightBackdrop : _dayBackdrop);
                SetNightForegrounds(takenOver, true);
            }
            HasTakenOver = takenOver;
        }

        private void FadeBackdropTo(Sprite next)
        {
            if (next == null || Backdrop.sprite == next) return;
            BackdropTransition.sprite = Backdrop.sprite;
            BackdropTransition.color = Backdrop.color;
            BackdropTransition.enabled = true;
            _backdropFadeAge = 0;
            Backdrop.sprite = next;
        }

        private void CacheDayForegrounds()
        {
            if (_dayForegrounds != null) return;
            _dayForegrounds = new Sprite[Foregrounds.Length];
            for (int i = 0; i < Foregrounds.Length; i++)
                if (Foregrounds[i] != null) _dayForegrounds[i] = Foregrounds[i].sprite;
        }

        private void SetNightForegrounds(bool night, bool fade)
        {
            CacheDayForegrounds();
            for (int i = 0; i < Foregrounds.Length; i++)
            {
                var layer = Foregrounds[i];
                var overlay = ForegroundTransitions[i];
                var next = night ? NightForegrounds[i] : _dayForegrounds[i];
                // SceneExit/OnDisable 的跨根销毁顺序不固定；仍存活的层各自复位，已销毁的层不再访问。
                if (!fade)
                {
                    if (layer != null) layer.sprite = next;
                    if (overlay != null) overlay.enabled = false;
                    continue;
                }
                if (layer.sprite == next) continue;
                overlay.sprite = layer.sprite;
                overlay.color = layer.color;
                // 渐变层是前景的子物体，位置与缩放随原堤岸，不新增碰撞。
                overlay.enabled = layer.enabled;
                layer.sprite = next;
                _backdropFadeAge = 0;
            }
        }

        private void LateUpdate()
        { AdvanceBackdrop(Time.unscaledDeltaTime, Time.timeScale <= 0 && Chapter.Phase != ChapterRunPhase.Complete); }

        public void AdvanceBackdrop(float realSeconds, bool paused)
        {
            if (paused) return;
            bool fading = BackdropTransition != null && BackdropTransition.enabled;
            for (int i = 0; i < ForegroundTransitions.Length; i++) fading |= ForegroundTransitions[i].enabled;
            if (!fading) return;
            _backdropFadeAge += realSeconds;
            float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(_backdropFadeAge / BackdropFadeSeconds));
            FadeLayer(BackdropTransition, alpha, Backdrop.color.a);
            for (int i = 0; i < ForegroundTransitions.Length; i++)
                FadeLayer(ForegroundTransitions[i], alpha, Foregrounds[i].color.a);
        }
        public void ApplyReplicaEntranceAge(float age)
        { _backdropFadeAge = age; AdvanceBackdrop(0, false); }
        private static void FadeLayer(SpriteRenderer layer, float alpha, float opacity)
        {
            if (layer == null || !layer.enabled) return;
            var color = layer.color;
            color.a = alpha * opacity;
            layer.color = color;
            if (alpha <= 0) layer.enabled = false;
        }
        private void OnDisable()
        {
            if (Encounter != null) Encounter.TakeoverRequested -= OnTakeover;
            StopCombat(ChapterCombatStopReason.SceneExit);
        }
    }
}
