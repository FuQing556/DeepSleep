using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    [CreateAssetMenu(menuName = "DeepSleep/Combat/Harness/Melee Mode")]
    public sealed class HarnessMeleeConfig : ScriptableObject
    {
        public Sprite IdlePose;
        [Min(0.01f)] public float IdlePoseScale = 1f;
        public Vector2 IdlePoseOffset;
        public Sprite RangedPose;
        public Sprite SwordSprite;
        [Range(0.5f, 1f), Tooltip("原剑图中剑尖的归一化 X，用于保证可见剑尖与判定终点一致。")]
        public float SwordTipNormalizedX = 0.99f;
        [Tooltip("世界朝向角。-90 表示剑尖朝下，朝左不把剑倒置。")]
        public float IdleSwordAngle = -90f;
        public HarnessMeleeAttackConfig[] Attacks;
        [Min(0.5f)] public float DurationSeconds = 12f;
        [Min(0)] public float CooldownSeconds = 20f;
        [Min(0), Tooltip("收刀后超过此时间未接招，下次从第一招开始；不是攻击冷却。")]
        public float ComboResetSeconds;
        public LayerMask EnemyLayers;
        public LayerMask ClearableProjectileLayers;
        [Min(0.01f)] public float SweepSampleDistance = 0.06f;
        [Min(0.01f)] public float IdleSwordLength = 0.6f;
        public Vector2 IdleSwordOffset = new(0.7f, 0.1f);
        [Min(0)] public float FloatAmplitude = 0.055f;
        [Min(0)] public float FloatFrequency = 0.8f;
        [Range(0, 1)] public float GhostAlpha = 0.4f;
        [Min(0.01f)] public float GhostSeconds = 0.16f;
        [Min(0.01f)] public float WaveFadeSeconds = 0.35f;

        public bool IsValid => IdlePose != null && RangedPose != null && SwordSprite != null &&
            Attacks != null && Attacks.Length == 3 &&
            System.Array.TrueForAll(Attacks, a => a != null && a.CharacterPose != null &&
                a.WaveSprite != null && a.WavePolygon != null && a.WavePolygon.Length >= 3 &&
                MeleeSwordMotion2D.IsValid(a.MotionKeys) && a.FollowThroughSeconds >= 0 &&
                a.SwingSeconds > 0 && a.SwordLength > 0 && a.BladeWidth > 0 &&
                a.WaveWidth > 0 && a.BladeDamage >= 0 && a.WaveDamage >= 0) &&
            DurationSeconds > 0 && CooldownSeconds >= 0 && WaveFadeSeconds > 0 &&
            ComboResetSeconds >= 0 && !float.IsInfinity(ComboResetSeconds);
    }
}
