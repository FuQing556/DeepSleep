using System;
using System.Reflection;
using DeepSleep.Runtime.Players.Companion;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>验证候选位集优化仍按原危险体索引升序遍历；不创建实体、不改运行时 API。</summary>
    public static class CompanionNavigationHotPathChecks
    {
        public static string Run()
        {
            MethodInfo method = typeof(CompanionNavigation2D).GetMethod("LowestSetBitIndex",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new InvalidOperationException("Navigation bit-scan helper was not found.");
            var scan = (Func<uint, int>)Delegate.CreateDelegate(typeof(Func<uint, int>), method);
            int cases = 0, bits = 0;
            for (int low = 0; low < 32; low++)
            {
                Check(1u << low, scan, ref cases, ref bits);
                for (int high = low + 1; high < 32; high++)
                    Check((1u << low) | (1u << high), scan, ref cases, ref bits);
            }
            Check(uint.MaxValue, scan, ref cases, ref bits);
            uint state = 0xA17C9E53u;
            for (int i = 0; i < 4096; i++)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                if (state != 0) Check(state, scan, ref cases, ref bits);
            }
            return "Navigation candidate bit-scan PASS: " + cases + " masks, " + bits +
                " set bits; all 32 indices, all two-bit pairs and fixed-seed dense/sparse masks preserve ascending enumeration.";
        }

        private static void Check(uint mask, Func<uint, int> scan, ref int cases, ref int testedBits)
        {
            uint remaining = mask, seen = 0;
            int previous = -1;
            while (remaining != 0)
            {
                int expected = 0;
                for (uint probe = remaining; (probe & 1u) == 0; probe >>= 1) expected++;
                int actual = scan(remaining);
                if (actual != expected || actual <= previous)
                    throw new InvalidOperationException("Bit-scan order differs from original per-bit traversal: " + mask);
                seen |= 1u << actual;
                previous = actual;
                remaining &= remaining - 1u;
                testedBits++;
            }
            if (seen != mask) throw new InvalidOperationException("Candidate bit enumeration omitted or added a hazard.");
            cases++;
        }
    }
}
