using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/遭遇/Claude追踪切割")]
    public sealed class ClaudeTrackingCutConfig : ScriptableObject
    {
        public float TrackingSeconds, LockedSeconds, FadeSeconds;
        public float Length, DamageWidth, Damage, MovementDirectionEpsilon, MarkerDiameter, MarkerSpin, FrozenClearance;
        public LayerMask PlayerLayers;
        [Tooltip("源图内实测刀线两端，归一化坐标，Y向上；用于等比摆正，不修改PNG。")]
        public Vector2 BladeAxisStart, BladeAxisEnd;
        public bool TryValidate(out string reason)
        {
            if (!Positive(TrackingSeconds) || !Positive(LockedSeconds) || !Positive(FadeSeconds) ||
                !Positive(Length) || !Positive(DamageWidth) || !Positive(Damage) || !Positive(MovementDirectionEpsilon) ||
                !Positive(MarkerDiameter) || !float.IsFinite(MarkerSpin) || !Positive(FrozenClearance) ||
                PlayerLayers.value == 0 || !Inside(BladeAxisStart) || !Inside(BladeAxisEnd) ||
                Vector2.Distance(BladeAxisStart, BladeAxisEnd) < .01f)
            { reason = "追踪切割参数或刀线轴校准无效。"; return false; }
            reason = string.Empty; return true;
        }
        private static bool Positive(float v) => float.IsFinite(v) && v > 0;
        private static bool Inside(Vector2 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1;
    }
}
