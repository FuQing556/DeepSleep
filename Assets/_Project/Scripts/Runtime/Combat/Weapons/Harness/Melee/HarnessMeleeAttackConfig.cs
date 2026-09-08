using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    [CreateAssetMenu(menuName = "DeepSleep/Combat/Harness/Melee Attack")]
    public sealed class HarnessMeleeAttackConfig : ScriptableObject
    {
        public Sprite CharacterPose;
        [Min(0.01f), Tooltip("仅此攻击姿态的图片倍率，不改变角色碰撞体。")]
        public float CharacterScale = 1f;
        public Vector2 CharacterOffset;
        public Sprite WaveSprite;
        [Range(0.01f, 0.45f)] public float GrowFraction = 0.3f;
        [Range(0.01f, 0.45f)] public float ShrinkFraction = 0.3f;
        [Range(0.001f, 0.5f)] public float MinimumSwordScale = 0.02f;
        [Tooltip("整条挥剑轨迹相对 LaserOrigin 剑柄锚点的偏移，朝左时镜像。")]
        public Vector2 SwingOffset;
        [Min(0.1f)] public float SwingSeconds = 0.5f;
        [Min(0f)] public float RecoverySeconds = 0.08f;
        [Tooltip("角位移进度，不是大小曲线。默认立方曲线：先慢后快。")]
        public AnimationCurve Travel = new(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 3, 3));
        public float StartAngle = 65f;
        public float EndAngle = -65f;
        [Range(0.1f, 1f)] public float OrbitHeight = 1f;
        [HideInInspector] public float GripRadius;
        [Min(0.01f)] public float SwordLength = 1.7f;
        [Min(0.01f)] public float BladeWidth = 0.16f;
        [Min(0)] public float BladeDamage = 2f;
        [Min(0)] public float WaveDamage = 3f;
        [Tooltip("自动贴齐剑尖后的额外偏移，按瞄准坐标解释；判定与图片一起移动。")]
        public Vector2 WaveOffset;
        public float WaveRotation;
        public bool WaveFlipX;
        public bool WaveFlipY;
        [Tooltip("图内对齐到完整剑尖的点：中心为0，坐标均按图宽计。")]
        public Vector2 WaveAttachPoint;
        [Range(0, 1), Tooltip("以挥剑轨迹的哪个位置作为剑气对齐方向；按完整剑长计算。")]
        public float WaveTipProgress = 1f;
        [Min(0.01f)] public float WaveWidth = 3f;
        [Tooltip("剑气图中归一化坐标，以中心为原点，宽度为1。纵坐标也按宽度计。可按实际亮弧修订。")]
        public Vector2[] WavePolygon;
    }
}
