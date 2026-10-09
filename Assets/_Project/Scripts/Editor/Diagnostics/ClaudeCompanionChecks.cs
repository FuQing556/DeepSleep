using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Encounters;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Input.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudeCompanionChecks
    {
        private static void Awake(object value) => value.GetType().GetMethod("Awake",
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(value, null);
        [MenuItem("DeepSleep/Diagnostics/Claude Companion")]
        private static void Menu() => Debug.Log(Run());
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play");
            var original = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var physics = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
            try
            {
                ClaudeEncounter2D encounter = null;
                CompanionCommandSource2D[] brains = null;
                var list = new System.Collections.Generic.List<CompanionCommandSource2D>();
                foreach (var root in preview.GetRootGameObjects())
                {
                    if (encounter == null) encounter = root.GetComponentInChildren<ClaudeEncounter2D>(true);
                    list.AddRange(root.GetComponentsInChildren<CompanionCommandSource2D>(true));
                    // 物理夹具不需要灯光；先撤销URP登记，避免跨预览场景迁移灯光触发管理器断言。
                    foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                        if (component.GetType().Name == "Light2D") component.enabled = false;
                    SceneManager.MoveGameObjectToScene(root, physics);
                }
                brains = list.ToArray();
                Check(encounter != null && brains.Length == 2, "Scene references");
                foreach (var root in physics.GetRootGameObjects())
                {
                    foreach (var h in root.GetComponentsInChildren<HealthComponent>(true)) Awake(h);
                    foreach (var h in root.GetComponentsInChildren<DamageHitbox2D>(true)) Awake(h);
                }
                Awake(encounter.TrackingCut.HitEffects); Awake(encounter.TrackingCut.PoseTransition);
                Awake(encounter.TrackingCut); Awake(encounter.SpatialCut);
                encounter.Actor.Body.BeginAuthority(10000, .5f, null, encounter.Perception);
                encounter.Actor.transform.position = new Vector2(9, 0);
                foreach (var brain in brains)
                {
                    Check(brain.Sensor.Claude == encounter && brain.SceneEffects != null, "AI explicit bindings");
                    Check(brain.Sensor.Initialize(), "Sensor initialized");
                    brain.Owner.position = new Vector2(-7, brain.Combat.Role == PlayerRole.DeepSeek ? 3 : -3);
                }
                var book = encounter.Permissions.Books[0];
                Check(book.Open(new Vector2(-6, 1), PlayerRole.DeepSeek, ClaudePermission.Movement,
                    encounter.Permissions.Config, false, encounter.Permissions.Gates[0],
                    encounter.Permissions.Lives[0], null, encounter.Perception), "Open book");
                Physics2D.SyncTransforms();
                foreach (var brain in brains)
                {
                    brain.Sensor.Refresh(brain.Owner.position, brain.AllyLife.transform.position);
                    Check(brain.Sensor.Target == book.Perception && brain.Sensor.HasMechanicTarget,
                        "Both roles prioritize permission book over boss");
                }
                book.Clear();
                Physics2D.SyncTransforms();
                foreach (var brain in brains)
                {
                    Vector2 center = encounter.Actor.Body.Sphere.bounds.center;
                    brain.Owner.position = center + Vector2.left * (encounter.Actor.Body.Sphere.bounds.extents.x - .2f);
                    Physics2D.SyncTransforms();
                    brain.Sensor.Refresh(brain.Owner.position, brain.AllyLife.transform.position);
                    var extent = (Vector2)brain.Shape.bounds.extents;
                    var offset = (Vector2)brain.Shape.bounds.center - (Vector2)brain.Owner.position;
                    float stay = brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent,
                        offset, Vector2.zero, brain.MotorConfig);
                    var move = CompanionSteering2D.Choose(brain.Sensor, brain.Config, brain.MotorConfig,
                        brain.Owner.position, Vector2.zero, extent, center, Vector2.zero, offset);
                    float escape = brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent,
                        offset, move, brain.MotorConfig);
                    Check(brain.Sensor.HasClaudeWarning && stay > 0 && escape < stay,
                        "Shield landing on either role forces escape despite destination inside shield");
                    encounter.Actor.Body.SetVulnerable(false);
                    Check(brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent,
                        offset, Vector2.zero, brain.MotorConfig) > 0,
                        "Invulnerable shield still has contact risk");
                    encounter.Actor.Body.SetVulnerable(true);
                    brain.Owner.position = new Vector2(-7, brain.Combat.Role == PlayerRole.DeepSeek ? 3 : -3);
                }
                Physics2D.SyncTransforms();
                var velocityField = typeof(ClaudeEncounter2D).GetField("_velocity", BindingFlags.Instance | BindingFlags.NonPublic);
                encounter.ApplyReplicaState(ClaudeEncounterState.Gap);
                var arena = encounter.Playfield.WorldBounds;
                Vector2 sphereOffset = encounter.Actor.Body.Sphere.transform.TransformVector(encounter.Actor.Body.Sphere.offset);
                foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                {
                    Vector2 center = arena.center;
                    if (direction.x != 0) center.x = direction.x > 0 ? arena.xMax - .1f : arena.xMin + .1f;
                    else center.y = direction.y > 0 ? arena.yMax - .1f : arena.yMin + .1f;
                    encounter.Actor.transform.position = center - sphereOffset;
                    velocityField.SetValue(encounter, direction * encounter.Config.MoveSpeed);
                    Physics2D.SyncTransforms();
                    Vector2 expected = DeepSleep.Runtime.Combat.Enemies.QuickAppMotor2D.Wrap(
                        center + direction * encounter.Config.MoveSpeed * .2f, arena);
                    Check(Vector2.Distance(encounter.PredictContactCenter(.2f), expected) < .001f,
                        "Contact prediction wraps at each screen edge, not across center of arena");
                }
                velocityField.SetValue(encounter, Vector2.zero);
                encounter.ApplyReplicaState(ClaudeEncounterState.Idle);
                encounter.Actor.transform.position = new Vector2(9, 0);
                Physics2D.SyncTransforms();
                foreach (var brain in brains)
                {
                    var bossPosition = encounter.Actor.Body.Perception.Position;
                    brain.Sensor.Refresh(bossPosition + Vector2.left * 3, bossPosition);
                    Check(!brain.Sensor.PrioritizeRescueThreat(bossPosition + Vector2.left * 3,
                        bossPosition, 3.5f, .5f), "Boss never requires killing before rescue");
                    Check(brain.Sensor.Danger(bossPosition, Vector2.zero, .5f) > 0,
                        "Boss actual contact remains dangerous, not globally passive");
                }
                var cut = encounter.TrackingCut;
                Check(cut.Begin(true, 42), "Dual cut begin"); cut.Simulate(.8f);
                Check(cut.State == ClaudeTrackingCutState.Locked && cut.LockedSecondsRemaining > .39f,
                    "Use actual lock deadline");
                foreach (var brain in brains)
                {
                    var sensor = brain.Sensor;
                    var extent = (Vector2)brain.Shape.bounds.extents;
                    var offset = (Vector2)brain.Shape.bounds.center - (Vector2)brain.Owner.position;
                    float stay = sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent, offset,
                        Vector2.zero, brain.MotorConfig);
                    var move = CompanionSteering2D.Choose(sensor, brain.Config, brain.MotorConfig,
                        brain.Owner.position, Vector2.zero, extent, brain.Owner.position, Vector2.zero, offset);
                    float evade = sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent, offset,
                        move, brain.MotorConfig);
                    Check(stay > 0 && evade < stay, "Escape locked slash instead of staying in marker");
                    var lane = brain.Combat.Role == PlayerRole.DeepSeek ? cut.Lane : cut.SecondaryLane;
                    Check(CompanionBattleSensor2D.LaneRisk(lane, lane.Center, .5f) == 1, "Lane center dangerous");
                    Check(CompanionBattleSensor2D.LaneRisk(lane, lane.Center + new Vector2(-lane.Direction.y,
                        lane.Direction.x) * 3, .5f) == 0, "Outside short slash safe");
                }
                cut.Cancel();
                Check(encounter.SpatialCut.Begin(false, 99), "Spatial warning begins");
                foreach (var brain in brains)
                {
                    var extent = (Vector2)brain.Shape.bounds.extents;
                    var offset = (Vector2)brain.Shape.bounds.center - (Vector2)brain.Owner.position;
                    float stay = brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero,
                        extent, offset, Vector2.zero, brain.MotorConfig);
                    var move = CompanionSteering2D.Choose(brain.Sensor, brain.Config, brain.MotorConfig,
                        brain.Owner.position, Vector2.zero, extent, brain.Owner.position, Vector2.zero, offset);
                    float evade = brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero,
                        extent, offset, move, brain.MotorConfig);
                    Check(float.IsFinite(evade) && evade <= stay, "Visible screen cuts influence steering");
                }
                encounter.SpatialCut.Cancel();
                Awake(encounter.Energy);
                encounter.Energy.Muzzle.position = new Vector2(4, 0);
                Check(encounter.Energy.Begin(false, new Vector2(-10, 0)), "Energy begin");
                encounter.Energy.Simulate(encounter.Energy.Config.ChargeSeconds);
                Check(encounter.Energy.State == ClaudeEnergyState.Flying, "Energy flying");
                Physics2D.SyncTransforms();
                foreach (var brain in brains)
                {
                    Check(brain.Sensor.CanAttack(encounter.Energy.Perception, brain.Owner.position), "Safe ranged interception");
                    brain.Sensor.Refresh(brain.Owner.position, brain.AllyLife.transform.position);
                    Check(brain.Sensor.Target == encounter.Energy.Perception, "Attack safe energy before boss");
                    Check(!brain.Sensor.CanAttack(encounter.Energy.Perception, encounter.Energy.Position), "Never detonate in own face");
                }
                brains[1].Owner.position = encounter.Energy.Position + Vector2.up;
                Physics2D.SyncTransforms();
                Check(!brains[0].Sensor.CanAttack(encounter.Energy.Perception, brains[0].Owner.position),
                    "Do not detonate beside living ally");
                foreach (var brain in brains)
                {
                    var energy = encounter.Energy;
                    brain.Owner.position = energy.Position + Vector2.up * (energy.Config.CollisionRadius + 1);
                    Physics2D.SyncTransforms();
                    brain.Sensor.Refresh(brain.Owner.position, brain.AllyLife.transform.position);
                    var extent = (Vector2)brain.Shape.bounds.extents;
                    var offset = (Vector2)brain.Shape.bounds.center - (Vector2)brain.Owner.position;
                    float stay = brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent,
                        offset, Vector2.zero, brain.MotorConfig);
                    var move = CompanionSteering2D.Choose(brain.Sensor, brain.Config, brain.MotorConfig,
                        brain.Owner.position, Vector2.zero, extent, energy.Position, Vector2.zero, offset);
                    float evade = brain.Sensor.PredictClaudeRisk(brain.Owner.position, Vector2.zero, extent,
                        offset, move, brain.MotorConfig);
                    Check(brain.Sensor.HasClaudeWarning && stay > 0 && evade < stay,
                        "Both roles escape flying sphere even when destination is toward sphere");
                }
                encounter.Energy.Cancel();
                Awake(encounter.SecondaryEnergy);
                encounter.SecondaryEnergy.Muzzle.position = new Vector2(4, 0);
                Check(encounter.SecondaryEnergy.Begin(true, new Vector2(-10, 0)), "Second sphere begins");
                encounter.SecondaryEnergy.Simulate(encounter.SecondaryEnergy.Config.ChargeSeconds);
                foreach (var brain in brains)
                {
                    var extent = (Vector2)brain.Shape.bounds.extents;
                    Vector2 test = encounter.SecondaryEnergy.Position + Vector2.up * 2;
                    Check(brain.Sensor.HasClaudeWarning && brain.Sensor.PredictClaudeRisk(test, Vector2.zero,
                        extent, Vector2.zero, Vector2.zero, brain.MotorConfig) > 0,
                        "Second sphere independently triggers immediate predictive avoidance");
                }
                encounter.SecondaryEnergy.Cancel();
                encounter.Actor.transform.position = new Vector2(9, -4);
                foreach(var brain in brains) brain.Sensor.Refresh(new Vector2(-8, 3), new Vector2(-8, 3));
                foreach (var brain in brains) Check(!brain.Sensor.HasClaudeWarning, "Cancel clears hazards");
                // 修改的是隔离预览实例的时钟，不更改正式状态资产或玩家存档。
                var cache = typeof(CompanionCommandSource2D).GetMethod("Cache", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var brain in brains)
                {
                    var effects = brain.SceneEffects;
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(DeepSleep.Runtime.Progression.Run.ChapterRunController).GetField("_isInitialized", flags).SetValue(effects.Chapter, true);
                    typeof(DeepSleep.Runtime.Progression.Run.ChapterRunController).GetField("_phase", flags).SetValue(effects.Chapter,
                        DeepSleep.Runtime.Progression.Run.ChapterRunPhase.Combat);
                    typeof(DeepSleep.Runtime.Progression.Run.ChapterRunController).GetField("_remainingCombatSeconds", flags).SetValue(effects.Chapter, 100f);
                    foreach (string name in new[] { "_valid", "_bossClock", "_bossStates" }) effects.GetType().GetField(name, flags).SetValue(effects, true);
                    effects.GetType().GetField("_bossElapsed", flags).SetValue(effects, 3f);
                    effects.GetType().GetField("_bossScheduleDuration", flags).SetValue(effects, 60f);
                    for (int count = 0; count < 4; count++)
                    {
                        var pulses = new DeepSleep.Runtime.Progression.Run.SceneEffectPulse[count];
                        for (int i = 0; i < count; i++) pulses[i] = new DeepSleep.Runtime.Progression.Run.SceneEffectPulse {
                            Effect = DeepSleep.Runtime.Progression.Run.SceneBattleEffect.InvertedMovement,
                            FirstWarningSeconds = 0, WarningSeconds = 2, DurationSeconds = 10 };
                        effects.GetType().GetField("_schedule", flags).SetValue(effects, pulses);
                        Check(effects.InvertsMovement == (count % 2 != 0), "Odd/even reversal parity");
                        Vector2 desired = new Vector2(.6f, -.8f);
                        object[] args = { (uint)7, desired, default(AimIntent), default(CommandButtonState),
                            default(CommandButtonState), default(CommandButtonState), default(PlayerCommand) };
                        cache.Invoke(brain, args);
                        var cmd = (PlayerCommand)args[6];
                        Check(effects.TransformMovement(cmd.Move) == desired, "Actual AI command and motor transforms cancel");
                    }
                    effects.EndBossClock();
                }
                return "Claude companion: " + checks + " checks passed (isolated Editor; no device/natural difficulty test).";
            }
            finally
            {
                EditorSceneManager.CloseScene(physics, true);
                EditorSceneManager.ClosePreviewScene(preview);
                SceneManager.SetActiveScene(original);
            }
        }
    }
}
