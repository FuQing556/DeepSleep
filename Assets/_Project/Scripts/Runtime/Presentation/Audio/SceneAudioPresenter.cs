using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Audio
{
    /// <summary>只消费已生效的阶段/会话边沿；初始快照、刷新 UI 不发提示。</summary>
    public sealed class SceneAudioPresenter : MonoBehaviour
    {
        public Camera WorldCamera;
        public AudioCue CombatAmbience = AudioCue.AmbienceSky;
        public AudioCue RestAmbience = AudioCue.AmbienceRest;
        public DeepSleep.Runtime.Combat.Encounters.Claude.ClaudeEncounterPresentation2D ClaudePresentation;
        private AudioCue? _requestedAmbience;
        public ChapterRunController Chapter;
        public RestNodePrototypeController2D Node;
        public CoopSessionController Session;
        private GameAudioService _audio;
        private ChapterRunPhase _phase;
        private RestNodeState _nodeState;
        private SessionPhase _sessionPhase;
        private bool _hostReady, _guestReady, _ai, _peer, _portalReady, _leaving;
        private AchievementService _achievements;
        private float _achievementAt = float.PositiveInfinity;

        private void Start()
        {
            _audio = GameAppRoot.Instance != null ? GameAppRoot.Instance.Audio : null;
            if (_audio == null) { enabled = false; return; }
            _audio.BindScene(this, WorldCamera);
            _phase = Chapter != null ? Chapter.Phase : ChapterRunPhase.WaitingForSelection;
            _nodeState = Node != null ? Node.State : RestNodeState.Combat;
            if (Chapter != null) Chapter.PhaseChanged += OnPhaseChanged;
            if (Node != null) Node.StateChanged += OnNodeChanged;
            CaptureSession();
            _portalReady = LocalPortalReady();
            RefreshAmbience();
            _achievements = GameAppRoot.Instance.Achievements;
            if (_achievements != null) _achievements.Unlocked += OnAchievement;
        }

        private void Update()
        {
            if (_audio == null) return;
            bool leaving = (GameAppRoot.Instance != null && GameAppRoot.Instance.SceneRouter.IsTransitioning) ||
                           (Session != null && Session.IsExiting);
            if (leaving)
            {
                if (!_leaving) _audio.StopWorld();
                _leaving = true;
                return;
            }
            if (_leaving) { _leaving = false; CaptureSession(); }
            RefreshAmbience();
            if (Node != null)
            {
                bool ready = LocalPortalReady();
                if (ready != _portalReady && Node.State == RestNodeState.Open)
                    _audio.Play(ready ? AudioCue.Ready : AudioCue.UiCancel);
                _portalReady = ready;
            }
            if (Session != null)
            {
                bool enteredRoom = Session.Phase == SessionPhase.Lobby && _sessionPhase != SessionPhase.Lobby && _sessionPhase != SessionPhase.Playing;
                if (enteredRoom) _audio.Play(AudioCue.Connected);
                else if (Session.Phase == SessionPhase.Disconnected && _sessionPhase != SessionPhase.Disconnected)
                    _audio.Play(_peer ? AudioCue.Disconnected : AudioCue.UiReject);
                else if (Session.HasPeer != _peer)
                    _audio.Play(Session.HasPeer ? AudioCue.Connected : AudioCue.Disconnected);
                if ((_hostReady != Session.HostReady || _guestReady != Session.GuestReady) && Session.Phase == SessionPhase.Lobby)
                    _audio.Play((!_hostReady && Session.HostReady) || (!_guestReady && Session.GuestReady) ? AudioCue.Ready : AudioCue.UiCancel);
                if (Session.LocalAi != _ai && Session.CanControlLocally) _audio.Play(AudioCue.AiToggle);
                CaptureSession();
            }
            if (Time.unscaledTime >= _achievementAt)
            {
                _achievementAt = float.PositiveInfinity;
                // 通关/失败主提示接管同一帧的成就；不在结算后补播一串。
                if (_phase != ChapterRunPhase.Complete && _phase != ChapterRunPhase.Defeat)
                    _audio.Play(AudioCue.Upgrade, gain: .7f);
            }
        }

        private void OnPhaseChanged(ChapterRunPhase phase)
        {
            _phase = phase;
            if (_audio == null || _leaving || (Session != null && Session.IsExiting)) return;
            if (phase != ChapterRunPhase.Combat) _audio.StopWorld();
            if (phase == ChapterRunPhase.Complete) _audio.Play(ClaudePresentation != null && Chapter != null &&
                (Chapter.IsChallenge ? Chapter.Challenge.ChallengeKind == DeepSleep.Runtime.Progression.Bestiary.BestiaryChallengeKind.Claude :
                    Chapter.SegmentNumber == Chapter.LevelBindings.Level.ChapterRunConfig.CombatSegmentCount)
                ? AudioCue.ClaudeDefeat : AudioCue.Victory);
            else if (phase == ChapterRunPhase.Defeat) _audio.Play(AudioCue.Defeat);
            else if (phase == ChapterRunPhase.Combat)
            { RefreshAmbience(); _audio.Play(AudioCue.Depart); }
            else if (phase == ChapterRunPhase.Node) RefreshAmbience();
        }

        private void RefreshAmbience()
        {
            var state = ClaudePresentation != null ? ClaudePresentation.State : DeepSleep.Runtime.Combat.Encounters.Claude.ClaudePresentationState.Hidden;
            var cue = _phase == ChapterRunPhase.Node ? RestAmbience :
                (state != DeepSleep.Runtime.Combat.Encounters.Claude.ClaudePresentationState.Hidden &&
                 state != DeepSleep.Runtime.Combat.Encounters.Claude.ClaudePresentationState.Departing)
                ? AudioCue.AmbienceRain : CombatAmbience;
            if (_requestedAmbience == cue) return;
            _requestedAmbience = cue; _audio.SetAmbience(cue, 1.5f);
        }

        private void OnNodeChanged(RestNodeState state)
        {
            if (state == _nodeState) return;
            _nodeState = state;
            if (_audio != null && !_leaving && state == RestNodeState.Open) _audio.Play(AudioCue.NodeOpen);
        }

        private bool LocalPortalReady()
        {
            if (Node == null || Session == null || Session.Assignment == null) return false;
            PlayerRole role = Session.Phase == SessionPhase.Playing ? Session.LocalRole : Session.Assignment.CurrentLocalPlayerRole;
            return Node.IsPortalReady(role);
        }

        private void CaptureSession()
        {
            if (Session == null) return;
            _peer = Session.HasPeer; _hostReady = Session.HostReady; _guestReady = Session.GuestReady;
            _ai = Session.LocalAi; _sessionPhase = Session.Phase;
        }

        private void OnAchievement(AchievementDefinition _) => _achievementAt = Mathf.Min(_achievementAt, Time.unscaledTime + .7f);

        private void OnDisable()
        {
            if (Chapter != null) Chapter.PhaseChanged -= OnPhaseChanged;
            if (Node != null) Node.StateChanged -= OnNodeChanged;
            if (_achievements != null) _achievements.Unlocked -= OnAchievement;
            if (_audio != null) _audio.ReleaseScene(this);
        }
    }
}
