using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard
{
    [CreateAssetMenu(menuName = "DeepSleep/Combat/DeepSeek/Rice Guard")]
    public sealed class DeepSeekRiceGuardConfig : ScriptableObject
    {
        [Min(1)] public int Charges;
        [Min(.01f)] public float DurationSeconds;
        [Min(0)] public float CooldownSeconds;
        [Min(0)] public float WarningSeconds;
        [Min(.01f), Tooltip("世界单位；以DS角色根为圆心，按受保护角色根判断是否在圈内。")]
        public float Radius;

        public bool IsValid => Charges > 0 && Finite(DurationSeconds) && DurationSeconds > 0 &&
            Finite(CooldownSeconds) && CooldownSeconds >= 0 && Finite(WarningSeconds) &&
            WarningSeconds >= 0 && WarningSeconds <= DurationSeconds && Finite(Radius) && Radius > 0;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
