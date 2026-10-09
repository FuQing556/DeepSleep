using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    [CreateAssetMenu(menuName="DeepSleep/Kimi/Encounter")]
    public sealed class KimiEncounterConfig : ScriptableObject
    {
        public BossPresentationTiming Timing;
        public float PreludeSeconds, CastGapSeconds, CycleGapSeconds;
        public float EntrySeconds => Timing.EntranceSeconds;
        public float PhaseChangeSeconds => Timing.PhaseSeconds;
        public int CastsPerCycle;
        public Vector2 BossPosition;
        public string RevealedTitle, ObjectiveText;
        public int DisplaySeconds;
        public bool TryValidate(out string reason)
        {
            if(Timing == null || !Positive(PreludeSeconds) || !Positive(EntrySeconds) || !Positive(PhaseChangeSeconds) ||
                !Positive(CastGapSeconds) || !Positive(CycleGapSeconds) || CycleGapSeconds<CastGapSeconds ||
                CastsPerCycle<2 || DisplaySeconds<1 || string.IsNullOrWhiteSpace(RevealedTitle) || string.IsNullOrWhiteSpace(ObjectiveText))
            {reason="遭遇时间/轮次/揭晓文案无效。";return false;}
            return Timing.TryValidate(out reason);
        }
        private static bool Positive(float value)=>float.IsFinite(value) && value>0;
    }
}
