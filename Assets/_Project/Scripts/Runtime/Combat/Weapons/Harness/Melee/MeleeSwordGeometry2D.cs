using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>表现与刀刃检测共用自由挥剑几何；剑气有独立位置，不约束剑尖。</summary>
    public static class MeleeSwordGeometry2D
    {
        public static float SizeAt(HarnessMeleeAttackConfig attack, float progress)
            => MeleeSwordMotion2D.Evaluate(attack.MotionKeys, progress).w;

        public static Vector2 Rotate(Vector2 point, float angle)
        {
            float r = angle * Mathf.Deg2Rad;
            return new Vector2(point.x * Mathf.Cos(r) - point.y * Mathf.Sin(r),
                point.x * Mathf.Sin(r) + point.y * Mathf.Cos(r));
        }

        public static Vector2 AimVector(Vector2 local, float aim)
            => Rotate(new Vector2(local.x, local.y * FacingSign(aim)), aim);

        public static void Evaluate(HarnessMeleeAttackConfig attack, Vector2 origin,
            float aimDegrees, float progress, out Vector2 hilt, out Vector2 tip)
        {
            Vector4 pose = MeleeSwordMotion2D.Evaluate(attack.MotionKeys, progress);
            hilt = origin + AimVector(attack.SwingOffset + new Vector2(pose.x, pose.y), aimDegrees);
            Vector2 direction = Rotate(Vector2.right, aimDegrees + pose.z * FacingSign(aimDegrees));
            tip = hilt + direction * (attack.SwordLength * pose.w);
        }

        /// <summary>任意刀刃点的路程上界：平移 + 旋转 + 伸缩，包含跨多个关键节点的变化。</summary>
        public static float SweepDistanceBound(HarnessMeleeAttackConfig attack, float from, float to)
        {
            Vector4 variation = MeleeSwordMotion2D.TotalVariation(attack.MotionKeys, from, to);
            float length = attack.SwordLength * MeleeSwordMotion2D.MaximumScale(attack.MotionKeys);
            return variation.x + variation.y + length * variation.z * Mathf.Deg2Rad +
                (attack.SwordLength + attack.BladeWidth * .5f) * variation.w;
        }

        public static Vector2 WavePosition(HarnessMeleeAttackConfig attack, Vector2 origin, float aim)
            => origin + AimVector(attack.WaveOffset, aim);

        public static Vector2 WaveSigns(HarnessMeleeAttackConfig attack, float aim)
            => new(attack.WaveFlipX ? -1f : 1f, FacingSign(aim) * (attack.WaveFlipY ? -1f : 1f));

        public static Vector2 WaveLocalVector(HarnessMeleeAttackConfig attack, Vector2 point, float aim)
            => Rotate(Vector2.Scale(point, WaveSigns(attack, aim)) * attack.WaveWidth, WaveAngle(attack, aim));

        // 向左瞄准时镜像，而不是把下劈整套倒转成上挑。
        public static float FacingSign(float aim) => Mathf.Cos(aim * Mathf.Deg2Rad) < -.001f ? -1f : 1f;

        public static float WaveAngle(HarnessMeleeAttackConfig attack, float aim)
            => aim + attack.WaveRotation * FacingSign(aim);
    }
}
