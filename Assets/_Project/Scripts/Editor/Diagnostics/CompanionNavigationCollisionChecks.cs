using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DeepSleep.Runtime.Players.Companion;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>冻结优化前的碰撞算法作独立参照；固定随机与边界对照，再在同一进程内测旧/新短步成本。</summary>
    public static class CompanionNavigationCollisionChecks
    {
        private const float Epsilon = .000001f;
        private const int BatchSize = 4096, Warmup = 4, Samples = 24;
        private static int _benchmarkSink;

        [MenuItem("DeepSleep/验证/导航碰撞旧新算法对照（只读）")]
        private static void RunMenu() => UnityEngine.Debug.Log(Run());

        public static string Run()
        {
            int checks = CheckBoundaries();
            uint state = 0x36FD482Bu;
            for (int i = 0; i < 100000; i++)
            {
                Vector2 center = RandomVector(ref state, -20f, 20f);
                Vector2 from = i % 4 == 0 ? center + RandomVector(ref state, -1.5f, 1.5f) : RandomVector(ref state, -20f, 20f);
                Vector2 to = i % 11 == 0 ? from : from + RandomVector(ref state, -35f, 35f);
                Vector2 extent = i % 13 == 0 ? Vector2.zero : RandomVector(ref state, 0f, 1.5f);
                float start = RandomFloat(ref state, -.5f, 3f);
                float duration = i % 17 == 0 ? 0f : RandomFloat(ref state, -.2f, 3f);
                float horizon = i % 9 == 0 ? float.PositiveInfinity : RandomFloat(ref state, -.2f, 2f);
                var hazard = new CompanionObstacleSnapshot(center, RandomFloat(ref state, -.2f, 1.5f),
                    RandomVector(ref state, -60f, 60f));
                Check(new Query(from, to, start, duration, extent, RandomFloat(ref state, -.1f, .3f), hazard, horizon), checks++);
            }
            var report = new StringBuilder("Navigation collision old/new reference PASS: ").Append(checks)
                .AppendLine(" fixed-seed/edge cases, including horizon split, tangent/corner offsets, negative/zero/tiny duration and high speed.");
            foreach (string name in new[] { "far-short-step", "mixed-short-step", "split-horizon" })
                report.AppendLine(Benchmark(name));
            return report.Append("Same-process Editor microbench; alternating old/new measurement order, fixed inputs, prewarmed. ")
                .Append("No scene objects, assets, damage or motor state changed; no GC-byte or phone-performance claim.").ToString();
        }

        private static int CheckBoundaries()
        {
            int checks = 0;
            Vector2 extent = new Vector2(.195f, .45f);
            var still = new CompanionObstacleSnapshot(Vector2.zero, .62f, Vector2.zero);
            float[] times = { -1f, 0f, Epsilon * .5f, Epsilon, .5f, 2f };
            float[] horizons = { -1f, 0f, .25f, 1.2f, float.PositiveInfinity };
            float[] offsets = { -.000001f, 0f, .000001f };
            foreach (float start in times)
            foreach (float duration in times)
            foreach (float horizon in horizons)
            foreach (float offset in offsets)
            {
                float tangentY = extent.y + .68f + offset;
                Check(new Query(new Vector2(-3f, tangentY), new Vector2(3f, tangentY), start, duration,
                    extent, .06f, still, horizon), checks++);
                float corner = .68f / Mathf.Sqrt(2f) + offset;
                Vector2 point = extent + new Vector2(corner, corner);
                Check(new Query(point, point, start, duration, extent, .06f, still, horizon), checks++);
                var fast = new CompanionObstacleSnapshot(new Vector2(0f, 2f), .62f, new Vector2(0f, -100f));
                Check(new Query(new Vector2(-50f, 0f), new Vector2(50f, 0f), start, duration,
                    extent, .06f, fast, horizon), checks++);
            }
            return checks;
        }

        private static string Benchmark(string name)
        {
            var queries = new Query[BatchSize];
            uint state = 0xC612AE31u;
            for (int i = 0; i < queries.Length; i++)
            {
                Vector2 from = RandomVector(ref state, -1f, 1f);
                Vector2 to = from + RandomVector(ref state, -.08f, .08f);
                Vector2 center = RandomVector(ref state, -10f, 10f);
                if (name == "far-short-step") center.x = (i % 2 == 0 ? -1f : 1f) * (3f + Mathf.Abs(center.x));
                else if (i % 4 == 0) center = from + RandomVector(ref state, -.8f, .8f);
                float start = name == "split-horizon" ? .9f : (i % 12) * .05f;
                float duration = name == "split-horizon" ? .6f : .05f;
                queries[i] = new Query(from, to, start, duration, new Vector2(.195f, .45f), .06f,
                    new CompanionObstacleSnapshot(center, .62f, new Vector2(0f, -.85f)), 1.2f);
                Check(queries[i], i);
            }
            for (int i = 0; i < Warmup; i++) { RunBatch(queries, false); RunBatch(queries, true); }
            var oldTicks = new long[Samples]; var newTicks = new long[Samples];
            for (int i = 0; i < Samples; i++)
            {
                if (i % 2 == 0) { oldTicks[i] = Measure(queries, false); newTicks[i] = Measure(queries, true); }
                else { newTicks[i] = Measure(queries, true); oldTicks[i] = Measure(queries, false); }
            }
            Array.Sort(oldTicks); Array.Sort(newTicks);
            double nanoseconds = 1e9 / Stopwatch.Frequency / BatchSize;
            double oldMedian = oldTicks[Samples / 2] * nanoseconds, newMedian = newTicks[Samples / 2] * nanoseconds;
            return name + ": old p50=" + oldMedian.ToString("F2", CultureInfo.InvariantCulture) +
                " ns/call, new p50=" + newMedian.ToString("F2", CultureInfo.InvariantCulture) +
                " ns/call, ratio=" + (newMedian / oldMedian).ToString("F3", CultureInfo.InvariantCulture) +
                ", calls/implementation=" + (BatchSize * Samples) + ", warmupBatches=" + Warmup + ", hitChecksum=" + _benchmarkSink;
        }

        private static long Measure(Query[] queries, bool current)
        {
            long begin = Stopwatch.GetTimestamp();
            RunBatch(queries, current);
            return Stopwatch.GetTimestamp() - begin;
        }

        private static void RunBatch(Query[] queries, bool current)
        {
            int hits = 0;
            // 分支置于批次外，两个版本的循环体除算法入口以外相同。
            if (current)
            {
                foreach (Query q in queries) if (!CurrentSafe(q)) hits++;
            }
            else
            {
                foreach (Query q in queries) if (!ReferenceSafe(q)) hits++;
            }
            _benchmarkSink = hits;
        }

        private static void Check(Query q, int index)
        {
            bool expectedSafe = ReferenceSafe(q), actualSafe = CurrentSafe(q);
            if (expectedSafe != actualSafe)
                throw new InvalidOperationException("Collision result changed at case " + index +
                    ": expectedSafe=" + expectedSafe + ", actualSafe=" + actualSafe +
                    ", from=" + q.From.ToString("R") + ", to=" + q.To.ToString("R") +
                    ", start=" + q.Start + ", duration=" + q.Duration + ", horizon=" + q.Horizon);
        }

        private static bool CurrentSafe(Query q) => CompanionNavigation2D.IsObstacleSegmentSafe(q.From, q.To,
            q.Start, q.Duration, q.Extent, q.Padding, q.Hazard, q.Horizon);

        private static bool ReferenceSafe(Query q) => ReferenceObstacleSafe(q.From, q.To,
            q.Start, q.Duration, q.Extent, q.Padding, q.Hazard, q.Horizon);

        private static bool ReferenceObstacleSafe(Vector2 from, Vector2 to, float startTime, float duration,
            Vector2 extent, float padding, CompanionObstacleSnapshot hazard, float horizon) =>
            !ReferenceIntersects(from, to, startTime, duration, extent, padding, hazard, horizon);

        // 以下冻结优化前的实现，不调用任何新路径；避免“拿新算法验证新算法”。
        private static bool ReferenceIntersects(Vector2 from, Vector2 to, float startTime, float duration,
            Vector2 extent, float padding, CompanionObstacleSnapshot hazard, float horizon)
        {
            startTime = Mathf.Max(0f, startTime);
            duration = Mathf.Max(0f, duration);
            horizon = Mathf.Max(0f, horizon);
            float endTime = startTime + duration;
            float radius = Mathf.Max(0f, hazard.Radius) + Mathf.Max(0f, padding);
            if (duration > Epsilon && startTime < horizon && endTime > horizon)
            {
                Vector2 split = Vector2.Lerp(from, to, (horizon - startTime) / duration);
                return ReferenceSweptBoxCircle(from - hazard.Center - hazard.Velocity * startTime,
                           split - hazard.Center - hazard.Velocity * horizon, extent, radius) ||
                       ReferenceSweptBoxCircle(split - hazard.Center - hazard.Velocity * horizon,
                           to - hazard.Center - hazard.Velocity * horizon, extent, radius);
            }
            return ReferenceSweptBoxCircle(from - hazard.Center - hazard.Velocity * Mathf.Min(startTime, horizon),
                to - hazard.Center - hazard.Velocity * Mathf.Min(endTime, horizon), extent, radius);
        }

        private static bool ReferenceSweptBoxCircle(Vector2 a, Vector2 b, Vector2 extent, float radius)
        {
            Vector2 limit = extent + Vector2.one * radius;
            if (Mathf.Min(a.x, b.x) > limit.x || Mathf.Max(a.x, b.x) < -limit.x ||
                Mathf.Min(a.y, b.y) > limit.y || Mathf.Max(a.y, b.y) < -limit.y) return false;
            Vector2 d = b - a;
            float enter = 0f, exit = 1f;
            if (ReferenceSlab(a.x, d.x, extent.x, ref enter, ref exit) &&
                ReferenceSlab(a.y, d.y, extent.y, ref enter, ref exit)) return true;
            float distance = Mathf.Min(ReferencePointBox(a, extent), ReferencePointBox(b, extent));
            distance = Mathf.Min(distance, ReferencePointSegment(new Vector2(-extent.x, -extent.y), a, b));
            distance = Mathf.Min(distance, ReferencePointSegment(new Vector2(-extent.x, extent.y), a, b));
            distance = Mathf.Min(distance, ReferencePointSegment(new Vector2(extent.x, -extent.y), a, b));
            distance = Mathf.Min(distance, ReferencePointSegment(new Vector2(extent.x, extent.y), a, b));
            return distance <= radius * radius;
        }

        private static bool ReferenceSlab(float origin, float delta, float extent, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) <= Epsilon) return origin >= -extent && origin <= extent;
            float a = (-extent - origin) / delta, b = (extent - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
            return enter <= exit;
        }

        private static float ReferencePointBox(Vector2 p, Vector2 extent)
        {
            float x = Mathf.Max(0f, Mathf.Abs(p.x) - extent.x), y = Mathf.Max(0f, Mathf.Abs(p.y) - extent.y);
            return x * x + y * y;
        }

        private static float ReferencePointSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude > Epsilon ? Mathf.Clamp01(Vector2.Dot(point - a, d) / d.sqrMagnitude) : 0f;
            return (point - a - d * t).sqrMagnitude;
        }

        private static float RandomFloat(ref uint state, float min, float max)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return min + (max - min) * ((state & 0xFFFFFFu) / 16777215f);
        }
        private static Vector2 RandomVector(ref uint state, float min, float max) =>
            new Vector2(RandomFloat(ref state, min, max), RandomFloat(ref state, min, max));

        private readonly struct Query
        {
            public readonly Vector2 From, To, Extent;
            public readonly float Start, Duration, Padding, Horizon;
            public readonly CompanionObstacleSnapshot Hazard;
            public Query(Vector2 from, Vector2 to, float start, float duration, Vector2 extent, float padding,
                CompanionObstacleSnapshot hazard, float horizon)
            { From = from; To = to; Start = start; Duration = duration; Extent = extent; Padding = padding; Hazard = hazard; Horizon = horizon; }
        }
    }
}
