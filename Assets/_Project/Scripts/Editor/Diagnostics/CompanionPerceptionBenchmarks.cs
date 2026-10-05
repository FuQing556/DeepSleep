using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Movement;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 独立合成几何的 Editor/Mono 微基准；不推进物理、不调用真实角色 Brain，不代表实机帧率。
    /// 临时配置/碰撞体均在测量前创建，测量区间不包含反射、字符串或资源导入。
    /// 每项暖机后测 21 个批次，输出每次调用耗时的中位数/P95及本线程托管分配。
    /// </summary>
    public static class CompanionPerceptionBenchmarks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(1000f, 1000f);
        private static float _sink;
        private static byte[] _retainedCalibration;
        private static bool _byteCounterAvailable, _allocationProbeAvailable;

        public static string Run()
        {
            if (!EditorApplication.isPlaying || !Mathf.Approximately(Time.timeScale, 0f))
                throw new InvalidOperationException("Use Play on an idle selection screen (timeScale=0). Do not run during a match.");
            var template = AssetDatabase.LoadAssetAtPath<CompanionTacticsConfig>(
                "Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset");
            var motorTemplate = AssetDatabase.LoadAssetAtPath<PlayerMotorConfig>(
                "Assets/_Project/Configs/Players/CFG_PlayerMotor_Default.asset");
            if (template == null || motorTemplate == null) throw new InvalidOperationException("Missing production AI/motor config.");
            var report = new StringBuilder("Editor/Mono synthetic CPU microbenchmark; NOT mobile/device or full-brain acceptance.\n");
            report.AppendLine("Unity=" + Application.unityVersion + ", fixedDelta=" + Time.fixedDeltaTime.ToString("F4") +
                ", decision=" + template.DecisionInterval.ToString("F4") + ", prediction=" + template.PredictionSeconds.ToString("F4"));
            report.AppendLine("P95 is a batch mean percentile, not an individual frame spike. Allocations exclude native engine memory.");
            CalibrateAllocations(report);
            foreach (int count in new[] { 0, 64, 128, 256 })
            {
                using var fixture = new Fixture(template, motorTemplate, count, 0);
                var buffer = new CompanionObstacleSnapshot[template.ObstacleCapacity];
                Action copy = () => _sink = fixture.Obstacles.CopyVisible(Origin, template.PerceptionRadius, buffer, out _);
                Action refresh = () => { fixture.First.Refresh(Origin, Origin + Vector2.left); _sink = fixture.First.ObstacleCount; };
                Action pair = () =>
                {
                    fixture.First.Refresh(Origin, Origin + Vector2.left);
                    fixture.Second.Refresh(Origin + Vector2.left, Origin);
                    _sink = fixture.First.ObstacleCount + fixture.Second.ObstacleCount;
                };
                refresh();
                Require(fixture.First.ObstacleCount == count && !fixture.First.ObstacleSaturated,
                    "Synthetic obstacle fixture count differs: " + fixture.First.ObstacleCount + "/" + count);
                report.AppendLine("Hazards=" + count + ", projectiles=0:");
                Measure(report, "Registry.CopyVisible", copy, 64);
                Measure(report, "Sensor.Refresh one brain", refresh, 32);
                Measure(report, "Sensor.Refresh two brains", pair, 16);
                Vector2 extent = new Vector2(.35f, .55f);
                Action steer = () =>
                {
                    Vector2 move = CompanionSteering2D.Choose(fixture.First, fixture.Config, fixture.Motor,
                        Origin, Vector2.zero, extent, Origin + Vector2.right * 2f, Vector2.zero, Vector2.zero);
                    _sink = move.x + move.y;
                };
                Measure(report, "Steering open local corridor", steer, 2);
                if (count > 0)
                {
                    fixture.PlaceWall();
                    refresh();
                    Measure(report, "Steering blocked forward corridor", steer, 2);
                }
            }
            // 实际 CombatPerceptionBody2D 的原生属性读路径；临时弹体仅设置已租出状态，绝不提交伤害/推进物理。
            using (var fixture = new Fixture(template, motorTemplate, 0, 64))
            {
                fixture.First.Refresh(Origin, Origin + Vector2.left);
                Require(!fixture.First.Saturated && fixture.First.NearbyCount > 0, "Synthetic projectile fixture was not observed");
                report.AppendLine("Hazards=0, observable projectile bodies=64:");
                Action refresh = () => fixture.First.Refresh(Origin, Origin + Vector2.left);
                Action danger = () => _sink = fixture.First.Danger(Origin, Vector2.right, .65f);
                Action nearest = () =>
                {
                    _sink = fixture.First.TryGetNearestThreat(Origin, 4f, out var point) ? point.x : 0f;
                };
                Action steer = () =>
                {
                    Vector2 move = CompanionSteering2D.Choose(fixture.First, fixture.Config, fixture.Motor,
                        Origin, Vector2.zero, new Vector2(.35f, .55f), Origin + Vector2.right * 2f,
                        Vector2.zero, Vector2.zero);
                    _sink = move.x + move.y;
                };
                Measure(report, "Sensor.Refresh native bodies", refresh, 32);
                Measure(report, "Sensor.Danger one evaluation", danger, 64);
                Measure(report, "Sensor.NearestThreat one scan", nearest, 64);
                Measure(report, "Steering with 64 native threats", steer, 2);
            }
            report.AppendLine("Checksum sink=" + _sink.ToString("F4") + "; rerun 3 times after import/compilation has settled.");
            return report.ToString();
        }

        private static void Measure(StringBuilder report, string label, Action action, int batch)
        {
            const int samples = 21;
            var timings = new double[samples];
            for (int warm = 0; warm < 16; warm++) action();
            long allocated = 0;
            for (int sample = 0; sample < samples; sample++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < batch; i++) action();
                long end = Stopwatch.GetTimestamp();
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                timings[sample] = (end - start) * 1000000d / Stopwatch.Frequency / batch;
            }
            Array.Sort(timings);
            string allocations = _byteCounterAvailable ? allocated + " B" : "bytes unavailable (thread counter failed retained-4096 calibration)";
            if (_allocationProbeAvailable)
            {
                // GC.Alloc recorder 单独测，不把 profiler 插桩的成本混入上面的 CPU 时间。
                using var probe = new EditorAllocationProbe();
                probe.Begin();
                for (int i = 0; i < samples * batch; i++) action();
                EditorAllocationProbe.Result result = probe.End();
                allocations += result.IsValid ? ", GC.Alloc=" + result.Count + " events" : ", GC.Alloc unavailable/overflow";
            }
            else allocations += ", GC.Alloc unavailable";
            report.AppendLine("  " + label + ": median=" + timings[samples / 2].ToString("F2") +
                " us/call, P95=" + timings[19].ToString("F2") + " us/call, " + allocations +
                " / " + samples * batch + " calls");
        }

        private static void CalibrateAllocations(StringBuilder report)
        {
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            _retainedCalibration = new byte[4096];
            _retainedCalibration[4095] = 123;
            long observed = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(_retainedCalibration);
            _byteCounterAvailable = observed >= 4096;
            report.AppendLine("Thread byte counter retained-4096 calibration: " +
                (_byteCounterAvailable ? "PASS" : "UNAVAILABLE") + ", observed=" + observed + " B.");
            try
            {
                report.AppendLine(EditorAllocationProbe.Calibrate());
                _allocationProbeAvailable = true;
            }
            catch (Exception exception)
            {
                _allocationProbeAvailable = false;
                report.AppendLine("GC.Alloc unavailable: " + exception.Message);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly CompanionTacticsConfig Config;
            public readonly PlayerMotorConfig Motor;
            public readonly CompanionObstacleRegistry2D Obstacles;
            public readonly CompanionBattleSensor2D First, Second;
            private readonly GameObject _root;
            private readonly Rigidbody2D[] _hazards;

            public Fixture(CompanionTacticsConfig template, PlayerMotorConfig motorTemplate, int hazardCount, int projectileCount)
            {
                Config = UnityEngine.Object.Instantiate(template);
                Config.hideFlags = HideFlags.HideAndDontSave;
                // 测试专用层、离屏坐标隔离场景物体；不改变项目 Layer 矩阵。
                Config.PerceptionLayers = 1 << 31;
                Motor = UnityEngine.Object.Instantiate(motorTemplate);
                Motor.hideFlags = HideFlags.HideAndDontSave;
                Rect bounds = Motor.MovementBounds;
                bounds.center += Origin;
                Set(Motor, "movementBounds", bounds);
                _root = new GameObject("CompanionPerceptionBenchmark_Temporary") { hideFlags = HideFlags.HideAndDontSave };
                _root.SetActive(false);
                Obstacles = _root.AddComponent<CompanionObstacleRegistry2D>();
                var enemies = _root.AddComponent<CombatPerceptionRegistry2D>();
                First = _root.AddComponent<CompanionBattleSensor2D>();
                Second = _root.AddComponent<CompanionBattleSensor2D>();
                Initialize(First, enemies);
                Initialize(Second, enemies);
                _hazards = new Rigidbody2D[hazardCount];
                try
                {
                    for (int i = 0; i < hazardCount; i++)
                    {
                        var shape = Circle("Hazard", 30, Origin + new Vector2(-5.4f + (i % 16) * .72f, 2f + (i / 16) * .72f),
                            .30f, out var body);
                        _hazards[i] = body;
                        Require(Obstacles.Register(shape, Vector2.down * .85f, First), "Obstacle registration failed");
                    }
                    for (int i = 0; i < projectileCount; i++)
                    {
                        Vector2 offset = new Vector2(-2.1f + (i % 8) * .6f, -2.1f + (i / 8) * .6f);
                        var shape = Circle("ObservableProjectile", 31, Origin + offset, .12f, out var body);
                        body.linearVelocity = -offset.normalized * 2f;
                        var projectile = shape.gameObject.AddComponent<EnemyProjectile2D>();
                        Set(projectile, "_body", body);
                        Set(projectile, "_bodyCollider", shape);
                        Set(projectile, "<IsRented>k__BackingField", true);
                        var perception = shape.gameObject.AddComponent<CombatPerceptionBody2D>();
                        perception.Shape = shape;
                        perception.Body = body;
                        perception.Projectile = projectile;
                        perception.Register(enemies);
                    }
                    _root.SetActive(true);
                    Physics2D.SyncTransforms();
                }
                catch { Dispose(); throw; }
            }

            private void Initialize(CompanionBattleSensor2D sensor, CombatPerceptionRegistry2D enemies)
            {
                sensor.Config = Config;
                sensor.Registry = enemies;
                sensor.ObstacleRegistry = Obstacles;
                Require(sensor.Initialize(), "Temporary sensor initialization failed");
            }

            private CircleCollider2D Circle(string label, int layer, Vector2 position, float radius, out Rigidbody2D body)
            {
                var child = new GameObject(label) { layer = layer, hideFlags = HideFlags.HideAndDontSave };
                child.transform.SetParent(_root.transform, false);
                child.transform.position = position;
                body = child.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.interpolation = RigidbodyInterpolation2D.None;
                var shape = child.AddComponent<CircleCollider2D>();
                shape.radius = radius;
                shape.isTrigger = true;
                return shape;
            }

            public void PlaceWall()
            {
                for (int i = 0; i < _hazards.Length; i++)
                {
                    Vector2 position = Origin + new Vector2(1.2f + (i % 16) * .55f, -5.625f + (i / 16) * .75f);
                    _hazards[i].position = position;
                    _hazards[i].transform.position = position;
                }
                Physics2D.SyncTransforms();
            }

            public void Dispose()
            {
                if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
                if (Config != null) UnityEngine.Object.DestroyImmediate(Config);
                if (Motor != null) UnityEngine.Object.DestroyImmediate(Motor);
            }
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, Private);
            Require(field != null, "Missing fixture field: " + fieldName);
            field.SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
