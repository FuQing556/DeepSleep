using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Progression.Run;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Audio
{
    public sealed partial class CombatAudioPresenter
    {
        public ClaudeEncounter2D Claude;
        public ChapterSceneEffectsController SceneEffects;
        private readonly ClaudeBookState[] _bookAudioStates = new ClaudeBookState[4];
        private readonly int[] _stateWarnings = new int[3], _stateActive = new int[3];
        private ClaudeTrackingCutState _trackingAudioState;
        private bool _claudeAudioSubscribed;
        private Vector2 ClaudePoint => Claude.Actor.transform.position;

        private void SubscribeClaude()
        {
            if (Claude == null) return;
            Claude.TakeoverRequested += ClaudeReveal;
            Claude.CastStarted += ClaudeCast;
            Claude.PhaseChanged += ClaudePhase;
            Claude.Completed += ClaudeDefeated;
            Claude.Actor.Body.DamageAccepted += ClaudeHurt;
            Claude.Actor.HitEffects.Played += ClaudeImpact;
            Claude.SpatialCut.Fired += ClaudeCut;
            Claude.TrackingCut.Fired += ClaudeSlash;
            Claude.Energy.Fired += ClaudeEnergyFire;
            Claude.Energy.Exploded += ClaudeEnergyBurst;
            Claude.SecondaryEnergy.Fired += ClaudeSecondEnergyFire;
            Claude.SecondaryEnergy.Exploded += ClaudeSecondEnergyBurst;
            foreach (var book in Claude.Permissions.Books) book.Closed += ClaudeBookClosed;
            _claudeAudioSubscribed = true;
        }
        private void UnsubscribeClaude()
        {
            if (!_claudeAudioSubscribed || Claude == null) return;
            Claude.TakeoverRequested -= ClaudeReveal;
            Claude.CastStarted -= ClaudeCast;
            Claude.PhaseChanged -= ClaudePhase;
            Claude.Completed -= ClaudeDefeated;
            Claude.Actor.Body.DamageAccepted -= ClaudeHurt;
            Claude.Actor.HitEffects.Played -= ClaudeImpact;
            Claude.SpatialCut.Fired -= ClaudeCut;
            Claude.TrackingCut.Fired -= ClaudeSlash;
            Claude.Energy.Fired -= ClaudeEnergyFire;
            Claude.Energy.Exploded -= ClaudeEnergyBurst;
            Claude.SecondaryEnergy.Fired -= ClaudeSecondEnergyFire;
            Claude.SecondaryEnergy.Exploded -= ClaudeSecondEnergyBurst;
            foreach (var book in Claude.Permissions.Books) book.Closed -= ClaudeBookClosed;
            _claudeAudioSubscribed = false;
        }
        private void ClaudeReveal() => PublishContentCue(AudioCue.ClaudeReveal, ClaudePoint);
        private void ClaudeDefeated() => PublishContentCue(AudioCue.ClaudeDefeat, ClaudePoint);
        private void ClaudePhase() => PublishContentCue(AudioCue.ClaudePhase, ClaudePoint);
        private void ClaudeHurt(DamagePacket damage) => PublishContentCue(AudioCue.ClaudeHit, damage.HitPoint, .16f);
        private void ClaudeImpact(Vector2 point, float angle) => PublishContentCue(AudioCue.ClaudeImpact, point, .12f);
        private void ClaudeCut() => PublishContentCue(AudioCue.ClaudeCutFire, ClaudePoint);
        private void ClaudeSlash() => PublishContentCue(AudioCue.ClaudeTrackingFire, Claude.TrackingCut.Lane.Center);
        private void ClaudeEnergyFire()
        {
            PublishContentCue(AudioCue.ClaudeEnergyFire, Claude.Energy.Position);
            if (Claude.Actor.Body.PhaseTwo) PublishContentCue(AudioCue.ClaudeEnergyCharge, ClaudePoint);
        }
        private void ClaudeSecondEnergyFire() => PublishContentCue(AudioCue.ClaudeEnergyFire, Claude.SecondaryEnergy.Position);
        private void ClaudeSecondEnergyBurst() => PublishContentCue(AudioCue.ClaudeEnergyBurst, Claude.SecondaryEnergy.Position);
        private void ClaudeEnergyBurst() => PublishContentCue(AudioCue.ClaudeEnergyBurst, Claude.Energy.Position);
        private void ClaudeCast(ClaudeSkill skill)
        {
            if (skill == ClaudeSkill.SpatialCut) PublishContentCue(AudioCue.ClaudeCutWarning, ClaudePoint);
            else if (skill == ClaudeSkill.Energy) PublishContentCue(AudioCue.ClaudeEnergyCharge, ClaudePoint);
        }
        private void ClaudeBookClosed(ClaudePermissionBook2D book, ClaudeBookCloseReason reason)
        {
            if (reason == ClaudeBookCloseReason.Broken)
                PublishContentCue(AudioCue.ClaudeBookBreak, book.transform.position, .12f);
            else if (reason == ClaudeBookCloseReason.Expired)
                PublishContentCue(AudioCue.SceneStateEnd, book.transform.position, .15f);
        }

        // Fixed-size explicitly bound objects, not a scene/object search. The host publishes
        // edges through the existing reliable audio fact stream; replicas never infer them twice.
        private void ObserveWorld02Audio()
        {
            if (IsReplica) return;
            if (!CanPresent)
            {
                System.Array.Clear(_stateWarnings, 0, 3); System.Array.Clear(_stateActive, 0, 3);
                System.Array.Clear(_bookAudioStates, 0, 4); _trackingAudioState = ClaudeTrackingCutState.Idle;
                return;
            }
            if (Claude != null)
            {
                var tracking = Claude.TrackingCut;
                if (tracking.State == ClaudeTrackingCutState.Locked && _trackingAudioState != tracking.State)
                    PublishContentCue(AudioCue.ClaudeTrackingLock, tracking.Lane.Center);
                _trackingAudioState = tracking.State;
                for (int i = 0; i < 4; i++)
                {
                    var book = Claude.Permissions.Books[i];
                    if (book.IsOpen && _bookAudioStates[i] == ClaudeBookState.Hidden)
                        PublishContentCue(AudioCue.ClaudeBookOpen, book.transform.position, .15f);
                    if (book.State == ClaudeBookState.Sealed && _bookAudioStates[i] != book.State)
                        PublishContentCue(AudioCue.ClaudeBookSeal, book.transform.position, .15f);
                    _bookAudioStates[i] = book.State;
                }
            }
            if (SceneEffects == null) return;
            for (int i = 0; i < 3; i++)
            {
                var effect = (SceneBattleEffect)i;
                int warning = SceneEffects.Count(effect, true), active = SceneEffects.Count(effect);
                if (warning > _stateWarnings[i]) PublishContentCue(AudioCue.SceneStateWarning, Vector2.zero, .15f);
                if (active > _stateActive[i]) PublishContentCue(AudioCue.SceneStateStart, Vector2.zero, .15f);
                else if (active < _stateActive[i]) PublishContentCue(AudioCue.SceneStateEnd, Vector2.zero, .15f);
                _stateWarnings[i] = warning; _stateActive[i] = active;
            }
        }
    }
}
