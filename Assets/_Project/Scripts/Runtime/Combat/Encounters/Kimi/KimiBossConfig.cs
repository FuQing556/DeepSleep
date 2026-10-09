using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    public enum KimiPose : byte
    {
        Idle, MoonCast, PrismCast, LaserCharge, LaserRelease,
        FluteCharge, TideRelease, Stagger, PhaseChange, Bow
    }

    /// <summary>已确认人物姿态与本体生命；技能调参独立，不能从美术尺寸推导碰撞。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/Combat/Kimi/Boss", fileName = "CFG_KI_Boss")]
    public sealed class KimiBossConfig : ScriptableObject
    {
        [Min(1)] public float MaximumHealth;
        [Range(0.01f, .99f)] public float PhaseTwoHealthFraction;
        [Min(1)] public float ContactDamageAmount;
        [Min(0.01f)] public float ContactIntervalSeconds;
        [Min(0f)] public float ContactKnockbackDistance;
        [Min(.01f)] public float ContactKnockbackSeconds = .18f;
        public Sprite[] Poses;

        public bool TryValidate(out string reason)
        {
            if (!float.IsFinite(MaximumHealth) || MaximumHealth < 1 ||
                !float.IsFinite(ContactDamageAmount) || ContactDamageAmount < 1 ||
                !float.IsFinite(ContactIntervalSeconds) || ContactIntervalSeconds <= 0 ||
                !float.IsFinite(ContactKnockbackDistance) || ContactKnockbackDistance < 0 ||
                !float.IsFinite(ContactKnockbackSeconds) || ContactKnockbackSeconds <= 0 ||
                !float.IsFinite(PhaseTwoHealthFraction) || PhaseTwoHealthFraction <= 0 || PhaseTwoHealthFraction >= 1 ||
                Poses == null || Poses.Length != System.Enum.GetValues(typeof(KimiPose)).Length)
            { reason = "生命、二阶段阈值或姿态数组无效。"; return false; }
            foreach (Sprite pose in Poses)
                if (pose == null) { reason = "Kimi 姿态素材缺失。"; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
