using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using Fact = DeepSleep.Runtime.Networking.NetworkMessageCatalog.CombatPresentationKind;

namespace DeepSleep.Runtime.Presentation.Audio
{
    public sealed partial class CombatAudioPresenter
    {
        [Tooltip("仅有Kimi遭遇的场景显式赋值。")]
        public KimiEncounter2D Kimi;
        private int _fluteHandle;
        private void StopKimiFlute() { if (_fluteHandle != 0 && Audio != null) Audio.StopLoop(_fluteHandle, .08f); _fluteHandle = 0; }
        public void PublishContentCue(AudioCue cue, Vector2 point, float minimumInterval = .06f)
        {
            if (cue < AudioCue.KimiReveal || cue > AudioCue.SceneStateEnd) return;
            Publish((Fact)((int)Fact.KimiReveal + (int)cue - (int)AudioCue.KimiReveal), PlayerRole.DeepSeek, point, minimumInterval);
        }
        private Vector2 KimiPoint => Kimi.Boss.transform.position;
        private void SubscribeKimi()
        {
            if (Kimi == null) return;
            Kimi.TakeoverRequested += KimiReveal;
            Kimi.CastStarted += KimiCast;
            Kimi.Completed += KimiDefeat;
            Kimi.PhaseChanged += KimiPhase;
            Kimi.Boss.DamageAccepted += KimiHit;
            Kimi.Moon.WarningStarted += MoonWarning;
            Kimi.Moon.VolleyFired += MoonFire;
            Kimi.Laser.Fired += KimiLaserFire;
            Kimi.Laser.ChargeStarted += KimiLaserCharge;
            Kimi.Prism.OrbFired += KimiOrb;
            Kimi.Prism.Reflected += KimiReflect;
            foreach (var side in Kimi.Prism.Sides) side.Broken += MirrorBreak;
            Kimi.Ultimate.VolleyFired += KimiTide;
            Kimi.Ultimate.Curtain.Broken += KimiInterrupt;
        }
        private void UnsubscribeKimi()
        {
            if (Kimi == null) return;
            Kimi.TakeoverRequested -= KimiReveal;
            Kimi.CastStarted -= KimiCast;
            Kimi.Completed -= KimiDefeat;
            Kimi.PhaseChanged -= KimiPhase;
            Kimi.Boss.DamageAccepted -= KimiHit;
            Kimi.Moon.WarningStarted -= MoonWarning;
            Kimi.Moon.VolleyFired -= MoonFire;
            Kimi.Laser.Fired -= KimiLaserFire;
            Kimi.Laser.ChargeStarted -= KimiLaserCharge;
            Kimi.Prism.OrbFired -= KimiOrb;
            Kimi.Prism.Reflected -= KimiReflect;
            foreach (var side in Kimi.Prism.Sides) side.Broken -= MirrorBreak;
            Kimi.Ultimate.VolleyFired -= KimiTide;
            Kimi.Ultimate.Curtain.Broken -= KimiInterrupt;
        }
        private void KimiReveal() => PublishContentCue(AudioCue.KimiReveal, Kimi.Config.BossPosition);
        private void KimiDefeat() => PublishContentCue(AudioCue.KimiDefeat, KimiPoint);
        private void KimiPhase() => PublishContentCue(AudioCue.KimiPhase, KimiPoint);
        private void KimiHit(DamagePacket packet) => PublishContentCue(AudioCue.KimiHit, packet.HitPoint, .15f);
        private void MoonWarning() => PublishContentCue(AudioCue.KimiMoonWarn, KimiPoint);
        private void MoonFire() => PublishContentCue(AudioCue.KimiMoonFire, KimiPoint);
        private void KimiLaserFire() => PublishContentCue(AudioCue.KimiLaserFire, KimiPoint);
        private void KimiLaserCharge() => PublishContentCue(AudioCue.KimiLaserCharge, KimiPoint);
        private void KimiOrb(Vector2 point) => PublishContentCue(AudioCue.KimiOrb, point);
        private void KimiReflect(Vector2 point) => PublishContentCue(AudioCue.KimiReflect, point, .15f);
        private void MirrorBreak(KimiMirrorSide2D side) => PublishContentCue(AudioCue.KimiMirrorBreak, side.transform.position, .1f);
        private void KimiTide() => PublishContentCue(AudioCue.KimiTide, KimiPoint);
        private void KimiInterrupt() => PublishContentCue(AudioCue.KimiInterrupt, KimiPoint);
        private void KimiCast(KimiSkill skill)
        {
            if (skill == KimiSkill.Prism) PublishContentCue(AudioCue.KimiPrism, KimiPoint);
            else if (skill == KimiSkill.Ultimate) PublishContentCue(AudioCue.KimiFlute, KimiPoint);
        }
    }
}
