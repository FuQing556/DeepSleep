using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters
{
    /// <summary>两名 Boss 共用的纯演出时间与采样；不决定技能、碰撞或生命。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/Combat/Boss Presentation Timing")]
    public sealed class BossPresentationTiming : ScriptableObject
    {
        public float NightSeconds = 3, FigureSeconds = 1, ShieldSeconds = .25f;
        public float ReadySeconds = .5f, PhaseSeconds = 2, DepartureSeconds = 1.5f;
        public float EntranceSeconds => NightSeconds + FigureSeconds + ShieldSeconds + ReadySeconds;
        public float FigureAlpha(float age) => In(age - NightSeconds, FigureSeconds);
        public float ShieldAlpha(float age) => In(age - NightSeconds - FigureSeconds, ShieldSeconds);
        public static float In(float age, float seconds) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / seconds));
        public bool TryValidate(out string reason)
        {
            foreach (float value in new[] { NightSeconds, FigureSeconds, ShieldSeconds, ReadySeconds, PhaseSeconds, DepartureSeconds })
                if (!float.IsFinite(value) || value <= 0) { reason = "Boss演出时间必须有限且大于零。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
