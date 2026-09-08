using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>表现与刀刃检测共同使用同一几何，不从已渲染的图片反推伤害。</summary>
    public static class MeleeSwordGeometry2D
    {
        public static float SizeAt(float progress)
            => SizeAt(progress, 0.1f, 0.1f, 0.02f);

        public static float SizeAt(HarnessMeleeAttackConfig attack, float progress)
            => SizeAt(progress, attack.GrowFraction, attack.ShrinkFraction, attack.MinimumSwordScale);

        private static float SizeAt(float progress, float grow, float shrink, float minimum)
        {
            float t = Mathf.Clamp01(progress);
            return t < grow ? Mathf.SmoothStep(minimum, 1f, t / grow) :
                t <= 1f - shrink ? 1f : Mathf.SmoothStep(1f, minimum, (t - (1f - shrink)) / shrink);
        }

        public static Vector2 Rotate(Vector2 point, float angle)
        {
            float r = angle * Mathf.Deg2Rad;
            return new Vector2(point.x * Mathf.Cos(r) - point.y * Mathf.Sin(r),
                point.x * Mathf.Sin(r) + point.y * Mathf.Cos(r));
        }

        public static void Evaluate(HarnessMeleeAttackConfig attack, Vector2 origin,
            float aimDegrees, float progress, out Vector2 hilt, out Vector2 tip)
        {
            float angle = Mathf.LerpUnclamped(attack.StartAngle, attack.EndAngle,
                attack.Travel.Evaluate(Mathf.Clamp01(progress))) * Mathf.Deg2Rad;
            Vector2 orbit = new(Mathf.Cos(angle), Mathf.Sin(angle) * attack.OrbitHeight * FacingSign(aimDegrees));
            Vector2 direction = Rotate(orbit.normalized, aimDegrees);
            Vector2 offset = attack.SwingOffset;
            offset.y *= FacingSign(aimDegrees);
            hilt = origin + Rotate(offset, aimDegrees);
            tip = hilt + direction * (attack.SwordLength * SizeAt(attack, progress));
        }

        public static Vector2 WavePosition(HarnessMeleeAttackConfig attack, Vector2 origin, float aim)
        {
            Evaluate(attack, origin, aim, attack.WaveTipProgress, out Vector2 hilt, out Vector2 tip);
            Vector2 fullTip = hilt + (tip - hilt).normalized * attack.SwordLength;
            Vector2 offset = Rotate(new Vector2(attack.WaveOffset.x,
                attack.WaveOffset.y * FacingSign(aim)), aim);
            return fullTip + offset - WaveLocalVector(attack, attack.WaveAttachPoint, aim);
        }

        public static Vector2 WaveSigns(HarnessMeleeAttackConfig attack, float aim)
            => new(attack.WaveFlipX ? -1f : 1f, FacingSign(aim) * (attack.WaveFlipY ? -1f : 1f));

        public static Vector2 WaveLocalVector(HarnessMeleeAttackConfig attack, Vector2 point, float aim)
            => Rotate(Vector2.Scale(point, WaveSigns(attack, aim)) * attack.WaveWidth, WaveAngle(attack, aim));

        // 向左瞄准时镜像，而不是把下劈整套倒转成上挑。
        public static float FacingSign(float aim) => Mathf.Cos(aim * Mathf.Deg2Rad) < -0.001f ? -1f : 1f;

        public static float WaveAngle(HarnessMeleeAttackConfig attack, float aim)
            => aim + attack.WaveRotation * FacingSign(aim);
    }
}
