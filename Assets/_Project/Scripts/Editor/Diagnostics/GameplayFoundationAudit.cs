using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeepSleep.Editor.Networking;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake;
using DeepSleep.Runtime.Combat.Enemies.Presentation;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 扩展玩法内容前的只读装配审计。只读场景和其显式引用的 Prefab；不补组件、不改数组、不保存资产。
    /// 错误是明确断链；警告是需要声明归属/确认设计意图，不能自动“修复”。
    /// </summary>
    public static class GameplayFoundationAudit
    {
        public readonly struct AuditResult
        {
            internal AuditResult(string report, int errors, int warnings, int checks, bool skipped)
            { Report = report; ErrorCount = errors; WarningCount = warnings; CheckCount = checks; Skipped = skipped; }
            public string Report { get; }
            public int ErrorCount { get; }
            public int WarningCount { get; }
            public int CheckCount { get; }
            public bool Skipped { get; }
            public override string ToString() => Report;
        }

        [MenuItem("DeepSleep/验证/玩法基础装配审计（只读）")]
        private static void RunMenu() => Debug.Log(RunLoaded());

        public static string RunLoaded()
        {
            var report = new StringBuilder();
            int visited = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                report.AppendLine(RunScene(scene));
                visited++;
            }
            if (visited == 0) report.AppendLine("No loaded scenes; nothing audited.");
            return report.ToString();
        }

        public static string RunScenePath(string path) => InspectScenePath(path, true).Report;

        /// <summary>检查将进入构建的已保存场景，错误按计数抛出；警告不阻断，不读取报告文字判断结果。</summary>
        public static AuditResult ThrowIfInvalidScenePath(string path)
        {
            AuditResult result = InspectScenePath(path, false);
            if (result.ErrorCount > 0) throw new InvalidOperationException(result.Report);
            return result;
        }

        private static AuditResult InspectScenePath(string path, bool useLoadedScene)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Use RunLoaded during Play; preview audit requires Edit Mode.");
            Scene existing = SceneManager.GetSceneByPath(path);
            if (useLoadedScene && existing.IsValid() && existing.isLoaded) return InspectScene(existing);
            Scene scene = EditorSceneManager.OpenPreviewScene(path);
            try { return InspectScene(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static string RunScene(Scene scene) => InspectScene(scene).Report;

        private static AuditResult InspectScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("A loaded scene is required.");
            var audit = new Audit(scene);
            audit.Run();
            return audit.Result();
        }

        private sealed class Audit
        {
            private readonly Scene _scene;
            private readonly MonoBehaviour[] _all;
            private readonly StringBuilder _details = new StringBuilder();
            private int _errors, _warnings, _checks;
            private bool _skipped;

            public Audit(Scene scene)
            {
                _scene = scene;
                _all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            }

            public void Run()
            {
                if (!_all.Any(b => b is ChapterRunController || b is FixedSimulationLoop || b is PlayerActor))
                { _skipped = true; return; }
                foreach (var b in _all) Check(b != null, "Scene contains a missing MonoBehaviour script.");
                var actors = All<PlayerActor>();
                Check(actors.Length == 2, "Gameplay requires exactly two PlayerActor components; found " + actors.Length + ".");
                foreach (var actor in actors)
                    Check(actor.TryValidateConfiguration(out string reason), Path(actor) + " actor: " + reason);
                foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    Check(actors.Count(a => a.Definition != null && a.Definition.Role == role) == 1,
                        "Role " + role + " must occur exactly once.");

                var session = Single<CoopSessionController>();
                var loop = Single<FixedSimulationLoop>();
                var gate = Single<NetworkAuthorityGate>();
                var chapter = Single<ChapterRunController>();
                var combatWorld = Single<ChapterCombatWorld2D>();
                var combatGate = Single<RestNodeCombatGate>();
                var levelBindings = Single<LevelSceneBindings>();
                var restNode = Single<RestNodePrototypeController2D>();
                var perception = Single<CombatPerceptionRegistry2D>();
                var obstacles = Single<CompanionObstacleRegistry2D>();
                var world = Single<NetworkWorldSnapshotChannel>();
                var combatFeedback = Single<NetworkCombatFeedbackChannel>();
                var hitFeedback = Single<NetworkPlayerHitFeedbackChannel>();
                if (session == null || loop == null || gate == null || chapter == null) return;

                if (levelBindings != null)
                {
                    Check(levelBindings.TryValidateConfiguration(out string reason), "Level bindings: " + reason);
                    Check(levelBindings.ChapterRun == chapter && levelBindings.RestNode == restNode &&
                        levelBindings.SimulationLoop == loop && levelBindings.Session == session &&
                        levelBindings.AuthorityGate == gate && levelBindings.PerceptionRegistry == perception &&
                        levelBindings.WorldSnapshot == world, "Level bindings do not reference this scene's unique services.");
                    Check(LevelSceneInstaller.TryValidateDerived(levelBindings, out reason), "Level registration: " + reason);
                    Check(Reference<Object>(chapter, "_levelBindings") == levelBindings,
                        "Chapter is not connected to the scene's LevelSceneBindings.");
                    Check(session.LevelBindings == levelBindings,
                        "Session must reference the same explicit LevelSceneBindings as the chapter.");
                    var selection = Reference<Object>(chapter, "_selection");
                    Check(selection != null && Reference<Object>(selection, "_chapterRun") == chapter,
                        "Opening character selection is missing its chapter start-validation gate.");
                    var level = Reference<MetaLevelDefinition>(chapter, "_level");
                    Check(level == levelBindings.Level, "Chapter settlement level differs from registered level.");
                    if (level != null)
                    {
                        Check(level.SceneName == _scene.name, "Level " + level.LevelId + " targets another scene: " + level.SceneName + ".");
                        Check(Reference<Object>(chapter, "_config") == level.ChapterRunConfig,
                            "Chapter config differs from its authoritative level definition.");
                    }
                }

                Check(session.Assignment != null && session.Selection != null && session.LocalInput != null &&
                    session.TransportComponent != null && session.Config != null, "Session core references incomplete.");
                if (session.Config != null)
                {
                    Check(session.Config.ProtocolVersion == NetworkMessageCatalog.ProtocolVersion,
                        "Session protocol version differs from NetworkMessageCatalog; apply NetworkBuildRevision.");
                    Check(session.Config.ContentVersion == NetworkBuildRevision.CurrentContentVersion,
                        "Session content version differs from NetworkBuildRevision.CurrentContentVersion.");
                }
                Check(session.DeepSeek != null && session.Harness != null && session.DeepSeek != session.Harness &&
                    actors.Contains(session.DeepSeek) && actors.Contains(session.Harness), "Session actors do not match this scene's pair.");
                if (session.DeepSeek != null && session.Harness != null)
                    Check(session.DeepSeek.Definition != null && session.DeepSeek.Definition.Role == PlayerRole.DeepSeek &&
                        session.Harness.Definition != null && session.Harness.Definition.Role == PlayerRole.Harness,
                        "Session DS/HS actor slots are swapped or missing identity.");
                Check(gate.Session == session, "Authority gate points to another/missing session.");
                if (obstacles != null) Check(obstacles.Session == session, "Obstacle registry session mismatch.");
                if (world != null)
                {
                    Check(world.TryValidateConfiguration(out string reason), "World snapshot: " + reason);
                    Check(world.Session == session, "World snapshot session mismatch.");
                }
                if (combatFeedback != null)
                {
                    Check(combatFeedback.TryValidateConfiguration(out string reason), "Combat feedback: " + reason);
                    Check(combatFeedback.Session == session, "Combat feedback session mismatch.");
                }
                if (hitFeedback != null)
                {
                    Check(hitFeedback.TryValidateConfiguration(out string reason), "Player hit feedback: " + reason);
                    Check(hitFeedback.Session == session, "Player hit feedback session mismatch.");
                }

                var authority = new HashSet<Object>(gate.AuthorityOnly ?? Array.Empty<Behaviour>());
                CheckArray(gate, "AuthorityOnly"); CheckArray(gate, "AuthorityBodies");
                if (combatWorld != null)
                {
                    Check(chapter.CombatWorld == combatWorld && combatWorld.gameObject == chapter.gameObject,
                        "Chapter combat World is missing, cross-scene, or not on its registered chapter object.");
                    Check(combatWorld.Bindings == levelBindings && combatWorld.Gate == combatGate,
                        "Combat World bindings/action gate differ from this level's unique services.");
                    Check(world != null && combatWorld.Rice == world.Rice && combatWorld.Rice != null &&
                        combatWorld.Rice.gameObject.scene == _scene, "Combat World Rice cleanup must use this scene's replicated pool.");
                    Check(!authority.Contains(combatWorld), "Combat World must remain available for replicated transition cleanup on guests.");
                }
                if (combatGate != null)
                {
                    Check(combatGate.TryValidateConfiguration(out string reason), "Combat action gate: " + reason);
                    Check(!authority.Contains(combatGate), "Combat action gate must remain available on guests.");
                    foreach (string field in new[] { "_laser", "_melee" })
                    {
                        var target = Reference<Component>(combatGate, field);
                        Check(target != null && target.gameObject.scene == _scene, "Combat action gate " + field + " is outside this level.");
                    }
                    var playerGates = CheckArray(combatGate, "_players");
                    Check(playerGates.Count == 2, "Combat action gate must contain exactly two distinct player action gates.");
                    foreach (Object playerGate in playerGates)
                        Check(playerGate is Component component && component.gameObject.scene == _scene,
                            "Combat action gate player entry is outside this level.");
                }
                foreach (var component in gate.AuthorityOnly ?? Array.Empty<Behaviour>())
                {
                    if (component == null) continue;
                    Check(component.gameObject.scene == _scene, "Authority list crosses scene: " + Path(component));
                    Check(!NetworkAuthorityRules.MustRemainEnabled(component),
                        "Client presentation/replication was authority-disabled: " + Path(component));
                }

                var steps = CheckArray(loop, "worldStepComponents");
                foreach (Object step in steps)
                {
                    Check(step is IFixedSimulationStep, "Loop entry lacks IFixedSimulationStep: " + Path(step));
                    Check(step is Component c && c.gameObject.scene == _scene, "Loop step crosses scene or refers to a prefab: " + Path(step));
                }
                var dispatchers = CheckArray(loop, "playerDispatchers");
                CheckArray(loop, "reviveCoordinators");
                foreach (var actor in actors)
                    Check(dispatchers.Contains(actor.CommandDispatcher), Path(actor) + " dispatcher omitted from FixedSimulationLoop.");
                var ownedSteps = new HashSet<Object>(steps);
                foreach (var actor in All<EnemyActor2D>())
                    foreach (Object step in References(actor, "_simulationStepComponents")) ownedSteps.Add(step);

                var brains = All<CompanionCommandSource2D>();
                Check(brains.Length == 2, "Gameplay requires exactly two companion brains; found " + brains.Length + ".");
                foreach (var brain in brains)
                {
                    Check(brain.Config != null && brain.Config.IsValid && brain.Sensor != null && brain.Combat != null &&
                        brain.Combat.IsValid && brain.Owner != null && brain.Body != null && brain.Shape != null && brain.MotorConfig != null &&
                        brain.Health != null && brain.Life != null && brain.AllyLife != null && brain.Revive != null && brain.TeamGuard != null &&
                        brain.NodeGoal != null && brain.SquadAnchor != null, Path(brain) + " explicit AI dependencies incomplete.");
                    Check(authority.Contains(brain), Path(brain) + " AI is absent from authority gate.");
                    var actor = actors.FirstOrDefault(a => a.transform == brain.Owner);
                    Check(actor != null, Path(brain) + " owner is not either scene actor.");
                    if (brain.Sensor != null)
                        Check(brain.Sensor.Registry == perception && brain.Sensor.ObstacleRegistry == obstacles && brain.Sensor.Config == brain.Config,
                            Path(brain) + " sensor registry/config mismatch.");
                    if (brain.Combat != null && actor != null && actor.Definition != null)
                        Check(brain.Combat.Role == actor.Definition.Role, Path(brain) + " combat role differs from owner role.");
                    if (brain.NodeGoal != null)
                    {
                        Check(brain.NodeGoal.TryValidateConfiguration(out string reason), Path(brain) + " node goal: " + reason);
                        Check(brain.NodeGoal.Actor == actor && brain.NodeGoal.Shape == brain.Shape && brain.NodeGoal.Session == session,
                            Path(brain) + " node goal actor/collider/session mismatch.");
                    }
                    if (brain.SquadAnchor != null) Check(brain.SquadAnchor.Session == session, Path(brain) + " squad anchor session mismatch.");
                }
                Check(brains.Contains(session.DeepSeekAi) && brains.Contains(session.HarnessAi) && session.DeepSeekAi != session.HarnessAi,
                    "Session AI references do not match this scene's two brains.");
                if (session.DeepSeekAi != null && session.HarnessAi != null && session.DeepSeek != null && session.Harness != null)
                    Check(session.DeepSeekAi.Owner == session.DeepSeek.transform && session.HarnessAi.Owner == session.Harness.transform,
                        "Session AI slots point to the wrong actor owner.");

                var registeredPools = levelBindings != null
                    ? new HashSet<Object>(levelBindings.Enemies.Where(e => e != null).Select(e => (Object)e.Pool)) : new HashSet<Object>();
                var directors = levelBindings != null
                    ? new HashSet<Object>(levelBindings.Enemies.Where(e => e != null).Select(e => (Object)e.Director)) : new HashSet<Object>();
                var cleanupBullets = levelBindings != null
                    ? new HashSet<Object>(levelBindings.Enemies.Where(e => e != null).SelectMany(e => e.ProjectilePools).Cast<Object>()) : new HashSet<Object>();
                Check(registeredPools.SetEquals(All<EnemyActorPool2D>().Cast<Object>()), "Level binding does not cover exactly this scene's enemy pools.");
                Check(directors.SetEquals(All<EnemySpawnDirector2D>().Cast<Object>()), "Level binding does not cover exactly this scene's spawn directors.");
                Check(cleanupBullets.SetEquals(All<EnemyProjectilePool2D>().Cast<Object>()), "Combat world binding does not cover exactly this scene's projectile pools.");
                var objectives = CheckArray(chapter, "_additionalObjectiveComponents");
                var chapterConfig = Reference<ChapterRunConfig>(chapter, "_config");
                Check(chapterConfig != null, "Chapter config missing.");
                if (chapterConfig != null) Check(chapterConfig.TryValidate(out string reason), "Chapter config: " + reason);
                foreach (string field in new[] { "_restNode", "_combatWorld", "_upgradeController", "_selection", "_deepSeekLife", "_harnessLife", "_hud", "_level" })
                    Check(Reference<Object>(chapter, field) != null, "Chapter missing " + field + ".");
                Check(Reference<Object>(chapter, "_session") == session, "Chapter session mismatch.");
                var rewards = All<EnemyTokenRewardController>();
                Check(rewards.Length == 1, "Expected one enemy Token reward controller; found " + rewards.Length + ".");
                var rewardPools = new HashSet<Object>();
                foreach (var reward in rewards) foreach (Object pool in CheckArray(reward, "_enemyPools")) rewardPools.Add(pool);
                var replicatedPools = world != null ? new HashSet<Object>(AllReferences<EnemyActorPool2D>(world)) : new HashSet<Object>();
                var replicatedBullets = world != null ? new HashSet<Object>(AllReferences<EnemyProjectilePool2D>(world)) : new HashSet<Object>();

                foreach (var pool in All<EnemyActorPool2D>())
                {
                    Check(pool.TryValidateConfiguration(out string reason), Path(pool) + " pool: " + reason);
                    Check(steps.Contains(pool), Path(pool) + " omitted from fixed simulation loop.");
                    Check(registeredPools.Contains(pool), Path(pool) + " omitted from the shared chapter/world enemy registration.");
                    Check(rewardPools.Contains(pool), Path(pool) + " omitted from Token reward subscriptions.");
                    Check(replicatedPools.Contains(pool), Path(pool) + " omitted from world replication pool references.");
                    Check(authority.Contains(pool), Path(pool) + " pool omitted from authority gate.");
                    Check(Reference<Object>(pool, "_perceptionRegistry") == perception, Path(pool) + " perception registry missing/mismatched.");
                    var prefab = Reference<EnemyActor2D>(pool, "_enemyPrefab");
                    if (prefab != null)
                    {
                        Check(prefab.TryValidateConfiguration(out reason), Path(prefab) + " enemy prefab: " + reason);
                        CheckPerception(prefab, true);
                        var flash = CheckHitFlash(prefab, prefab.Health, false);
                        var snakeVisual = prefab.GetComponent<DataCrawlerSnakeVisual2D>();
                        if (flash != null && snakeVisual != null && flash.Sources != null && flash.Sources.Length > 0)
                            Check(flash.Sources[0] == Reference<SpriteRenderer>(snakeVisual, "_renderer"),
                                Path(prefab) + " hit flash source 0 is not the declared snake body renderer.");
                        CheckPrefabVisuals(prefab, world);
                        var localSteps = CheckArray(prefab, "_simulationStepComponents");
                        foreach (var step in prefab.GetComponents<MonoBehaviour>().OfType<IFixedSimulationStep>())
                            Check(localSteps.Contains((Object)step), Path(prefab) + " local simulation step omitted: " + step.GetType().Name);
                    }
                    var presenters = All<EnemyDespawnEffectPresenter2D>().Where(p => Reference<Object>(p, "_enemyPool") == pool).ToArray();
                    Warn(presenters.Length > 0, Path(pool) + " has no despawn feedback presenter; confirm whether intentional.");
                    foreach (var presenter in presenters)
                    {
                        Check(presenter.TryValidateConfiguration(out reason), Path(presenter) + " feedback: " + reason);
                        foreach (var effect in AllReferences<OneShotSpriteEffectPool2D>(presenter))
                            Check(All<NetworkEffectEventChannel>().Any(c => c.Pool == effect), Path(effect) + " enemy feedback pool has no network effect channel.");
                    }
                }
                foreach (var pool in All<EnemyProjectilePool2D>())
                {
                    Check(pool.TryValidateConfiguration(out string reason), Path(pool) + " projectile pool: " + reason);
                    Check(steps.Contains(pool) && authority.Contains(pool), Path(pool) + " projectile pool missing loop/authority registration.");
                    Check(replicatedBullets.Contains(pool), Path(pool) + " omitted from projectile replication references.");
                    Check(cleanupBullets.Contains(pool), Path(pool) + " omitted from combat world projectile cleanup.");
                    Check(Reference<Object>(pool, "_perceptionRegistry") == perception, Path(pool) + " projectile perception registry mismatch.");
                    var prefab = Reference<EnemyProjectile2D>(pool, "_projectilePrefab");
                    if (prefab != null) { CheckPerception(prefab, false); CheckPrefabVisuals(prefab, world); }
                }
                foreach (var director in All<EnemySpawnDirector2D>())
                {
                    Check(director.TryValidateConfiguration(out string reason), Path(director) + " spawn: " + reason);
                    Check(directors.Contains(director) && steps.Contains(director) && authority.Contains(director),
                        Path(director) + " spawn director missing chapter/loop/authority registration.");
                    var autoStart = new SerializedObject(director).FindProperty("_autoStart");
                    Check(autoStart != null && !autoStart.boolValue,
                        Path(director) + " auto-start must be disabled; the chapter owns combat activation.");
                }

                var lifecycle = combatWorld != null ? CheckArray(combatWorld, "ParticipantComponents") : new HashSet<Object>();
                foreach (Object participant in lifecycle)
                    Check(participant is IChapterCombatLifecycle && participant is Component component && component.gameObject.scene == _scene,
                        "Combat lifecycle participant is outside this level or lacks the contract: " + Path(participant));
                foreach (var participant in _all.Where(b => b is IChapterCombatLifecycle))
                    Check(lifecycle.Contains(participant), Path(participant) + " lifecycle module exists but is not explicitly registered.");
                foreach (Object objective in objectives) Check(objective is IChapterCombatObjective, "Chapter objective has no objective interface: " + Path(objective));
                foreach (var objective in _all.Where(b => b is IChapterCombatObjective))
                {
                    Check(objectives.Contains(objective), Path(objective) + " objective exists but chapter does not reference it.");
                    if (chapterConfig != null)
                        Warn(Enumerable.Range(1, chapterConfig.CombatSegmentCount).Any(((IChapterCombatObjective)objective).IsRequiredForSegment),
                            Path(objective) + " objective is not required by any configured segment.");
                }
                foreach (var driver in All<DoubaoChapterEncounterDriver2D>())
                {
                    Check(lifecycle.Contains(driver), Path(driver) + " Doubao lifecycle is missing from the combat World.");
                    foreach (Object director in CheckArray(driver, "_spawnDirectors"))
                        Check(directors.Contains(director), Path(driver) + " encounter cap refers to an unregistered spawn director.");
                    Check(Reference<Object>(driver, "_chapterRun") == chapter && Reference<Object>(driver, "_session") == session,
                        Path(driver) + " encounter driver chapter/session mismatch.");
                    Check(steps.Contains(driver), Path(driver) + " encounter driver omitted from simulation loop.");
                    var encounter = Reference<DoubaoWordWallEncounter2D>(driver, "_encounter");
                    Check(encounter != null, Path(driver) + " encounter reference missing.");
                    if (encounter != null)
                    {
                        ownedSteps.Add(encounter);
                        Check(!steps.Contains(encounter), Path(encounter) + " encounter is simulated both directly and via chapter driver.");
                        Check(encounter.TryValidateConfiguration(out string reason), Path(encounter) + " encounter: " + reason);
                        Check(Reference<Object>(encounter, "_obstacleRegistry") == obstacles, Path(encounter) + " obstacle registry mismatch.");
                        Check(All<DoubaoEncounterNetworkChannel>().Any(c => Reference<Object>(c, "_encounter") == encounter),
                            Path(encounter) + " has no encounter replication channel.");
                    }
                }
                foreach (var step in _all.Where(b => b is IFixedSimulationStep))
                    Warn(ownedSteps.Contains(step), Path(step) + " IFixedSimulationStep has no known loop/actor/encounter owner; confirm explicit delegation.");
                foreach (var group in All<NetworkEffectEventChannel>().GroupBy(c => c.EffectId))
                    Check(group.Key != 0 && group.Count() == 1, "Network effect ID is zero or duplicated: " + group.Key + ".");
                foreach (var channel in All<NetworkEffectEventChannel>())
                    Check(channel.Session == session && channel.Pool != null, Path(channel) + " effect channel session/pool incomplete.");
                if (world != null && world.ViewPrefab != null)
                {
                    var flash = CheckHitFlash(world.ViewPrefab, null, true);
                    if (flash != null)
                    {
                        Check(Reference<SpriteHitFlash2D>(world.ViewPrefab, "HitFlash") == flash,
                            "Network entity view does not reference its hit flash.");
                        Check(flash.Sources != null && world.ViewPrefab.Layers != null && flash.Sources.SequenceEqual(world.ViewPrefab.Layers),
                            "Network hit flash Sources must exactly match the ordered replica Layers; layer 0 is the body.");
                    }
                }
                foreach (var boss in All<DoubaoBoss2D>())
                {
                    var flash = CheckHitFlash(boss, boss, false);
                    if (flash != null && flash.Sources != null && flash.Sources.Length > 0)
                        Check(flash.Sources[0] == Reference<SpriteRenderer>(boss, "_renderer"), Path(boss) + " hit flash source 0 is not the declared boss body.");
                    foreach (var channel in All<DoubaoEncounterNetworkChannel>().Where(c => Reference<Object>(c, "_boss") == boss))
                        Check(Reference<SpriteHitFlash2D>(channel, "_bossHitFlash") == flash && flash != null,
                            Path(channel) + " boss hit-flash reference missing or mismatched.");
                }
            }

            private SpriteHitFlash2D CheckHitFlash(Component subject, MonoBehaviour damageSource, bool replica)
            {
                var flash = subject.GetComponent<SpriteHitFlash2D>();
                Check(flash != null, Path(subject) + " lacks explicit SpriteHitFlash2D.");
                if (flash == null) return null;
                Check(flash.TryValidateConfiguration(out string reason), Path(subject) + " hit flash: " + reason);
                Check(flash.DamageSource == damageSource && flash.ReplicaOnly == replica,
                    Path(subject) + " hit flash damage source/replica mode mismatch.");
                var baseRenderers = subject.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r != flash.Overlay).ToArray();
                Check(flash.Sources != null && new HashSet<SpriteRenderer>(flash.Sources).SetEquals(baseRenderers),
                    Path(subject) + " hit flash Sources must explicitly cover all base renderers and exclude Overlay.");
                Check(flash.Overlay != null && flash.Overlay.transform.IsChildOf(subject.transform),
                    Path(subject) + " hit flash overlay is outside the subject hierarchy.");
                return flash;
            }

            private void CheckPerception(Component prefab, bool enemy)
            {
                var body = prefab.GetComponent<CombatPerceptionBody2D>();
                Check(body != null, Path(prefab) + " prefab lacks CombatPerceptionBody2D.");
                if (body == null) return;
                Check(body.Shape != null && body.Body != null && (enemy ? body.Enemy == prefab && body.Hitbox != null : body.Projectile == prefab),
                    Path(prefab) + " perception body lacks/mismatches geometry or actor/projectile references.");
            }

            private void CheckPrefabVisuals(Component prefab, NetworkWorldSnapshotChannel channel)
            {
                if (channel == null || channel.Catalog == null || channel.ViewPrefab == null)
                { Check(false, "World sprite catalog/view prefab missing."); return; }
                var flash = prefab.GetComponent<SpriteHitFlash2D>();
                var renderers = flash != null ? flash.Sources ?? Array.Empty<SpriteRenderer>() : prefab.GetComponentsInChildren<SpriteRenderer>(true);
                Check(channel.ViewPrefab.Layers != null && renderers.Length <= channel.ViewPrefab.Layers.Length,
                    Path(prefab) + " sprite renderer count exceeds replica layer capacity.");
                var entries = channel.Catalog.Entries ?? Array.Empty<NetworkSpriteCatalog.Entry>();
                foreach (var renderer in renderers)
                    if (renderer != null && renderer.sprite != null) Check(entries.Any(e => e.Sprite == renderer.sprite && e.Id != 0),
                        Path(prefab) + " initial sprite missing from network catalog: " + renderer.sprite.name + ".");
                // 动画/运行时替换的所有 Sprite 还需目录专项审计，不能以初始帧覆盖代替。
            }

            private T[] All<T>() where T : MonoBehaviour => _all.OfType<T>().ToArray();
            private T Single<T>() where T : MonoBehaviour
            {
                var values = All<T>();
                Check(values.Length == 1, "Expected exactly one " + typeof(T).Name + "; found " + values.Length + ".");
                return values.Length == 1 ? values[0] : null;
            }
            private HashSet<Object> CheckArray(Object owner, string name)
            {
                var property = new SerializedObject(owner).FindProperty(name);
                Check(property != null && property.isArray, Path(owner) + "." + name + " array field is missing; audit schema must stay explicit.");
                Object[] values = References(owner, name);
                var result = new HashSet<Object>();
                foreach (Object value in values)
                {
                    Check(value != null, Path(owner) + "." + name + " contains a null entry.");
                    if (value != null) Check(result.Add(value), Path(owner) + "." + name + " duplicates " + Path(value) + ".");
                }
                return result;
            }
            private void Check(bool condition, string message)
            { _checks++; if (!condition) { _errors++; _details.AppendLine("ERROR " + message); } }
            private void Warn(bool condition, string message)
            { _checks++; if (!condition) { _warnings++; _details.AppendLine("WARN " + message); } }
            public string Report() => _skipped ? _scene.path + ": SKIPPED (not a gameplay scene)." :
                _scene.path + ": " + _checks + " checks, " + _errors + " errors, " + _warnings + " warnings.\n" + _details +
                "Read-only assembly audit; does not establish gameplay, network or device correctness.";
            public AuditResult Result() => new AuditResult(Report(), _errors, _warnings, _checks, _skipped);
        }

        private static T Reference<T>(Object owner, string field) where T : Object =>
            owner != null ? new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T : null;

        private static Object[] References(Object owner, string field)
        {
            var property = owner != null ? new SerializedObject(owner).FindProperty(field) : null;
            if (property == null || !property.isArray) return Array.Empty<Object>();
            var values = new Object[property.arraySize];
            for (int i = 0; i < values.Length; i++) values[i] = property.GetArrayElementAtIndex(i).objectReferenceValue;
            return values;
        }

        private static IEnumerable<T> AllReferences<T>(Object owner) where T : Object
        {
            var iterator = new SerializedObject(owner).GetIterator();
            while (iterator.Next(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue is T value)
                    yield return value;
        }

        private static string Path(Object value)
        {
            if (value == null) return "<null>";
            if (value is Component component)
            {
                string path = component.name;
                for (Transform parent = component.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                return path + " (" + component.GetType().Name + ")";
            }
            string assetPath = AssetDatabase.GetAssetPath(value);
            return string.IsNullOrEmpty(assetPath) ? value.name : assetPath;
        }
    }
}
