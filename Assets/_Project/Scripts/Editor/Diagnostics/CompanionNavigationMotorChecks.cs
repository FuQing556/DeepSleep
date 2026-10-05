using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Movement;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// Play 中读取实际角色配置/碰撞体，以独立感知快照运行导航、末端转向和真实 PlayerMovementStep。
    /// 不移动场景角色、不触发伤害、不改资产；不是完整物理或实机验收。
    /// </summary>
    public static class CompanionNavigationMotorChecks
    {
        private static readonly FieldInfo ObstaclesField = typeof(CompanionBattleSensor2D).GetField(
            "<Obstacles>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CountField = typeof(CompanionBattleSensor2D).GetField(
            "<ObstacleCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SaturatedField = typeof(CompanionBattleSensor2D).GetField(
            "<ObstacleSaturated>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("DeepSleep/验证/同伴导航真实电机闭环（Play）")]
        private static void RunMenu() => UnityEngine.Debug.Log(Run());

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode with gameplay scene required.");
            var brains = UnityEngine.Object.FindObjectsByType<CompanionCommandSource2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (brains.Length == 0) throw new InvalidOperationException("No live CompanionCommandSource2D found.");
            var report = new StringBuilder();
            int failed = 0, cases = 0;
            foreach (var brain in brains)
            {
                if (brain.Config == null || brain.MotorConfig == null || brain.Shape == null || brain.Sensor == null)
                    throw new InvalidOperationException("Brain dependencies missing: " + brain.name);
                Vector2 extent = brain.Shape.bounds.extents;
                Vector2 offset = (Vector2)brain.Shape.bounds.center - (Vector2)brain.Owner.position;
                if (extent.x <= 0f || extent.y <= 0f)
                    throw new InvalidOperationException("Actual collider bounds unavailable: " + brain.name);
                Rect bounds = brain.MotorConfig.MovementBounds;
                Vector2 center = bounds.center;
                var testObject = new GameObject("CompanionMotorCheck_IsolatedSensor");
                testObject.hideFlags = HideFlags.HideAndDontSave;
                var sensor = testObject.AddComponent<CompanionBattleSensor2D>();
                sensor.enabled = false;
                sensor.Config = brain.Config;
                // 只给隔离组件注入可见障碍数组；敌弹/怪物快照为空，以隔离导航与电机问题。
                var visible = new CompanionObstacleSnapshot[brain.Config.ObstacleCapacity];
                ObstaclesField.SetValue(sensor, visible);
                try
                {
                    report.AppendLine(brain.name + ": extent=" + extent.ToString("F3") +
                        ", offset=" + offset.ToString("F3") + ", speed=" + brain.MotorConfig.MaximumSpeed +
                        ", acceleration=" + brain.MotorConfig.Acceleration + ", deceleration=" + brain.MotorConfig.Deceleration +
                        ", fixed=" + Time.fixedDeltaTime.ToString("F3") + ", decision=" +
                        (Mathf.CeilToInt(brain.Config.DecisionInterval / Time.fixedDeltaTime - .00001f) * Time.fixedDeltaTime).ToString("F3"));

                    var wall = new CompanionObstacleSnapshot[7];
                    for (int i = 0; i < wall.Length; i++)
                        wall[i] = new CompanionObstacleSnapshot(center + new Vector2(0f, -2.4f + i * .8f), .5f, Vector2.zero);
                    AddCase(report, ref cases, ref failed, RunCase("vertical-wall", brain, sensor, visible, extent, offset,
                        wall, center + new Vector2(-3f, 0f), center + new Vector2(3f, 0f), 18f));

                    var u = new CompanionObstacleSnapshot[11];
                    for (int i = 0; i < 5; i++)
                    {
                        u[i] = new CompanionObstacleSnapshot(center + new Vector2(-.85f, i * .65f), .34f, Vector2.zero);
                        u[i + 5] = new CompanionObstacleSnapshot(center + new Vector2(.85f, i * .65f), .34f, Vector2.zero);
                    }
                    u[10] = new CompanionObstacleSnapshot(center + new Vector2(0f, 2.6f), .52f, Vector2.zero);
                    AddCase(report, ref cases, ref failed, RunCase("U-backtrack", brain, sensor, visible, extent, offset,
                        u, center + new Vector2(0f, 1.1f), center + new Vector2(0f, 3.8f), 18f));

                    var falling = new CompanionObstacleSnapshot[7];
                    for (int i = 0; i < falling.Length; i++)
                        falling[i] = new CompanionObstacleSnapshot(center + new Vector2(0f, -1f + i * .9f), .42f, Vector2.down * .85f);
                    AddCase(report, ref cases, ref failed, RunCase("falling-wall", brain, sensor, visible, extent, offset,
                        falling, center + new Vector2(-3f, -1f), center + new Vector2(3f, 1f), 18f));

                    // 只复制现役注册表中的实际几何，不询问生成器的列/保护路线或改动遭遇状态。
                    var actual = new CompanionObstacleSnapshot[256];
                    int actualCount = brain.Sensor.ObstacleRegistry.CopyVisible(center, 1000f, actual, out bool saturated);
                    if (actualCount > 0 && !saturated)
                    {
                        var maze = new CompanionObstacleSnapshot[actualCount];
                        Array.Copy(actual, maze, actualCount);
                        Vector2 start = PickSafePoint(maze, bounds, extent, center + new Vector2(-3f, -2f));
                        Vector2 goal = PickSafePoint(maze, bounds, extent, center + new Vector2(3f, 2f));
                        AddCase(report, ref cases, ref failed, RunCase("live-maze-snapshot-" + actualCount, brain, sensor, visible,
                            extent, offset, maze, start, goal, 20f));
                    }
                    else report.AppendLine("  live-maze-snapshot SKIPPED: no active bubbles or registry truncated; start World01 maze first.");
                }
                finally { UnityEngine.Object.DestroyImmediate(testObject); }
            }
            report.AppendLine("TOTAL: " + cases + " cases, " + failed + " failed. Contacts are continuous actual AABB/circle entries without padding; " +
                "decision timings include Navigate + Steering, excluding snapshot copy. No attacks, invulnerability, physics contacts, network or new bubble spawns simulated.");
            return report.ToString();
        }

        private static Result RunCase(string name, CompanionCommandSource2D brain, CompanionBattleSensor2D sensor,
            CompanionObstacleSnapshot[] visible, Vector2 extent, Vector2 offset, CompanionObstacleSnapshot[] initial,
            Vector2 startCenter, Vector2 goalCenter, float seconds)
        {
            var cfg = brain.Config;
            var motor = brain.MotorConfig;
            var navigation = new CompanionNavigation2D();
            navigation.Configure(cfg.NavigationCellSize, cfg.NavigationPadding, cfg.NavigationPredictionSeconds,
                cfg.NavigationReplanSeconds, cfg.NavigationGoalHysteresis, cfg.NavigationStuckSeconds, cfg.NavigationMaxExpandedNodes);
            var hazards = (CompanionObstacleSnapshot[])initial.Clone();
            var overlapped = new bool[hazards.Length];
            float dt = Time.fixedDeltaTime;
            int decisionTicks = Mathf.Max(1, Mathf.CeilToInt(cfg.DecisionInterval / dt - .00001f));
            int totalTicks = Mathf.CeilToInt(seconds / dt);
            Vector2 position = startCenter - offset, velocity = Vector2.zero, move = Vector2.zero;
            goalCenter = CompanionThreatMath.ClampPoint(goalCenter, motor.MovementBounds, extent);
            var result = new Result { Name = name, Goal = goalCenter, Final = startCenter, MinimumDistance = Vector2.Distance(startCenter, goalCenter) };
            float lowestY = startCenter.y;
            for (int tick = 0; tick < totalTicks; tick++)
            {
                if (tick % decisionTicks == 0)
                {
                    int count = 0; bool saturated = false;
                    for (int i = 0; i < hazards.Length; i++)
                    {
                        float range = cfg.PerceptionRadius + hazards[i].Radius;
                        if ((hazards[i].Center - position).sqrMagnitude > range * range) continue;
                        if (count >= visible.Length) { saturated = true; continue; }
                        visible[count++] = hazards[i];
                    }
                    CountField.SetValue(sensor, count);
                    SaturatedField.SetValue(sensor, saturated);
                    long begin = Stopwatch.GetTimestamp();
                    var route = navigation.Navigate(visible, count, saturated, motor.MovementBounds, position + offset,
                        extent, velocity, motor.MaximumSpeed, motor.Acceleration, goalCenter, cfg.DecisionInterval);
                    move = CompanionSteering2D.Choose(sensor, cfg, motor, position, velocity, extent,
                        route.Waypoint - offset, move, offset);
                    double elapsed = (Stopwatch.GetTimestamp() - begin) * 1000d / Stopwatch.Frequency;
                    result.Decisions++;
                    result.DecisionTotalMs += elapsed;
                    result.DecisionMaxMs = Math.Max(result.DecisionMaxMs, elapsed);
                    result.MaxExpanded = Mathf.Max(result.MaxExpanded, navigation.LastExpandedNodes);
                    if (route.ShouldWait) result.WaitDecisions++;
                    if (route.IsStuck) result.StuckReplans++;
                }

                Vector2 before = position + offset;
                PlayerMovementStep.Calculate(ref position, ref velocity, move, motor, extent, offset, dt);
                position += velocity * dt;
                Vector2 after = position + offset;
                for (int i = 0; i < hazards.Length; i++)
                {
                    bool contact = !CompanionNavigation2D.IsObstacleSegmentSafe(before, after, 0f, dt,
                        extent, 0f, hazards[i]);
                    if (contact && !overlapped[i]) result.Contacts++;
                    Vector2 nextCenter = hazards[i].Center + hazards[i].Velocity * dt;
                    var next = new CompanionObstacleSnapshot(nextCenter, hazards[i].Radius, hazards[i].Velocity);
                    overlapped[i] = !CompanionNavigation2D.IsObstacleSegmentSafe(after, after, 0f, 0f, extent, 0f, next);
                    hazards[i] = next;
                }
                lowestY = Mathf.Min(lowestY, after.y);
                result.Final = after;
                result.Elapsed = (tick + 1) * dt;
                result.MinimumDistance = Mathf.Min(result.MinimumDistance, Vector2.Distance(after, goalCenter));
                if (Vector2.Distance(after, goalCenter) <= Mathf.Max(.16f, cfg.NavigationCellSize * .5f) && velocity.magnitude < .3f)
                { result.Reached = true; break; }
            }
            result.Backtracked = lowestY < startCenter.y - .5f;
            result.Passed = result.Reached && result.Contacts == 0 &&
                result.MaxExpanded <= cfg.NavigationMaxExpandedNodes && (name != "U-backtrack" || result.Backtracked);
            return result;
        }

        private static Vector2 PickSafePoint(CompanionObstacleSnapshot[] hazards, Rect bounds, Vector2 extent, Vector2 preferred)
        {
            Vector2 best = CompanionThreatMath.ClampPoint(preferred, bounds, extent);
            float score = float.PositiveInfinity;
            for (float y = bounds.yMin + extent.y + .1f; y <= bounds.yMax - extent.y - .1f; y += .36f)
            for (float x = bounds.xMin + extent.x + .1f; x <= bounds.xMax - extent.x - .1f; x += .36f)
            {
                Vector2 point = new Vector2(x, y);
                if (!CompanionNavigation2D.IsSegmentSafe(point, point, 0f, .5f, extent, .06f, hazards, hazards.Length)) continue;
                float distance = (point - preferred).sqrMagnitude;
                if (distance >= score) continue;
                best = point; score = distance;
            }
            if (float.IsPositiveInfinity(score)) throw new InvalidOperationException("Live snapshot has no safe starting sample; do not fabricate a route.");
            return best;
        }

        private static void AddCase(StringBuilder report, ref int cases, ref int failed, Result value)
        {
            cases++;
            if (!value.Passed) failed++;
            report.AppendLine("  " + value.Name + " " + (value.Passed ? "PASS" : "FAIL") + ": contacts=" + value.Contacts +
                ", reached=" + value.Reached + ", time=" + value.Elapsed.ToString("F2") + "s, minGoalDistance=" +
                value.MinimumDistance.ToString("F3") + ", final=" + value.Final.ToString("F3") + ", goal=" + value.Goal.ToString("F3") +
                ", waits=" + value.WaitDecisions + ", stuck=" + value.StuckReplans + ", backtracked=" + value.Backtracked +
                ", expandedMax=" + value.MaxExpanded + ", decisionAvg=" + (value.DecisionTotalMs / Math.Max(1, value.Decisions)).ToString("F3") +
                "ms, max=" + value.DecisionMaxMs.ToString("F3") + "ms, decisions=" + value.Decisions);
        }

        private sealed class Result
        {
            public string Name;
            public bool Passed, Reached, Backtracked;
            public Vector2 Final, Goal;
            public float Elapsed, MinimumDistance;
            public int Contacts, WaitDecisions, StuckReplans, MaxExpanded, Decisions;
            public double DecisionTotalMs, DecisionMaxMs;
        }
    }
}
