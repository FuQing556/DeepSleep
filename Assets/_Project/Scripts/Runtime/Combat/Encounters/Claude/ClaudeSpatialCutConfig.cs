using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/遭遇/Claude全屏切割")]
    public sealed class ClaudeSpatialCutConfig : ScriptableObject
    {
        public int LineCount;
        public float LineLength, OffsetExtentFraction;
        public float WarningSeconds, CutIntervalSeconds, FlashFadeSeconds, FractureDelaySeconds, FractureFadeSeconds;
        public float Damage, DamageWidth, WarningWidth, FlashWidth, FractureWidth, FrozenClearance;
        public Color WarningColor, FlashColor, FractureColor;
        public LayerMask PlayerLayers;
        public bool TryValidate(out string reason)
        {
            if (LineCount < 1 || LineCount > 64 || !Positive(LineLength) ||
                !Positive(OffsetExtentFraction) || OffsetExtentFraction > 1 || !Positive(WarningSeconds) ||
                !Positive(CutIntervalSeconds) || !Positive(FlashFadeSeconds) || !Positive(FractureFadeSeconds) ||
                !float.IsFinite(FractureDelaySeconds) || FractureDelaySeconds < 0 ||
                !Positive(Damage) || !Positive(DamageWidth) || !Positive(WarningWidth) ||
                !Positive(FlashWidth) || !Positive(FractureWidth) || !Positive(FrozenClearance) || PlayerLayers.value == 0)
            { reason = "全屏切割配置缺失或越界。"; return false; }
            reason = string.Empty; return true;
        }
        private static bool Positive(float v) => float.IsFinite(v) && v > 0;
    }
}
