using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    [CreateAssetMenu(menuName="DeepSleep/Kimi/Ultimate")]
    public sealed class KimiUltimateConfig : ScriptableObject
    {
        public int PhaseOneHits, PhaseTwoHits, VolleyCount, PhaseTwoBlades, ReinforcementLimit;
        public float PhaseOneChargeSeconds, PhaseTwoChargeSeconds, StaggerSeconds, ReleaseDelay, VolleyInterval;
        public float SpreadDegrees, ReinforcementHealthMultiplier;
        public Vector2 CurtainOffset, MuzzleOffset;
        public bool TryValidate(out string reason)
        {
            if (PhaseOneHits <= 0 || PhaseTwoHits < PhaseOneHits || VolleyCount <= 0 || PhaseTwoBlades <= 0 ||
                ReinforcementLimit <= 0 || !Positive(PhaseOneChargeSeconds) || !Positive(PhaseTwoChargeSeconds) ||
                !Positive(StaggerSeconds) || !Positive(ReleaseDelay) || !Positive(VolleyInterval) ||
                !Positive(SpreadDegrees) || !Positive(ReinforcementHealthMultiplier))
            { reason="大招次数/时长/发射/增援配置无效。"; return false; }
            reason=string.Empty; return true;
        }
        private static bool Positive(float v) => float.IsFinite(v) && v>0;
    }
}
