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
        [Tooltip("整条挥剑轨迹相对 LaserOrigin 剑柄锚点的偏移，朝左时镜像。")]
        public Vector2 SwingOffset;
        [Tooltip("时间0到1。剑柄相对LaserOrigin；角度0朝右；大小1为配置剑长。每招独立节奏，不绑定剑气。")]
        public MeleeSwordPoseKey[] MotionKeys;
        [Min(0.1f)] public float SwingSeconds = 0.5f;
        [Min(0f)] public float RecoverySeconds = 0.08f;
        [Min(0), Tooltip("挥剑结束后的视觉余势，不再造成刀刃伤害。")]
        public float FollowThroughSeconds = .075f;
        public Vector2 FollowThroughDrift;
        [Range(0, 1)] public float FollowThroughEndScale = .2f;
        [Min(0.01f)] public float SwordLength = 1.7f;
        [Min(0.01f)] public float BladeWidth = 0.16f;
        [Min(0)] public float BladeDamage = 2f;
        [Min(0)] public float WaveDamage = 3f;
        [Tooltip("剑气中心相对LaserOrigin的独立位置，按瞄准坐标解释；不从剑尖反算。")]
        public Vector2 WaveOffset;
        public float WaveRotation;
        public bool WaveFlipX;
        public bool WaveFlipY;
        [Min(0.01f)] public float WaveWidth = 3f;
        [Tooltip("剑气图中归一化坐标，以中心为原点，宽度为1。纵坐标也按宽度计。可按实际亮弧修订。")]
        public Vector2[] WavePolygon;
    }
}
