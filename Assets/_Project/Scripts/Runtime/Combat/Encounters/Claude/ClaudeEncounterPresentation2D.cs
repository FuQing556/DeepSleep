using DeepSleep.Runtime.UI;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    public enum ClaudePresentationState { Hidden, NightTransition, Appearing, Battle, Departing }

    /// <summary>
    /// 章节接管后的雨夜/人物出退场表现。不裁决伤害、胜负或奖励；正式遭遇显式调用。
    /// 普通暂停冻结；胜利结算暂停只允许退场影像与雨层透明度继续，不推进战斗。
    /// </summary>
    public sealed class ClaudeEncounterPresentation2D : MonoBehaviour
    {
        public SpriteRenderer Backdrop;
        public SpriteRenderer NightOverlay;
        public Sprite NightSprite;
        public SpriteRenderer[] FigureLayers;
        public RainOverlayView Rain;
        public BossPresentationTiming Timing;
        public BossBarrierFeedback2D Barrier;
        public float BackdropFadeSeconds => Timing.NightSeconds;
        public float FigureFadeSeconds => Timing.FigureSeconds;
        private float _age;
        private float _departureNightAlpha;
        private float _departureFigureAlpha;
        private bool _replica;
        public ClaudePresentationState State { get; private set; }
        public bool IsEntranceComplete => State == ClaudePresentationState.Battle;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Timing == null || Barrier == null || Backdrop == null || NightOverlay == null || Backdrop == NightOverlay || NightSprite == null ||
                Backdrop.transform.parent != NightOverlay.transform.parent ||
                Rain == null || FigureLayers == null || FigureLayers.Length == 0 ||
                !float.IsFinite(BackdropFadeSeconds) || BackdropFadeSeconds <= 0 ||
                !float.IsFinite(FigureFadeSeconds) || FigureFadeSeconds <= 0)
            { reason = "背景、独立夜景层、人物层、雨层及渐变时长须显式配置。"; return false; }
            for (int i = 0; i < FigureLayers.Length; i++)
                if (FigureLayers[i] == null || FigureLayers[i].sprite == null || FigureLayers[i] == Backdrop || FigureLayers[i] == NightOverlay)
                { reason = "人物表现层为空或误引用背景。"; return false; }
            return Timing.TryValidate(out reason);
        }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[ClaudePresentation] " + reason, this); enabled = false; return; }
            ResetPresentation();
        }

        /// <summary>由权威接管/副本状态触发；重复消息不重播入场。</summary>
        public void BeginEntrance()
        {
            if (State != ClaudePresentationState.Hidden) return;
            _replica = false;
            State = ClaudePresentationState.NightTransition;
            _age = 0;
            NightOverlay.sprite = NightSprite;
            NightOverlay.enabled = true;
            SetAlpha(NightOverlay, 0);
            SetFigures(0, false);
            Barrier.EntranceAlpha = 0;
            Rain.SetWeatherActive(true);
        }

        /// <summary>仅胜利触发。重复调用不重置；正式碰撞由遭遇先注销。</summary>
        public void BeginDeparture()
        {
            if (State == ClaudePresentationState.Hidden || State == ClaudePresentationState.Departing) return;
            _departureNightAlpha = NightOverlay.color.a;
            _departureFigureAlpha = FigureLayers[0].enabled ? FigureLayers[0].color.a : 0;
            State = ClaudePresentationState.Departing;
            _age = 0;
            Rain.SetWeatherActive(false, true);
            Barrier.EntranceAlpha = 0;
            Barrier.Barrier.enabled = false;
        }

        private void LateUpdate()
        {
            FollowBackdrop();
            Advance(Time.unscaledDeltaTime, Time.timeScale <= 0);
        }

        public void Advance(float seconds, bool paused)
        {
            if (_replica || !float.IsFinite(seconds) || seconds <= 0 || State == ClaudePresentationState.Hidden ||
                (paused && State != ClaudePresentationState.Departing)) return;
            // 消费跨阶段剩余时间，表现不会因帧率或隔离测试的大步长变慢。
            if (State == ClaudePresentationState.NightTransition)
            {
                _age += seconds;
                SetAlpha(NightOverlay, Mathf.SmoothStep(0, 1, Mathf.Clamp01(_age / BackdropFadeSeconds)));
                if (_age < BackdropFadeSeconds) return;
                seconds = _age - BackdropFadeSeconds;
                _age = 0;
                State = ClaudePresentationState.Appearing;
                SetFigures(0, true);
            }
            if (State == ClaudePresentationState.Appearing)
            {
                _age += seconds;
                SetFigures(Timing.FigureAlpha(_age + BackdropFadeSeconds), true);
                Barrier.EntranceAlpha = Timing.ShieldAlpha(_age + BackdropFadeSeconds);
                if (_age >= Timing.EntranceSeconds - BackdropFadeSeconds) State = ClaudePresentationState.Battle;
            }
            else if (State == ClaudePresentationState.Departing)
            {
                _age += seconds;
                float left = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(_age / BackdropFadeSeconds));
                SetAlpha(NightOverlay, _departureNightAlpha * left);
                float figureLeft = 1 - BossPresentationTiming.In(_age, Timing.DepartureSeconds);
                SetFigures(_departureFigureAlpha * figureLeft, _departureFigureAlpha > 0 && figureLeft > 0);
                if (left <= 0) ResetPresentation();
            }
        }

        private void FollowBackdrop()
        {
            // 两图保持相同几何，滚动/镜头移动不留下独立夜景层错位。
            NightOverlay.transform.SetPositionAndRotation(Backdrop.transform.position, Backdrop.transform.rotation);
            NightOverlay.transform.localScale = Backdrop.transform.localScale;
            NightOverlay.flipX = Backdrop.flipX;
            NightOverlay.flipY = Backdrop.flipY;
        }

        public void ResetPresentation()
        {
            State = ClaudePresentationState.Hidden;
            _replica = false;
            _age = 0;
            if (NightOverlay != null) { NightOverlay.enabled = false; SetAlpha(NightOverlay, 0); }
            if (FigureLayers != null)
                for (int i = 0; i < FigureLayers.Length; i++)
                    if (FigureLayers[i] != null) { FigureLayers[i].enabled = false; SetAlpha(FigureLayers[i], 0); }
            if (Rain != null) Rain.ResetWeather();
            if (Barrier != null) { Barrier.EntranceAlpha = 0; if (Barrier.Barrier != null) Barrier.Barrier.enabled = false; }
        }

        private void SetFigures(float alpha, bool shown)
        {
            for (int i = 0; i < FigureLayers.Length; i++)
            { SetAlpha(FigureLayers[i], alpha); FigureLayers[i].enabled = shown; }
        }
        private static void SetAlpha(SpriteRenderer target, float alpha)
        { Color c = target.color; c.a = alpha; target.color = c; }
        public void CaptureSnapshot(ClaudeEncounterSnapshot f)
        {
            f.PresentationState = State; f.NightAlpha = NightOverlay.enabled ? NightOverlay.color.a : 0;
            f.FigureAlpha = FigureLayers[0].enabled ? FigureLayers[0].color.a : 0; f.RainAlpha = Rain.Group.alpha;
            f.ShieldAlpha = Barrier.EntranceAlpha;
        }
        public void ApplyReplica(ClaudeEncounterSnapshot f)
        {
            if (f.PresentationState == ClaudePresentationState.Hidden) { ResetPresentation(); return; }
            _replica = true; State = f.PresentationState;
            FollowBackdrop(); NightOverlay.sprite = NightSprite; NightOverlay.enabled = f.NightAlpha > 0;
            SetAlpha(NightOverlay, f.NightAlpha); SetFigures(f.FigureAlpha, f.FigureAlpha > 0);
            Rain.SetWeatherActive(State != ClaudePresentationState.Departing, State == ClaudePresentationState.Departing);
            Rain.Group.alpha = f.RainAlpha;
            Barrier.EntranceAlpha = f.ShieldAlpha;
            if (f.ShieldAlpha <= 0) Barrier.Barrier.enabled = false;
        }
        private void OnDisable() => ResetPresentation();
    }
}
