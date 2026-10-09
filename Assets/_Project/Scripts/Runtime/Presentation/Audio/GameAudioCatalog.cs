using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DeepSleep.Runtime.Presentation.Audio
{
    public enum AudioCue
    {
        UiFocus, UiConfirm, UiCancel, UiOpen, UiReject, DsShot, DsHit, DsSplash,
        GuardStart, GuardBlock, GuardEnd, HsCharge, HsFire, HsSlashUp, HsSlashDown,
        HsSlashSweep, HsHit, HsWave, PlayerHurtDs, PlayerHurtHs, PlayerDown,
        ReviveStart, ReviveDone, BubblePop, BubbleImpact, DoubaoReveal, DoubaoDefeat,
        EnemyDefeat, SnakeShot, NodeOpen, Depart, Victory, Defeat, Upgrade, Refresh,
        Connected, Disconnected, Ready, AiToggle, AmbienceSky, AmbienceDusk, AmbienceRest,
        KimiReveal, KimiPhase, KimiMoonWarn, KimiMoonFire, KimiPrism, KimiOrb, KimiReflect, KimiMirrorBreak, KimiLaserCharge, KimiLaserFire, KimiFlute, KimiTide, KimiInterrupt, KimiDefeat, KimiHit, DownloadCharge, DownloadDash, DownloadDefeat, DownloadImpact, GuardBlockMetal, GuardDefeat, GuardImpact,
        RecursiveHit, RecursiveSplit, RecursiveDefeat, RecursiveImpact, QuickAppImpact, QuickAppDefeat,
        ClaudeReveal, ClaudePhase, ClaudeCutWarning, ClaudeCutFire, ClaudeTrackingLock, ClaudeTrackingFire,
        ClaudeEnergyCharge, ClaudeEnergyFire, ClaudeEnergyBurst, ClaudeBookOpen, ClaudeBookSeal, ClaudeBookBreak,
        ClaudeHit, ClaudeImpact, ClaudeDefeat, SceneStateWarning, SceneStateStart, SceneStateEnd,
        AmbienceCyber, AmbienceRain, AmbienceArcade
    }

    [Serializable]
    public sealed class AudioCueDefinition
    {
        public AudioCue Cue;
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        public AudioClip[] HarnessClips = Array.Empty<AudioClip>();
        public AudioMixerGroup Output;
        [Range(0, 1)] public float Gain = .5f;
        [Range(0, .02f)] public float PitchVariation = .012f;
        [Min(0)] public float MinimumInterval = .045f;
        [Range(1, 8)] public int MaximumVoices = 3;
        [Range(0, 100)] public int Importance = 30;
        public bool IsUi;
        public bool PauseWithWorld = true;
    }

    [CreateAssetMenu(menuName = "DeepSleep/Audio/Cue Catalog")]
    public sealed class GameAudioCatalog : ScriptableObject
    {
        public AudioCueDefinition[] Entries = Array.Empty<AudioCueDefinition>();

        public bool TryValidate(out string reason)
        {
            int count = Enum.GetValues(typeof(AudioCue)).Length;
            if (Entries == null) { reason = "Audio catalog entries are missing."; return false; }
            var seen = new System.Collections.Generic.HashSet<AudioCue>();
            foreach (var entry in Entries)
            {
                if (entry == null || (uint)entry.Cue >= count || !seen.Add(entry.Cue) || entry.Output == null ||
                    entry.Clips == null || entry.Clips.Length == 0 ||
                    Array.Exists(entry.Clips, clip => clip == null) || entry.HarnessClips == null ||
                    Array.Exists(entry.HarnessClips, clip => clip == null))
                { reason = "Audio cue requires unique ID, clips and output group."; return false; }
                if (!(entry.Gain >= 0 && entry.Gain <= 1) || !(entry.PitchVariation >= 0 && entry.PitchVariation <= .02f) ||
                    !(entry.MinimumInterval >= 0 && entry.MinimumInterval < float.PositiveInfinity) ||
                    entry.MaximumVoices < 1 || entry.MaximumVoices > 8 || entry.Importance < 0 || entry.Importance > 100 ||
                    (entry.IsUi && (entry.HarnessClips.Length == 0 || entry.PauseWithWorld)))
                { reason = "Audio cue gain, timing, voice limits or themed UI policy is invalid."; return false; }
            }
            if (seen.Count != count)
            { reason = "Audio catalog does not cover every declared cue."; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
