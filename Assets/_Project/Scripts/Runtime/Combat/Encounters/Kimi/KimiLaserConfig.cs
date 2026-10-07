using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    [CreateAssetMenu(menuName = "DeepSleep/Kimi/Laser")]
    public sealed class KimiLaserConfig : ScriptableObject
    {
        public float ChargeSeconds, FireSeconds, RecoverySeconds;
        public float Length, DamageWidth, VisualWidth, TextureRepeatLength, TextureScrollSpeed;
        public float Damage, FocusStartScale, FocusEndScale, FocusSpinDegrees;
        public Color WarningColor, BeamColor;
        public LayerMask PlayerLayers;
        public int ShotCount;
        public int PhaseTwoRayCount;
        public float PhaseTwoRaySpacingDegrees;
        public float PhaseTwoRayDelaySeconds;
        public float TargetMarkerDiameter, TargetMarkerSpinDegrees;

        public bool TryValidate(out string reason)
        {
            if (PhaseTwoRayCount < 1 || PhaseTwoRayCount > 5 || PhaseTwoRayCount % 2 == 0 || !Positive(PhaseTwoRaySpacingDegrees) ||
                PhaseTwoRaySpacingDegrees * (PhaseTwoRayCount-1) >= 180 || !Positive(PhaseTwoRayDelaySeconds) ||
                ShotCount <= 0 || !Positive(TargetMarkerDiameter) || !float.IsFinite(TargetMarkerSpinDegrees) ||
                !Positive(ChargeSeconds) || !Positive(FireSeconds) || !Positive(RecoverySeconds) ||
                !Positive(Length) || !Positive(DamageWidth) || !Positive(VisualWidth) || VisualWidth < DamageWidth ||
                !Positive(TextureRepeatLength) || !float.IsFinite(TextureScrollSpeed) || !Positive(Damage) ||
                !Positive(FocusStartScale) || !Positive(FocusEndScale) || !float.IsFinite(FocusSpinDegrees) || PlayerLayers.value == 0)
            { reason = "激光时间、几何、伤害、视觉或玩家层配置无效。"; return false; }
            reason = string.Empty; return true;
        }
        private static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }
}
