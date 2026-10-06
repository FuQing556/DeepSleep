using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    [CreateAssetMenu(menuName="DeepSleep/Kimi/Encounter")]
    public sealed class KimiEncounterConfig : ScriptableObject
    {
        public float PreludeSeconds, EntrySeconds, PhaseChangeSeconds, CastGapSeconds, CycleGapSeconds;
        public int CastsPerCycle;
        public Vector2 BossPosition;
        public string RevealedTitle, ObjectiveText;
        public int DisplaySeconds;
        public bool TryValidate(out string reason)
        {
            if(!Positive(PreludeSeconds) || !Positive(EntrySeconds) || !Positive(PhaseChangeSeconds) ||
                !Positive(CastGapSeconds) || !Positive(CycleGapSeconds) || CycleGapSeconds<CastGapSeconds ||
                CastsPerCycle<2 || DisplaySeconds<1 || string.IsNullOrWhiteSpace(RevealedTitle) || string.IsNullOrWhiteSpace(ObjectiveText))
            {reason="遭遇时间/轮次/揭晓文案无效。";return false;}
            reason=string.Empty;return true;
        }
        private static bool Positive(float value)=>float.IsFinite(value) && value>0;
    }
}
