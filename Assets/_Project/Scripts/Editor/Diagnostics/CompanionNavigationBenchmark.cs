using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DeepSleep.Runtime.Players.Companion;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 固定快照/固定输入的导航微基准；不创建场景对象、不读取隐藏路线、不改配置。
    /// 时间和线程分配只包围 Navigate，完整路径校验/摘要、构造与报告均在计量区外。
    /// </summary>
    public static class CompanionNavigationBenchmark
    {
        private const int WarmupCycles = 8;
        private const int SampleCycles = 48;
        private const int ExpansionLimit = 768;
        private const float Cell = .36f, Padding = .06f, Prediction = 1.2f;
        private const float Replan = .35f, Hysteresis = .35f, Stuck = 1.2f;
        private const float Speed = 6.5f, Acceleration = 30f, DecisionDt = .125f;
        private const ulong HashSeed = 14695981039346656037ul;
        private static readonly Vector2 Extent = new Vector2(.195f, .45f);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static byte[] _calibrationBytes;
        private static bool _allocationCounterSupported;

        [MenuItem("DeepSleep/验证/同伴导航固定输入性能基准（只读）")]
        private static void RunMenu() => UnityEngine.Debug.Log(Run());

        /// <summary>保存首次输出的 behaviorDigest，优化后传回该值，任何结果/路线/展开数变化都会失败。</summary>
        public static string Run(string expectedBehaviorDigest = null)
        {
            string calibration = CalibrateAllocations();
            string regression = CompanionNavigationChecks.Run();
            Workload[] workloads = CreateWorkloads();
            var report = new StringBuilder();
            report.AppendLine("Companion navigation fixed-input benchmark v1");
            report.Append("Unity=").Append(Application.unityVersion).Append(", platform=").Append(Application.platform)
                .Append(", processor=").Append(SystemInfo.processorType).Append(", debugBuild=").Append(UnityEngine.Debug.isDebugBuild)
                .Append(", stopwatchHz=").Append(Stopwatch.Frequency).AppendLine();
            report.Append("warmup=").Append(WarmupCycles).Append(", measuredCycles=").Append(SampleCycles)
                .Append(", cell=").Append(Cell.ToString("F2", Invariant)).Append(", expansionLimit=").Append(ExpansionLimit).AppendLine();
            report.AppendLine(calibration);
            ulong all = HashSeed;
            foreach (Workload workload in workloads)
            {
                Sample sample = Measure(workload);
                all = Hash(all, sample.Digest);
                report.Append(workload.Name).Append(": decisions=").Append(sample.Decisions)
                    .Append(", mean=").Append(sample.MeanMs.ToString("F4", Invariant))
                    .Append("ms, p50=").Append(sample.MedianMs.ToString("F4", Invariant))
                    .Append("ms, p95=").Append(sample.P95Ms.ToString("F4", Invariant))
                    .Append("ms, max=").Append(sample.MaximumMs.ToString("F4", Invariant))
                    .Append("ms, threadBytes=").Append(_allocationCounterSupported ? sample.AllocatedBytes.ToString(Invariant) : "UNAVAILABLE")
                    .Append(", bytes/decision=").Append(_allocationCounterSupported ?
                        ((double)sample.AllocatedBytes / sample.Decisions).ToString("F2", Invariant) : "UNAVAILABLE")
                    .Append(", GC collections=").Append(sample.Gen0).Append('/').Append(sample.Gen1).Append('/').Append(sample.Gen2)
                    .Append(", expandedMax=").Append(sample.MaximumExpanded)
                    .Append(", lastExpandedStateSumPerCycle=").Append(sample.ExpandedPerCycle)
                    .Append(", route/wait/reached/stuck=").Append(sample.Routes).Append('/').Append(sample.Waits)
                    .Append('/').Append(sample.Reached).Append('/').Append(sample.Stucks)
                    .Append(", digest=").Append(sample.Digest.ToString("X16", Invariant)).AppendLine();
            }
            string digest = all.ToString("X16", Invariant);
            if (!string.IsNullOrEmpty(expectedBehaviorDigest) &&
                !string.Equals(digest, expectedBehaviorDigest.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Navigation behavior digest changed: expected " + expectedBehaviorDigest +
                    ", actual " + digest + ".\n" + report);
            report.Append("behaviorDigest=").Append(digest).AppendLine();
            report.AppendLine(regression);
            report.Append("PASS: deterministic full-path/result/expanded-node signatures and per-decision expansion bound. ")
                .Append("All inputs stay fixed across revisions; sequence cases retain navigation state but do not simulate a motor. ")
                .Append("Times/bytes exclude fixture construction, Reset, CopyPath, assertions and reports; collection counts span the measured loop. ")
                .Append("No forced GC. Editor microbenchmark only, not phone performance or full gameplay acceptance.");
            return report.ToString();
        }

        private static string CalibrateAllocations()
        {
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            _calibrationBytes = new byte[4096];
            _calibrationBytes[4095] = 123;
            long measured = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(_calibrationBytes);
            _allocationCounterSupported = measured >= 4096;
            return "GC thread-counter calibration: retained new byte[4096], observed=" + measured +
                " B; " + (_allocationCounterSupported ? "SUPPORTED" : "UNAVAILABLE (zero is not a zero-allocation result)");
        }

        private static Sample Measure(Workload workload)
        {
            var navigation = new CompanionNavigation2D();
            navigation.Configure(Cell, Padding, Prediction, Replan, Hysteresis, Stuck, ExpansionLimit);
            var path = new Vector2[4096];
            var durations = new long[workload.Frames.Length * SampleCycles];
            // 预热包括计时/分配 API、所有输入帧、碰撞校验与摘要，避免把 JIT 首次调用记入热路径。
            for (int cycle = 0; cycle < WarmupCycles; cycle++)
            {
                navigation.Reset();
                foreach (Frame frame in workload.Frames)
                {
                    GC.GetAllocatedBytesForCurrentThread(); Stopwatch.GetTimestamp();
                    CompanionNavigationResult2D value = Navigate(navigation, frame);
                    ValidateAndHash(navigation, frame, value, path, HashSeed);
                }
            }
            var sample = new Sample();
            long totalTicks = 0;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            for (int cycle = 0; cycle < SampleCycles; cycle++)
            {
                navigation.Reset();
                ulong digest = HashSeed;
                int expanded = 0;
                foreach (Frame frame in workload.Frames)
                {
                    long allocated = _allocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() : 0;
                    long begin = Stopwatch.GetTimestamp();
                    CompanionNavigationResult2D value = Navigate(navigation, frame);
                    long elapsed = Stopwatch.GetTimestamp() - begin;
                    if (_allocationCounterSupported)
                        sample.AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
                    durations[sample.Decisions++] = elapsed;
                    totalTicks += elapsed;
                    sample.MaximumExpanded = Math.Max(sample.MaximumExpanded, navigation.LastExpandedNodes);
                    expanded += navigation.LastExpandedNodes;
                    digest = ValidateAndHash(navigation, frame, value, path, digest);
                    if (cycle == 0)
                    {
                        if (value.HasPath) sample.Routes++;
                        if (value.ShouldWait) sample.Waits++;
                        if (value.ReachedGoal) sample.Reached++;
                        if (value.IsStuck) sample.Stucks++;
                    }
                }
                if (cycle == 0) { sample.Digest = digest; sample.ExpandedPerCycle = expanded; }
                else if (sample.Digest != digest || sample.ExpandedPerCycle != expanded)
                    throw new InvalidOperationException("Non-deterministic navigation in " + workload.Name + ", cycle=" + cycle);
            }
            sample.Gen0 = GC.CollectionCount(0) - g0;
            sample.Gen1 = GC.CollectionCount(1) - g1;
            sample.Gen2 = GC.CollectionCount(2) - g2;
            Array.Sort(durations);
            double milliseconds = 1000d / Stopwatch.Frequency;
            sample.MeanMs = totalTicks * milliseconds / durations.Length;
            sample.MedianMs = durations[durations.Length / 2] * milliseconds;
            sample.P95Ms = durations[Math.Max(0, (int)Math.Ceiling(durations.Length * .95) - 1)] * milliseconds;
            sample.MaximumMs = durations[durations.Length - 1] * milliseconds;
            return sample;
        }

        private static CompanionNavigationResult2D Navigate(CompanionNavigation2D navigation, Frame frame) =>
            navigation.Navigate(frame.Hazards, frame.Hazards.Length, false, frame.Bounds, frame.Position, Extent,
                frame.Velocity, Speed, Acceleration, frame.Goal, DecisionDt);

        private static ulong ValidateAndHash(CompanionNavigation2D navigation, Frame frame,
            CompanionNavigationResult2D value, Vector2[] path, ulong hash)
        {
            Require(navigation.LastExpandedNodes <= ExpansionLimit, "Navigation exceeded its expansion budget.");
            Require(value.HasPath != value.ShouldWait, "Path/wait flags are inconsistent.");
            Require(!value.ReachedGoal || value.ShouldWait, "Reached goal did not wait.");
            int count = navigation.CopyPath(path);
            Require(count == value.PathLength && count == navigation.RemainingPathCount, "Path copy/result count mismatch.");
            if (value.HasPath)
            {
                Require(count > 0 && path[0] == value.Waypoint, "Waypoint differs from the first remaining path point.");
                float delay = Mathf.Max(0f, Speed - Vector2.Dot(frame.Velocity, (frame.Goal - frame.Position).normalized)) / Acceleration;
                float duration = Vector2.Distance(frame.Position, value.Waypoint) / Speed + delay;
                Require(CompanionNavigation2D.IsSegmentSafe(frame.Position, value.Waypoint, 0f, duration,
                    Extent, Padding, frame.Hazards, frame.Hazards.Length, Prediction), "Returned waypoint segment is unsafe.");
            }
            Require(!frame.RequireRoute || value.HasPath, "Expected fixture route was not found.");
            Require(!frame.RequireBacktrack || value.Waypoint.y < frame.Position.y, "U fixture did not backtrack.");
            hash = HashVector(hash, value.Waypoint);
            hash = Hash(hash, (uint)((value.HasPath ? 1 : 0) | (value.ShouldWait ? 2 : 0) |
                (value.ReachedGoal ? 4 : 0) | (value.IsStuck ? 8 : 0)));
            hash = Hash(hash, (uint)navigation.LastExpandedNodes);
            hash = Hash(hash, (uint)count);
            for (int i = 0; i < count; i++) hash = HashVector(hash, path[i]);
            return hash;
        }

        private static Workload[] CreateWorkloads()
        {
            var empty = Array.Empty<CompanionObstacleSnapshot>();
            var wall = new CompanionObstacleSnapshot[7];
            for (int i = 0; i < wall.Length; i++) wall[i] = Obstacle(0f, -2.4f + i * .8f, .5f);
            var u = new CompanionObstacleSnapshot[11];
            for (int i = 0; i < 5; i++)
            { u[i] = Obstacle(-.85f, i * .65f, .34f); u[i + 5] = Obstacle(.85f, i * .65f, .34f); }
            u[10] = Obstacle(0f, 2.6f, .52f);
            var partition = new CompanionObstacleSnapshot[15];
            for (int i = 0; i < partition.Length; i++) partition[i] = Obstacle(0f, -5.6f + i * .8f, .6f);
            Rect local = new Rect(-5f, -4.5f, 10f, 9f), world = new Rect(-9.6f, -5.4f, 19.2f, 10.8f);
            var cached = new Frame[48];
            var waiting = new Frame[48];
            var falling = new Frame[48];
            for (int tick = 0; tick < cached.Length; tick++)
            {
                // 轨迹由固定输入定义，不让旧/新导航的输出反馈改写下一帧输入。
                cached[tick] = new Frame(empty, local, new Vector2(-3f + tick * .05f, -1f), new Vector2(3f, -1f),
                    new Vector2(.4f, 0f));
                waiting[tick] = new Frame(partition, new Rect(-4f, -4f, 8f, 8f), new Vector2(-.9f, 0f), new Vector2(2f, 0f));
                var moving = new CompanionObstacleSnapshot[7];
                for (int i = 0; i < moving.Length; i++)
                    moving[i] = new CompanionObstacleSnapshot(new Vector2(0f, -1f + i * .9f - tick * DecisionDt * .85f),
                        .42f, Vector2.down * .85f);
                falling[tick] = new Frame(moving, local, new Vector2(-3f, -1f), new Vector2(3f, 1f));
            }
            return new[]
            {
                new Workload("direct-cold", new Frame(empty, local, new Vector2(-3f, 0f), new Vector2(3f, 0f), requireRoute: true)),
                new Workload("wall-cold-7", new Frame(wall, local, new Vector2(-3f, 0f), new Vector2(3f, 0f), requireRoute: true)),
                new Workload("U-backtrack-cold-11", new Frame(u, new Rect(-4f, -3f, 8f, 7.5f),
                    new Vector2(0f, 1.1f), new Vector2(0f, 3.8f), requireRoute: true, requireBacktrack: true)),
                new Workload("blocked-cold-15", new Frame(partition, new Rect(-4f, -4f, 8f, 8f),
                    new Vector2(-.9f, 0f), new Vector2(2f, 0f))),
                new Workload("dense-cold-128", new Frame(Dense(128), world, new Vector2(-8.7f, -3.8f), new Vector2(8.7f, 3.8f))),
                new Workload("dense-cold-256", new Frame(Dense(256), world, new Vector2(-8.7f, -3.8f), new Vector2(8.7f, 3.8f))),
                new Workload("cached-route-sequence", cached),
                new Workload("blocked-wait-sequence", waiting),
                new Workload("falling-wall-sequence", falling)
            };
        }

        private static CompanionObstacleSnapshot[] Dense(int count)
        {
            var hazards = new CompanionObstacleSnapshot[count];
            int rows = count / 16;
            for (int i = 0; i < count; i++)
            {
                int x = i % 16, y = i / 16;
                float rowSpacing = rows == 8 ? 1.3f : .65f;
                hazards[i] = new CompanionObstacleSnapshot(new Vector2((x - 7.5f) * .85f, (y - (rows - 1) * .5f) * rowSpacing),
                    .23f + (i % 3) * .035f, new Vector2(0f, -(i % 4) * .15f));
            }
            return hazards;
        }

        private static CompanionObstacleSnapshot Obstacle(float x, float y, float radius) =>
            new CompanionObstacleSnapshot(new Vector2(x, y), radius, Vector2.zero);
        private static ulong Hash(ulong hash, ulong value) => unchecked((hash ^ value) * 1099511628211ul);
        private static ulong HashVector(ulong hash, Vector2 value) =>
            Hash(Hash(hash, unchecked((uint)BitConverter.SingleToInt32Bits(value.x))), unchecked((uint)BitConverter.SingleToInt32Bits(value.y)));
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private readonly struct Frame
        {
            public readonly CompanionObstacleSnapshot[] Hazards;
            public readonly Rect Bounds;
            public readonly Vector2 Position, Goal, Velocity;
            public readonly bool RequireRoute, RequireBacktrack;
            public Frame(CompanionObstacleSnapshot[] hazards, Rect bounds, Vector2 position, Vector2 goal,
                Vector2 velocity = default, bool requireRoute = false, bool requireBacktrack = false)
            {
                Hazards = hazards; Bounds = bounds; Position = position; Goal = goal; Velocity = velocity;
                RequireRoute = requireRoute; RequireBacktrack = requireBacktrack;
            }
        }

        private sealed class Workload
        {
            public readonly string Name;
            public readonly Frame[] Frames;
            public Workload(string name, params Frame[] frames) { Name = name; Frames = frames; }
        }

        private sealed class Sample
        {
            public int Decisions, Gen0, Gen1, Gen2, MaximumExpanded, ExpandedPerCycle, Routes, Waits, Reached, Stucks;
            public long AllocatedBytes;
            public ulong Digest;
            public double MeanMs, MedianMs, P95Ms, MaximumMs;
        }
    }
}
