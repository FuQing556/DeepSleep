using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>保形三次插值：穿过每张关键姿势，不越过相邻值，也不在每个节点强制停顿。无逐帧分配。</summary>
    public static class MeleeSwordMotion2D
    {
        public static bool IsValid(MeleeSwordPoseKey[] keys)
        {
            if (keys == null || keys.Length < 2 || keys[0].Time != 0 || keys[keys.Length - 1].Time != 1)
                return false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!Finite(keys[i].Time) || (i > 0 && keys[i].Time <= keys[i - 1].Time) || keys[i].Scale <= 0)
                    return false;
                for (int c = 0; c < 4; c++) if (!Finite(keys[i].Channels[c])) return false;
            }
            return true;
        }

        public static Vector4 Evaluate(MeleeSwordPoseKey[] keys, float time)
        {
            if (time <= 0) return keys[0].Channels;
            if (time >= 1) return keys[keys.Length - 1].Channels;
            int i = 0;
            while (i < keys.Length - 2 && keys[i + 1].Time < time) i++;
            float h = keys[i + 1].Time - keys[i].Time;
            float t = (time - keys[i].Time) / h, t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * keys[i].Channels +
                (t3 - 2 * t2 + t) * h * Tangent(keys, i) +
                (-2 * t3 + 3 * t2) * keys[i + 1].Channels +
                (t3 - t2) * h * Tangent(keys, i + 1);
        }

        // 每通道在一段内单调，累计跨过的关键节点即可得到完整变化量，不能只相减区间两端。
        public static Vector4 TotalVariation(MeleeSwordPoseKey[] keys, float from, float to)
        {
            float start = Mathf.Clamp01(Mathf.Min(from, to)), end = Mathf.Clamp01(Mathf.Max(from, to));
            Vector4 previous = Evaluate(keys, start), total = Vector4.zero;
            for (int i = 1; i < keys.Length; i++)
            {
                if (keys[i].Time <= start || keys[i].Time >= end) continue;
                Vector4 next = keys[i].Channels;
                total += Abs(next - previous);
                previous = next;
            }
            return total + Abs(Evaluate(keys, end) - previous);
        }

        public static float MaximumScale(MeleeSwordPoseKey[] keys)
        {
            float maximum = 0;
            for (int i = 0; i < keys.Length; i++) maximum = Mathf.Max(maximum, keys[i].Scale);
            return maximum;
        }

        private static Vector4 Tangent(MeleeSwordPoseKey[] keys, int i)
        {
            if (i == 0) return Slope(keys, 0);
            if (i == keys.Length - 1) return Slope(keys, i - 1);
            float before = keys[i].Time - keys[i - 1].Time, after = keys[i + 1].Time - keys[i].Time;
            float w1 = 2 * after + before, w2 = after + 2 * before;
            Vector4 left = Slope(keys, i - 1), right = Slope(keys, i), result = Vector4.zero;
            for (int c = 0; c < 4; c++)
                if (left[c] * right[c] > 0) result[c] = (w1 + w2) / (w1 / left[c] + w2 / right[c]);
            return result;
        }

        private static Vector4 Slope(MeleeSwordPoseKey[] keys, int i)
            => (keys[i + 1].Channels - keys[i].Channels) / (keys[i + 1].Time - keys[i].Time);
        private static Vector4 Abs(Vector4 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z), Mathf.Abs(v.w));
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
