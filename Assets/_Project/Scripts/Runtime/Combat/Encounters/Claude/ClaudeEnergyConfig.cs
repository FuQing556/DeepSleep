using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/遭遇/Claude能量弹", fileName = "CFG_CL_Energy")]
    public sealed class ClaudeEnergyConfig : ScriptableObject
    {
        public float PhaseOneHealth = 100, PhaseTwoHealth = 999;
        public float ChargeSeconds = 2, CrossArenaSeconds = 4;
        public float Damage = 2;
        [Tooltip("爆炸半径独立于飞行两层；保留缩小前外圈的可见半径。")]
        public float ExplosionRadius = 3.712f;
        public float LingerSeconds = 2.5f, FadeSeconds = .6f;
        [Tooltip("内层可见直径，与外环、碰撞和爆炸范围独立。")]
        public float VisibleDiameter = 3.2f;
        public float HaloVisibleDiameter = 4.949333f;
        public float CollisionRadius = 1.4f;
        public float CoreSpin = 55, HaloSpin = -32;
        public LayerMask PlayerLayers;

        public bool TryValidate(out string reason)
        {
            if (!Positive(PhaseOneHealth) || PhaseOneHealth < 1 || !Positive(PhaseTwoHealth) || PhaseTwoHealth < 1 || !Positive(ChargeSeconds) ||
                !Positive(CrossArenaSeconds) || !Positive(Damage) || !Positive(LingerSeconds) ||
                !Positive(FadeSeconds) || !Positive(VisibleDiameter) || !Positive(HaloVisibleDiameter) || !Positive(CollisionRadius) ||
                !Positive(ExplosionRadius) ||
                !float.IsFinite(CoreSpin) || !float.IsFinite(HaloSpin) || CoreSpin * HaloSpin >= 0 || PlayerLayers.value == 0)
            { reason = "Claude能量弹数值或玩家层配置无效。"; return false; }
            reason = string.Empty; return true;
        }
        private static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }
}
