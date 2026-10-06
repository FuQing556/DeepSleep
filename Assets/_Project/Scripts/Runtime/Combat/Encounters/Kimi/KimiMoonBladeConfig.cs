using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    [CreateAssetMenu(menuName = "DeepSleep/Combat/Kimi/Moon Blade", fileName = "CFG_KI_MoonBlade")]
    public sealed class KimiMoonBladeConfig : ScriptableObject
    {
        [Min(1)] public int LaneCount;
        [Min(1)] public int VolleyCount;
        [Min(1)] public int PhaseOneBlades;
        [Min(1)] public int PhaseTwoBlades;
        [Min(0.01f)] public float WarningSeconds;
        [Min(0)] public float VolleyGapSeconds;
        [Min(0)] public float EdgePadding;
        [Min(0.01f)] public float WarningWidth;
        public Color WarningColor;

        public bool TryValidate(out string reason)
        {
            if (LaneCount < 2 || VolleyCount < 1 || PhaseOneBlades < 1 || PhaseTwoBlades < PhaseOneBlades ||
                PhaseTwoBlades >= LaneCount || !float.IsFinite(WarningSeconds) || WarningSeconds <= 0 ||
                !float.IsFinite(VolleyGapSeconds) || VolleyGapSeconds < 0 ||
                !float.IsFinite(EdgePadding) || EdgePadding < 0 || !float.IsFinite(WarningWidth) || WarningWidth <= 0)
            { reason = "通道、波数、并发或预警参数无效，至少保留一条空通道。"; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
