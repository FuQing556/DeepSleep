using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    [CreateAssetMenu(menuName = "DeepSleep/Combat/Kimi/Prism", fileName = "CFG_KI_Prism")]
    public sealed class KimiPrismConfig : ScriptableObject
    {
        [Min(.01f)] public float Duration;
        [Min(.01f)] public float FirstShotDelay;
        [Min(.01f)] public float ShotInterval;
        [Min(.01f)] public float PhaseOneSpeed;
        [Min(.01f)] public float PhaseTwoSpeed;
        [Min(1)] public float PhaseOneMirrorHealth;
        [Min(1)] public float PhaseTwoMirrorHealth;
        [Min(1)] public float ContactDamage;
        [Min(.01f)] public float ContactInterval;
        [Min(.01f)] public float OrbRadius;
        [Min(0)] public float ArenaInset;
        [Min(0)] public float ExitPadding;
        public LayerMask PlayerLayers;

        public bool TryValidate(out string reason)
        {
            float[] positive = { Duration, FirstShotDelay, ShotInterval, PhaseOneSpeed, PhaseTwoSpeed,
                PhaseOneMirrorHealth, PhaseTwoMirrorHealth, ContactDamage, ContactInterval, OrbRadius };
            foreach (float value in positive)
                if (!float.IsFinite(value) || value <= 0) { reason = "棱光数值必须为有限正数。"; return false; }
            if (FirstShotDelay >= Duration || PhaseTwoSpeed < PhaseOneSpeed || PhaseTwoMirrorHealth < PhaseOneMirrorHealth ||
                !float.IsFinite(ArenaInset) || ArenaInset < 0 || !float.IsFinite(ExitPadding) || ExitPadding < OrbRadius || PlayerLayers == 0)
            { reason = "棱光计时、阶段倍率或边界配置无效。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
