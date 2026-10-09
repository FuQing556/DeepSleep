using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    public enum ClaudePose : byte { Idle, Move, PhaseChange, Defeated, Cut, EnergyCharge, EnergyRelease, Seal }

    [CreateAssetMenu(menuName = "DeepSleep/Combat/Claude/Boss", fileName = "CFG_CL_Boss")]
    public sealed class ClaudeBossConfig : ScriptableObject
    {
        public float MaximumHealth;
        public float PhaseTwoHealthFraction;
        public float ContactDamage;
        public float ContactIntervalSeconds;
        public float ContactKnockbackDistance;
        public float ContactKnockbackSeconds;
        public Sprite[] Poses;

        public bool TryValidate(out string reason)
        {
            if (!float.IsFinite(MaximumHealth) || MaximumHealth < 1 ||
                !float.IsFinite(PhaseTwoHealthFraction) || PhaseTwoHealthFraction <= 0 || PhaseTwoHealthFraction >= 1 ||
                !float.IsFinite(ContactDamage) || ContactDamage < 1 ||
                !float.IsFinite(ContactIntervalSeconds) || ContactIntervalSeconds <= 0 ||
                !float.IsFinite(ContactKnockbackDistance) || ContactKnockbackDistance < 0 ||
                !float.IsFinite(ContactKnockbackSeconds) || ContactKnockbackSeconds <= 0 ||
                Poses == null || Poses.Length != System.Enum.GetValues(typeof(ClaudePose)).Length)
            { reason = "Claude生命、接触参数或姿态数量无效。"; return false; }
            foreach (var pose in Poses)
                if (pose == null) { reason = "Claude姿态未绑定。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
