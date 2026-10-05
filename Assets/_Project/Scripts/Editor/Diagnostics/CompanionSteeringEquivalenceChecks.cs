using System;
using System.Reflection;
using System.Text;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Movement;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>保留优化前 Choose/SafeStep 作为只读参考；用固定种子对等价重排逐分量精确比对。</summary>
    public static class CompanionSteeringEquivalenceChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play required; test does not simulate physics.");
            var template = AssetDatabase.LoadAssetAtPath<CompanionTacticsConfig>(
                "Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset");
            var motor = AssetDatabase.LoadAssetAtPath<PlayerMotorConfig>(
                "Assets/_Project/Configs/Players/CFG_PlayerMotor_Default.asset");
            if (template == null || motor == null) throw new InvalidOperationException("Missing production configs.");
            var tactics = UnityEngine.Object.Instantiate(template);
            tactics.hideFlags = HideFlags.HideAndDontSave;
            var root = new GameObject("SteeringEquivalence_Temporary") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var sensor = root.AddComponent<CompanionBattleSensor2D>();
                sensor.Config = tactics;
                sensor.Registry = root.AddComponent<CombatPerceptionRegistry2D>();
                sensor.ObstacleRegistry = root.AddComponent<CompanionObstacleRegistry2D>();
                if (!sensor.Initialize()) throw new InvalidOperationException("Synthetic sensor initialization failed");
                var countField = typeof(CompanionBattleSensor2D).GetField("<ObstacleCount>k__BackingField", Private);
                var saturatedField = typeof(CompanionBattleSensor2D).GetField("<ObstacleSaturated>k__BackingField", Private);
                if (countField == null || saturatedField == null) throw new InvalidOperationException("Snapshot fields missing");
                var random = new System.Random(20261005);
                Rect bounds = motor.MovementBounds;
                var report = new StringBuilder();
                int mismatches = 0;
                const int cases = 512;
                for (int test = 0; test < cases; test++)
                {
                    // 包含场外起点的边界钳制、瞬间重叠、静止、移动危险、饱和与抵达目标。
                    int count = test % 8 == 0 ? 0 : test % 8 == 1 ? 1 :
                        test % 8 == 2 ? 256 : random.Next(2, 129);
                    count = Mathf.Min(count, sensor.Obstacles.Length);
                    bool saturated = test % 17 == 0;
                    Vector2 position = new Vector2(Range(random, bounds.xMin - 1f, bounds.xMax + 1f),
                        Range(random, bounds.yMin - 1f, bounds.yMax + 1f));
                    Vector2 destination = test % 11 == 0 ? position : new Vector2(
                        Range(random, bounds.xMin, bounds.xMax), Range(random, bounds.yMin, bounds.yMax));
                    Vector2 extent = new Vector2(Range(random, .1f, .7f), Range(random, .1f, .8f));
                    Vector2 offset = new Vector2(Range(random, -.3f, .3f), Range(random, -.3f, .3f));
                    Vector2 velocity = test % 7 == 0 ? Vector2.zero :
                        new Vector2(Range(random, -10f, 10f), Range(random, -10f, 10f));
                    Vector2 previous = Vector2.ClampMagnitude(new Vector2(Range(random, -1f, 1f), Range(random, -1f, 1f)), 1f);
                    tactics.PredictionSeconds = Range(random, .08f, .8f);
                    tactics.DecisionInterval = Range(random, .02f, .25f);
                    tactics.NavigationPadding = Range(random, 0f, .2f);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 center = i == 0 && test % 3 == 0 ? position + offset :
                            new Vector2(Range(random, bounds.xMin - 3f, bounds.xMax + 3f),
                                Range(random, bounds.yMin - 3f, bounds.yMax + 3f));
                        Vector2 speed = test % 5 == 0 ? Vector2.zero :
                            new Vector2(Range(random, -8f, 8f), Range(random, -8f, 8f));
                        sensor.Obstacles[i] = new CompanionObstacleSnapshot(center, Range(random, .04f, 1.2f), speed);
                    }
                    countField.SetValue(sensor, count);
                    saturatedField.SetValue(sensor, saturated);
                    Vector2 expected = ReferenceChoose(sensor, tactics, motor, position, velocity, extent, destination, previous, offset);
                    Vector2 actual = CompanionSteering2D.Choose(sensor, tactics, motor, position, velocity, extent, destination, previous, offset);
                    // Vector2 == 使用容差；这里刻意不用它，以免把细微重排漂移隐去。
                    if (expected.x == actual.x && expected.y == actual.y) continue;
                    mismatches++;
                    if (mismatches <= 12) report.AppendLine("Case " + test + ", obstacles=" + count +
                        ", saturated=" + saturated + ": expected=" + expected.ToString("R") + ", actual=" + actual.ToString("R"));
                }
                report.Insert(0, "Steering exact component comparison: " + cases + " deterministic cases, " + mismatches +
                    " mismatches. Seed=20261005. Reference uses current collision geometry but original candidate/control algorithm.\n");
                if (mismatches != 0) throw new InvalidOperationException(report.ToString());
                return report.ToString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(tactics);
            }
        }

        private static float Range(System.Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();

        // Frozen 2026-10-05 pre-optimization Steering. Do not refactor together with runtime Choose.
        private static Vector2 ReferenceChoose(CompanionBattleSensor2D sensor, CompanionTacticsConfig tactics,
            PlayerMotorConfig motor, Vector2 position, Vector2 velocity, Vector2 extent, Vector2 destination,
            Vector2 previousMove, Vector2 colliderOffset)
        {
            Vector2 offset = destination - position;
            Vector2 desired = offset.magnitude <= Mathf.Min(.12f, tactics.ArrivalRadius) ? Vector2.zero :
                offset.normalized * Mathf.Clamp01(offset.magnitude / (motor.MaximumSpeed * .24f));
            Vector2 result = Vector2.zero;
            float best = float.PositiveInfinity;
            for (int i = 0; i < 18; i++)
            {
                Vector2 candidate = i == 0 ? desired : i == 1 ? Vector2.zero :
                    new Vector2(Mathf.Cos((i - 2) % 8 * Mathf.PI / 4), Mathf.Sin((i - 2) % 8 * Mathf.PI / 4)) *
                    (i >= 10 ? .5f : 1f);
                if (sensor.ObstacleSaturated && candidate != Vector2.zero) continue;
                Vector2 end = position, nextVelocity = velocity;
                float elapsed = 0;
                bool safe = true;
                float hold = tactics.DecisionInterval + Time.fixedDeltaTime;
                float horizon = Mathf.Max(tactics.PredictionSeconds, hold + motor.MaximumSpeed / motor.Deceleration);
                while (elapsed < horizon)
                {
                    float step = Mathf.Min(Time.fixedDeltaTime, horizon - elapsed);
                    Vector2 intent = elapsed < hold ? candidate : Vector2.zero;
                    Vector2 previous = end;
                    PlayerMovementStep.Calculate(ref end, ref nextVelocity, intent, motor, extent, colliderOffset, step);
                    end += nextVelocity * step;
                    if (!ReferenceSafeStep(previous + colliderOffset, end + colliderOffset, elapsed, step, extent,
                        tactics.NavigationPadding, sensor.Obstacles, sensor.ObstacleCount)) { safe = false; break; }
                    elapsed += step;
                }
                if (!safe) continue;
                Vector2 averageVelocity = (end - position) / horizon;
                float danger = sensor.Danger(position + colliderOffset, averageVelocity, extent.magnitude);
                float score = Vector2.Distance(end, destination) + tactics.DangerCost * danger +
                    tactics.DirectionChangeCost * (candidate - previousMove).sqrMagnitude;
                if (score >= best) continue;
                best = score;
                result = candidate;
            }
            return result;
        }

        private static bool ReferenceSafeStep(Vector2 from, Vector2 to, float time, float duration, Vector2 extent,
            float padding, CompanionObstacleSnapshot[] obstacles, int count)
        {
            if (CompanionNavigation2D.IsSegmentSafe(from, to, time, duration, extent, padding, obstacles, count)) return true;
            for (int i = 0; i < count; i++)
            {
                var h = obstacles[i];
                float start = Clearance(from, h.Center + h.Velocity * time, extent, h.Radius + padding);
                float finish = Clearance(to, h.Center + h.Velocity * (time + duration), extent, h.Radius + padding);
                if (start <= 0) { if (finish <= start + .0001f) return false; }
                else if (!CompanionNavigation2D.IsObstacleSegmentSafe(from, to, time, duration, extent, padding, h)) return false;
            }
            return true;
        }

        private static float Clearance(Vector2 point, Vector2 center, Vector2 extent, float radius)
        {
            Vector2 d = point - center;
            Vector2 q = new Vector2(Mathf.Abs(d.x), Mathf.Abs(d.y)) - extent;
            return new Vector2(Mathf.Max(0, q.x), Mathf.Max(0, q.y)).magnitude +
                Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
        }
    }
}
