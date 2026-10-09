using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Progression.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class QuickAppChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string RunCompanionTargets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var original = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var physics = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            int checks = 0;
            void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
            void Awake(object value) => value.GetType().GetMethod("Awake", Private)?.Invoke(value, null);
            try
            {
                DeepSleep.Runtime.Players.Companion.CompanionCommandSource2D brain = null;
                foreach (var root in preview.GetRootGameObjects())
                {
                    foreach (var item in root.GetComponentsInChildren<DeepSleep.Runtime.Players.Companion.CompanionCommandSource2D>(true))
                        if (item.Combat.Role == DeepSleep.Runtime.Players.Identity.PlayerRole.Harness) brain = item;
                    foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                        if (component.GetType().Name == "Light2D") component.enabled = false;
                    SceneManager.MoveGameObjectToScene(root, physics);
                }
                Check(brain != null && brain.Sensor.Initialize(), "HS sensor configured");
                brain.Owner.position = Vector2.zero;
                Awake(brain.Combat.Laser);
                var laserConfig = (DeepSleep.Runtime.Combat.Weapons.Harness.HarnessTerminalLaserConfig)
                    new SerializedObject(brain.Combat.Laser).FindProperty("_config").objectReferenceValue;
                int fired = 0;
                brain.Combat.Laser.FireRequested += _ => fired++;
                string[] paths = {
                    "Assets/_Project/Prefabs/Combat/Enemies/QuickApp/PF_Enemy_QuickApp.prefab",
                    "Assets/_Project/Prefabs/Combat/Enemies/Internet/PF_Enemy_Download.prefab",
                    "Assets/_Project/Prefabs/Combat/Enemies/DataCrawlerSnake/PF_Enemy_DataCrawlerSnake.prefab" };
                var bodies = new DeepSleep.Runtime.Combat.Perception.CombatPerceptionBody2D[3];
                for (int i = 0; i < paths.Length; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]), physics);
                    go.transform.position = new Vector2(i == 2 ? 2 : 10, i == 0 ? 2 : 0);
                    bodies[i] = go.GetComponent<DeepSleep.Runtime.Combat.Perception.CombatPerceptionBody2D>();
                    Awake(bodies[i].Enemy.Health); Awake(bodies[i].Hitbox);
                    bodies[i].Register(brain.Sensor.Registry);
                }
                // 先让近处爬虫成为旧目标，验证惯性分也不能压过高速威胁。
                bodies[0].gameObject.SetActive(false); bodies[1].gameObject.SetActive(false);
                Physics2D.SyncTransforms(); brain.Sensor.Refresh(Vector2.zero, Vector2.zero);
                Check(brain.Sensor.Target == bodies[2], "Near crawler baseline");
                for (int index = 0; index < 2; index++)
                {
                    bodies[index].gameObject.SetActive(true); Physics2D.SyncTransforms();
                    brain.Sensor.Refresh(Vector2.zero, Vector2.zero);
                    Check(brain.Sensor.Target == bodies[index], "Fast enemy beats nearby sticky crawler");
                    brain.Combat.ResetIntent();
                    brain.Combat.Build(brain.Sensor, Vector2.zero, false, false, false, 1,
                        out var aim, out var skill, out var attack, out var cancel);
                    Check((attack & DeepSleep.Runtime.Input.Commands.CommandButtonState.Pressed) != 0, "HS requests lock");
                    var command = new DeepSleep.Runtime.Input.Commands.PlayerCommand(1, 1, Vector2.zero, aim, skill, 0, attack, cancel, 0, 0);
                    brain.Combat.Laser.ConsumeCommand(in command, .02f);
                    Check(brain.Combat.Laser.TryGetSelectedTargetPosition(out var point) &&
                        Vector2.Distance(point, bodies[index].Position) < .001f, "Real HS laser locks fast enemy collider");
                    bodies[index].transform.position = new Vector2(-8, 2); Physics2D.SyncTransforms();
                    var idle = default(DeepSleep.Runtime.Input.Commands.PlayerCommand);
                    brain.Combat.Laser.ConsumeCommand(in idle, .02f);
                    Check(brain.Combat.Laser.TryGetSelectedTargetPosition(out point) &&
                        Vector2.Distance(point, bodies[index].Position) < .001f, "Lock follows moved/wrapped fast enemy");
                    int before = fired;
                    brain.Combat.Laser.ConsumeCommand(in idle, laserConfig.CalibrationSeconds);
                    Check(fired == before + 1, "HS completes calibration and requests fire at fast enemy");
                    brain.Combat.Laser.CancelSelection(); bodies[index].gameObject.SetActive(false);
                    typeof(DeepSleep.Runtime.Combat.Weapons.Harness.HarnessTerminalLaserController)
                        .GetMethod("OnDisable", Private).Invoke(brain.Combat.Laser, null);
                    Physics2D.SyncTransforms(); brain.Sensor.Refresh(Vector2.zero, Vector2.zero);
                }
                return "Companion fast targets: " + checks + " checks passed (isolated physics/real HS command and lock, not natural fight).";
            }
            finally
            {
                EditorSceneManager.CloseScene(physics, true); EditorSceneManager.ClosePreviewScene(preview);
                SceneManager.SetActiveScene(original);
            }
        }
        public static string RunAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int checks = 0;
            void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuickAppInstaller.PrefabRoot + "PF_Enemy_QuickApp.prefab");
            var actor = prefab.GetComponent<EnemyActor2D>(); var motor = prefab.GetComponent<QuickAppMotor2D>();
            var visual = prefab.GetComponent<QuickAppVisual2D>();
            Check(actor.TryValidateConfiguration(out var reason), reason);
            Check(motor.TryValidateConfiguration(out reason), reason);
            Check(visual.MotionTrailPrefab != null && visual.Group != null && visual.Actor == actor && visual.Motor == motor, "Trail assembly");
            Check(Mathf.Abs(QuickAppInstaller.VisibleDiameter(QuickAppInstaller.ArtPath, visual.Renderer.sprite.pixelsPerUnit) * visual.Renderer.transform.lossyScale.x - 1.9f) < .001f, "Visible diameter 1.9u");
            Check(prefab.transform.localScale == Vector3.one && visual.Renderer.transform != prefab.transform, "Physics scale independent");
            var contact = (EnemyContactDamageConfig)new SerializedObject(motor.Contact).FindProperty("_config").objectReferenceValue;
            Check(!contact.DespawnOnImpact && contact.RepeatIntervalSeconds == 1 && contact.KnockbackDistance == 3.6f && contact.KnockbackSeconds == .24f, "Persistent strong knockback");
            var random = new System.Random(302); var repeat = new System.Random(302);
            var a = motor.Config.SampleVariation(random, Vector2.left); var b = motor.Config.SampleVariation(repeat, Vector2.left);
            Check(a.IsValid && a.TravelDirection == b.TravelDirection && a.CurveAmplitude == b.CurveAmplitude && a.CurveWavelength == b.CurveWavelength, "Frozen deterministic random curve");
            Check(a.CurveAmplitude * 2 >= motor.Playfield.WorldBounds.height && a.CurveWavelength >= motor.Playfield.WorldBounds.width, "Screen-sized S");
            Rect bounds = motor.Playfield.WorldBounds;
            Check(Vector2.Distance(QuickAppMotor2D.Wrap(new Vector2(bounds.xMax + .3f, 0), bounds), new Vector2(bounds.xMin + .3f, 0)) < .001f, "Right-left");
            Check(Vector2.Distance(QuickAppMotor2D.Wrap(new Vector2(bounds.xMin - .3f, 0), bounds), new Vector2(bounds.xMax - .3f, 0)) < .001f, "Left-right");
            Check(Vector2.Distance(QuickAppMotor2D.Wrap(new Vector2(0, bounds.yMax + .3f), bounds), new Vector2(0, bounds.yMin + .3f)) < .001f, "Top-bottom");
            Check(Vector2.Distance(QuickAppMotor2D.Wrap(new Vector2(0, bounds.yMin - .3f), bounds), new Vector2(0, bounds.yMax - .3f)) < .001f, "Bottom-top");
            var view = AssetDatabase.LoadAssetAtPath<NetworkEntityView>("Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab");
            Check(view.WrappingTrailSprite == visual.Renderer.sprite && view.WrappingPlayfield == motor.Playfield && view.MotionTrailPrefab == visual.MotionTrailPrefab, "Replica wrap/trail references");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            try
            {
                var bindings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
                var entry = bindings.Enemies.Single(e => e.EntryId == "quick-app");
                Check(entry.Pool != null && entry.Director != null && bindings.WorldSnapshot.EnemyPools.Contains(entry.Pool), "Level/world registration");
                Check(bindings.WorldSnapshot.Catalog.Entries.Any(e => e.Sprite == visual.Renderer.sprite), "Sprite catalog");
                foreach (var effect in entry.Root.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
                {
                    Check(effect.TryValidateConfiguration(out reason), reason);
                    var net = effect.GetComponent<NetworkEffectEventChannel>(); Check(net != null && net.Pool == effect && net.Session == bindings.Session, "Network effect event");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return "QUICKAPP ASSETS PASS " + checks;
        }

        public static string RunPlay()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in isolated World02 Play.");
            var bindings = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
            var entry = bindings.Enemies.Single(e => e.EntryId == "quick-app");
            foreach (var e in bindings.Enemies) { e.Director.Stop(); e.Pool.DespawnAll(EnemyDespawnReason.RunReset); }
            int checks = 0;
            void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
            var originalMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                var prefab = (EnemyActor2D)new SerializedObject(entry.Pool).FindProperty("_enemyPrefab").objectReferenceValue;
                var config = prefab.GetComponent<QuickAppMotor2D>().Config;
                var variation = config.SampleVariation(new System.Random(42), Vector2.left);
                Vector2 start = new Vector2(8, 4); Vector2 normal = new Vector2(-variation.TravelDirection.y, variation.TravelDirection.x);
                Check(entry.Pool.TryRent(start, in variation, out var actor), "Rent real pool actor");
                var motor = actor.GetComponent<QuickAppMotor2D>(); var bounds = motor.Playfield.WorldBounds;
                Physics2D.SyncTransforms();
                int despawns = 0; actor.DespawnRequested += (_, __) => despawns++;
                Vector2 previous = motor.Body.position; bool horizontal = false, vertical = false;
                for (int i = 1; i <= 600; i++)
                {
                    motor.Contact.Simulate(.02f); motor.Simulate(.02f); Physics2D.Simulate(.02f);
                    Vector2 current = motor.Body.position;
                    Check(current.x >= bounds.xMin - .001f && current.x <= bounds.xMax + .001f && current.y >= bounds.yMin - .001f && current.y <= bounds.yMax + .001f, "Remains in arena");
                    float phase = variation.InitialRotationDegrees * Mathf.Deg2Rad;
                    float travelled = variation.TravelSpeed * i * .02f;
                    Vector2 expected = QuickAppMotor2D.Wrap(start + variation.TravelDirection * travelled + normal *
                        (variation.CurveAmplitude * (Mathf.Sin(phase + travelled * 2 * Mathf.PI / variation.CurveWavelength) - Mathf.Sin(phase))), bounds);
                    Check(Vector2.Distance(current, expected) < .008f, $"Continuous giant S after wrap step={i} actual={current} expected={expected} running={motor.IsRunning} health={actor.Health.CurrentHealth}");
                    horizontal |= Mathf.Abs(current.x - previous.x) > bounds.width * .5f;
                    vertical |= Mathf.Abs(current.y - previous.y) > bounds.height * .5f; previous = current;
                }
                Check(horizontal && vertical && motor.WrapSequence > 0 && despawns == 0 && entry.Pool.ActiveCount == 1, "Both axes wrap without exit");
                Vector2 held = motor.Body.position; motor.Simulate(0); Physics2D.Simulate(.02f);
                Check(Vector2.Distance(held, motor.Body.position) < .001f, "Zero-dt stops");
                var trail = (SpriteMotionTrail2D)typeof(QuickAppVisual2D).GetField("_trail", Private).GetValue(actor.GetComponent<QuickAppVisual2D>());
                var renderer = actor.GetComponent<QuickAppVisual2D>().Renderer;
                trail.Sample(renderer, actor.GetComponent<UnityEngine.Rendering.SortingGroup>(), true);
                Check(trail.Renderers.Any(r => r.enabled), "Real actor trail emits");
                entry.Pool.DespawnAll(EnemyDespawnReason.RunReset);
                Check(trail.Renderers.All(r => !r.enabled) && !motor.IsRunning, "Reset clears visual/movement");
                Check(entry.Pool.TryRent(start, in variation, out actor) && actor.Health.CurrentHealth == 3, "Reuse resets health");
                var effects = entry.Root.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true);
                int before = effects.Sum(e => e.ActiveCount);
                var damage = new DamagePacket(100, actor.transform.position, Vector2.right, bindings.gameObject);
                Check(actor.Health.TryReceiveDamage(in damage) && entry.Pool.ActiveCount == 0 && effects.Sum(e => e.ActiveCount) > before, "Defeat returns and plays dedicated effect");
                entry.Director.ApplyRuntimeTuning(true, 0, 1, 4, 1.5f);
                Check(entry.Director.TrySpawnNow(), "Normal director spawning");
                actor = entry.Pool.Instances.First(e => e.gameObject.activeSelf);
                Check(actor.Health.MaximumHealth == 4f, "Wave health multiplier uses shared integer rounding");
                return "QUICKAPP PLAY PASS " + checks;
            }
            finally { entry.Director.Stop(); entry.Pool.DespawnAll(EnemyDespawnReason.RunReset); Physics2D.simulationMode = originalMode; }
        }
    }
}
