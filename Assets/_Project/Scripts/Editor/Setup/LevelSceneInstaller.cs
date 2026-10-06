using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeepSleep.Editor.Networking;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式操作所选关卡；清单是来源，消费者数组只是可重新生成的接线。</summary>
    public static class LevelSceneInstaller
    {
        private sealed class Write
        {
            public Object Owner;
            public string Field;
            public Object[] Values;
            public bool IsArray;
            public bool? BooleanValue;
            public bool IsChanged => BooleanValue.HasValue
                ? ReadBoolean(Owner, Field) != BooleanValue.Value
                : !Read(Owner, Field, IsArray).SequenceEqual(Values);
            public string Label => Owner.name + "." + Field;
            public string TargetLabel => BooleanValue.HasValue ? BooleanValue.Value.ToString() :
                "[" + string.Join(", ", Values.Select(v => v == null ? "null" : v.name)) + "]";
        }

        [MenuItem("DeepSleep/关卡/预览所选绑定接线（只读）")]
        private static void PreviewSelected() => Debug.Log(Preview(SelectedBindings()));

        [MenuItem("DeepSleep/关卡/应用所选绑定接线")]
        private static void ApplySelected()
        {
            LevelSceneBindings bindings = SelectedBindings();
            Debug.Log("[Level] Applied " + Apply(bindings) + " reference fields in " + bindings.gameObject.scene.path + ". Save the scene explicitly.");
        }

        [MenuItem("DeepSleep/关卡/迁移当前关卡战斗生命周期")]
        private static void InstallLifecycleSelected() => Debug.Log(InstallLifecycle(SceneManager.GetActiveScene()));

        [MenuItem("DeepSleep/关卡/导入当前场景到所选关卡定义")]
        private static void MigrateSelected()
        {
            if (Selection.activeObject is not MetaLevelDefinition level)
                throw new InvalidOperationException("Select the exact MetaLevelDefinition asset, then make its gameplay scene active.");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(level))?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("Selected level must be a saved asset.");
            string stem = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(level));
            Debug.Log(Migrate(SceneManager.GetActiveScene(), level,
                folder + "/" + stem + "_Run.asset", folder + "/" + stem + "_Content.asset"));
        }

        /// <summary>显式扫描所选旧场景并验证每个 Director/Pool/Prefab 归属后一次性导入；不调参、不自动保存场景。</summary>
        public static string Migrate(Scene scene, MetaLevelDefinition level, string runConfigPath, string manifestPath)
        {
            RequireEditScene(scene);
            if (level == null || !level.TryValidate(out string reason))
                throw new InvalidOperationException("A valid explicit level definition is required.");
            if (level.SceneName != scene.name)
                throw new InvalidOperationException("Level " + level.LevelId + " targets " + level.SceneName + ", not " + scene.name + ".");
            var all = Components(scene);
            var existing = all.OfType<LevelSceneBindings>().ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Multiple LevelSceneBindings in selected scene.");
            if (existing.Length == 1)
            {
                if (existing[0].Level != level) throw new InvalidOperationException("Existing binding belongs to another level; no references changed.");
                return "[Level] Already imported. " + InstallLifecycle(scene) + " No definitions or gameplay configuration values rewritten.";
            }

            var chapter = One<ChapterRunController>(all);
            var rest = One<RestNodePrototypeController2D>(all);
            var loop = One<FixedSimulationLoop>(all);
            var session = One<CoopSessionController>(all);
            var gate = One<NetworkAuthorityGate>(all);
            var perception = One<CombatPerceptionRegistry2D>(all);
            var rewards = One<EnemyTokenRewardController>(all);
            var world = One<NetworkWorldSnapshotChannel>(all);
            if (gate.Session != session || world.Session != session)
                throw new InvalidOperationException("Existing authority/world services reference another session; no import performed.");
            ValidateCombatGate(One<RestNodeCombatGate>(all), scene);
            if (world.Rice == null || world.Rice.gameObject.scene != scene)
                throw new InvalidOperationException("Existing world replication needs this scene's explicit Rice pool before import.");
            var oldCombatWorlds = all.OfType<ChapterCombatWorld2D>().ToArray();
            if (oldCombatWorlds.Length > 1 || oldCombatWorlds.Length == 1 && oldCombatWorlds[0].gameObject != chapter.gameObject)
                throw new InvalidOperationException("Existing lifecycle World does not belong to the unique chapter object.");
            var lifecycleParticipants = oldCombatWorlds.Length == 0
                ? References(chapter, "_additionalObjectiveComponents").OfType<MonoBehaviour>()
                    .Where(v => v is IChapterCombatLifecycle).ToArray()
                : oldCombatWorlds[0].ParticipantComponents;
            ValidateParticipants(all, chapter, lifecycleParticipants, scene);
            Read(chapter, "_combatWorld", false);
            var selection = Reference<OpeningCharacterSelectionController>(chapter, "_selection");
            if (selection == null || selection.gameObject.scene != scene)
                throw new InvalidOperationException("Existing chapter needs a selection controller in this scene.");
            Read(selection, "_chapterRun", false);
            Read(chapter, "_levelBindings", false);
            // 混合列表中的空引用无法安全判断归属，必须在创建配置资产之前报告。
            MergeManaged(References(loop, "worldStepComponents"), Array.Empty<Object>());
            MergeManaged(References(gate, "AuthorityOnly"), Array.Empty<Object>());
            var sourceConfig = Reference<ChapterRunConfig>(chapter, "_config");
            if (sourceConfig == null || !sourceConfig.TryValidate(out reason)) throw new InvalidOperationException("Existing chapter config is invalid.");
            // 只有用户显式调用旧场景导入才扫描；正式关卡始终以 Bindings 为唯一来源。
            var directors = all.OfType<EnemySpawnDirector2D>()
                .OrderBy(d => AssetDatabase.GetAssetPath(d.Channel), StringComparer.Ordinal).ToArray();
            if (directors.Length == 0) throw new InvalidOperationException("Selected legacy scene has no enemy directors.");
            var pools = directors.Select(d => Reference<EnemyActorPool2D>(d, "_pool")).ToArray();
            if (pools.Any(p => p == null || p.gameObject.scene != scene) || pools.Distinct().Count() != pools.Length)
                throw new InvalidOperationException("Phase A requires exactly one pool per channel.");
            if (!new HashSet<EnemyActorPool2D>(pools).SetEquals(all.OfType<EnemyActorPool2D>()))
                throw new InvalidOperationException("Selected scene contains an enemy pool not owned by exactly one director. Resolve before import.");
            var roots = new GameObject[directors.Length];
            var prefabs = new GameObject[directors.Length];
            var actors = new EnemyActor2D[directors.Length];
            var bullets = new EnemyProjectilePool2D[directors.Length][];
            var ids = new string[directors.Length];
            var channels = new HashSet<EnemySpawnChannelDefinition>();
            for (int i = 0; i < directors.Length; i++)
            {
                var director = directors[i];
                if (director.Channel == null || !channels.Add(director.Channel)) throw new InvalidOperationException("Missing/repeated explicit spawn channel.");
                roots[i] = PrefabUtility.GetOutermostPrefabInstanceRoot(director.gameObject);
                if (roots[i] == null) throw new InvalidOperationException(director.name + " needs an explicit runtime prefab instance before import.");
                prefabs[i] = PrefabUtility.GetCorrespondingObjectFromOriginalSource(roots[i]);
                actors[i] = Reference<EnemyActor2D>(pools[i], "_enemyPrefab");
                if (prefabs[i] == null || actors[i] == null || !pools[i].transform.IsChildOf(roots[i].transform))
                    throw new InvalidOperationException(director.name + " runtime prefab/pool/actor relationship is invalid.");
                bullets[i] = roots[i].GetComponentsInChildren<EnemyProjectilePool2D>(true);
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(director.Channel));
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Spawn channel must be a saved asset.");
                ids[i] = "enemy-" + guid;
            }
            if (roots.Distinct().Count() != roots.Length)
                throw new InvalidOperationException("Each channel must have a separate runtime module root.");
            for (int segment = 1; segment <= sourceConfig.CombatSegmentCount; segment++)
                foreach (var rule in sourceConfig.GetSegment(segment).SpawnRules)
                    if (rule.Enabled && !channels.Contains(rule.Channel))
                        throw new InvalidOperationException("Existing segment enables an unregistered channel; resolve before import.");
            var allBullets = bullets.SelectMany(b => b).Distinct().ToArray();
            if (allBullets.Length != 1 || !new HashSet<EnemyProjectilePool2D>(allBullets).SetEquals(all.OfType<EnemyProjectilePool2D>()))
                throw new InvalidOperationException("Phase A world replication requires exactly one explicitly owned enemy projectile pool.");
            if (level.ChapterRunConfig != null || level.ContentManifest != null)
                throw new InvalidOperationException("Level already has gameplay definitions but no scene binding. Bind them explicitly; migration will not overwrite them.");
            ValidateNewAssetPath(runConfigPath);
            ValidateNewAssetPath(manifestPath);
            if (runConfigPath == manifestPath) throw new InvalidOperationException("Run and content paths must differ.");

            var config = Object.Instantiate(sourceConfig);
            config.name = Path.GetFileNameWithoutExtension(runConfigPath);
            AssetDatabase.CreateAsset(config, runConfigPath);
            var manifest = ScriptableObject.CreateInstance<LevelContentManifest>();
            manifest.name = Path.GetFileNameWithoutExtension(manifestPath);
            var manifestData = new SerializedObject(manifest);
            var entries = manifestData.FindProperty("_enemies");
            entries.arraySize = directors.Length;
            for (int i = 0; i < directors.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_entryId").stringValue = ids[i];
                entry.FindPropertyRelative("_channel").objectReferenceValue = directors[i].Channel;
                entry.FindPropertyRelative("_runtimePrefab").objectReferenceValue = prefabs[i];
                entry.FindPropertyRelative("_enemyPrefab").objectReferenceValue = actors[i];
            }
            manifestData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(manifest, manifestPath);
            SetReference(level, "_chapterRunConfig", config);
            SetReference(level, "_contentManifest", manifest);
            var bindings = Undo.AddComponent<LevelSceneBindings>(chapter.gameObject);
            var data = new SerializedObject(bindings);
            Assign(data, "_level", level); Assign(data, "_chapterRun", chapter); Assign(data, "_restNode", rest);
            Assign(data, "_simulationLoop", loop); Assign(data, "_session", session); Assign(data, "_authorityGate", gate);
            Assign(data, "_perceptionRegistry", perception); Assign(data, "_tokenRewards", rewards); Assign(data, "_worldSnapshot", world);
            var bindingsEntries = data.FindProperty("_enemies");
            bindingsEntries.arraySize = directors.Length;
            for (int i = 0; i < directors.Length; i++)
            {
                var entry = bindingsEntries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_entryId").stringValue = ids[i];
                entry.FindPropertyRelative("_root").objectReferenceValue = roots[i].transform;
                entry.FindPropertyRelative("_director").objectReferenceValue = directors[i];
                entry.FindPropertyRelative("_pool").objectReferenceValue = pools[i];
                var projectileRefs = entry.FindPropertyRelative("_projectilePools");
                projectileRefs.arraySize = bullets[i].Length;
                for (int j = 0; j < bullets[i].Length; j++) projectileRefs.GetArrayElementAtIndex(j).objectReferenceValue = bullets[i][j];
            }
            data.ApplyModifiedProperties();
            string lifecycleReport = InstallLifecycle(scene);
            AssetDatabase.SaveAssetIfDirty(config);
            AssetDatabase.SaveAssetIfDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(level);
            EditorSceneManager.MarkSceneDirty(scene);
            return "[Level] Imported " + level.LevelId + " into " + scene.path + "; " + directors.Length +
                " enemy entries. " + lifecycleReport + " Independent run config copied without changing values. Save the scene explicitly.";
        }

        /// <summary>
        /// 从已经显式登记的章节目标一次性导入生命周期参与者；只在主动调用时创建 World。
        /// 后续参与者清单由 World 显式保存，不通过层级扫描猜测或补齐；不保存场景。
        /// </summary>
        public static string InstallLifecycle(Scene scene)
        {
            RequireEditScene(scene);
            var all = Components(scene);
            var bindings = One<LevelSceneBindings>(all);
            var chapter = One<ChapterRunController>(all);
            if (bindings.ChapterRun != chapter)
                throw new InvalidOperationException("Level binding and scene chapter differ; no lifecycle migration performed.");
            // 在创建组件前验证 A 批所有内容关系，不能以迁移为名修补不明确的登记。
            BuildPlan(bindings, false);
            var gate = One<RestNodeCombatGate>(all);
            ValidateCombatGate(gate, scene);
            if (bindings.WorldSnapshot.Rice == null || bindings.WorldSnapshot.Rice.gameObject.scene != scene)
                throw new InvalidOperationException("Lifecycle requires this scene's explicitly replicated Rice projectile pool.");
            var worlds = all.OfType<ChapterCombatWorld2D>().ToArray();
            if (worlds.Length > 1 || worlds.Length == 1 && worlds[0].gameObject != chapter.gameObject)
                throw new InvalidOperationException("Exactly one combat World must be installed on the registered chapter object.");
            var participants = worlds.Length == 0
                ? References(chapter, "_additionalObjectiveComponents").OfType<MonoBehaviour>()
                    .Where(v => v is IChapterCombatLifecycle).ToArray()
                : worlds[0].ParticipantComponents;
            ValidateParticipants(all, chapter, participants, scene);
            Read(chapter, "_combatWorld", false);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Migrate chapter combat lifecycle");
            try
            {
                bool created = worlds.Length == 0;
                var combatWorld = created ? Undo.AddComponent<ChapterCombatWorld2D>(chapter.gameObject) : worlds[0];
                if (created)
                {
                    var data = new SerializedObject(combatWorld);
                    var field = data.FindProperty("ParticipantComponents");
                    field.arraySize = participants.Length;
                    for (int index = 0; index < participants.Length; index++)
                        field.GetArrayElementAtIndex(index).objectReferenceValue = participants[index];
                    data.ApplyModifiedProperties();
                }
                int changes = Apply(bindings);
                if (created) EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                return "[Level lifecycle] " + bindings.Level.LevelId + ": " + (created ? "created" : "reused") +
                    " explicit combat World, " + participants.Length + " participants, " + changes +
                    " changed derived fields. Director auto-start disabled; chapter is the sole combat start owner. Save the scene explicitly.";
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        /// <summary>只读预览，不创建或修复资产、对象和字段。</summary>
        public static string Preview(LevelSceneBindings bindings)
        {
            var plan = BuildPlan(bindings);
            var changed = plan.Where(w => w.IsChanged).ToArray();
            return "Level " + bindings.Level.LevelId + ": " + changed.Length + " changed reference fields\n" +
                string.Join("\n", changed.Select(w => w.Label + " -> " + w.TargetLabel));
        }

        /// <summary>应用已验证的派生引用；不改玩法数值、不创建内容实例、不自动保存场景。</summary>
        public static int Apply(LevelSceneBindings bindings, bool recordUndo = true)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            RequireEditScene(bindings.gameObject.scene);
            var changed = BuildPlan(bindings).Where(w => w.IsChanged).ToArray();
            foreach (var write in changed)
            {
                if (recordUndo) Undo.RecordObject(write.Owner, "Apply level registration");
                var data = new SerializedObject(write.Owner);
                var field = data.FindProperty(write.Field);
                if (write.BooleanValue.HasValue) field.boolValue = write.BooleanValue.Value;
                else if (write.IsArray)
                {
                    field.arraySize = write.Values.Length;
                    for (int i = 0; i < write.Values.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = write.Values[i];
                }
                else field.objectReferenceValue = write.Values[0];
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(write.Owner);
                if (PrefabUtility.IsPartOfPrefabInstance(write.Owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(write.Owner);
            }
            if (changed.Length > 0) EditorSceneManager.MarkSceneDirty(bindings.gameObject.scene);
            return changed.Length;
        }

        /// <summary>历史入口转调：只按已登记清单派生，不重新扫描内容或保存场景。</summary>
        public static bool TryApplyRegisteredScene(Scene scene, out string report)
        {
            RequireEditScene(scene);
            var bindings = Components(scene).OfType<LevelSceneBindings>().ToArray();
            if (bindings.Length > 1) throw new InvalidOperationException("Multiple LevelSceneBindings in selected scene.");
            report = string.Empty;
            if (bindings.Length == 0) return false;
            report = "[Level] Applied " + Apply(bindings[0]) + " derived fields from " +
                bindings[0].Level.LevelId + ". No legacy initialization performed. Save the scene explicitly.";
            return true;
        }

        /// <summary>拒绝未声明内容和陈旧派生清单；不改变任何字段。</summary>
        public static bool TryValidateDerived(LevelSceneBindings bindings, out string reason)
        {
            try
            {
                var changed = BuildPlan(bindings).Where(w => w.IsChanged).Select(w => w.Label).ToArray();
                reason = changed.Length == 0 ? string.Empty : "Derived level references differ; preview/apply explicit registration: " + string.Join(", ", changed);
                return changed.Length == 0;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private static List<Write> BuildPlan(LevelSceneBindings bindings, bool includeLifecycle = true)
        {
            if (bindings == null) throw new InvalidOperationException("Missing LevelSceneBindings.");
            if (!bindings.TryValidateConfiguration(out string reason, false)) throw new InvalidOperationException(reason);
            var entries = bindings.Enemies.ToArray();
            foreach (var entry in entries)
            {
                bindings.Level.ContentManifest.TryGetEnemy(entry.EntryId, out var content);
                if (Reference<EnemyActorPool2D>(entry.Director, "_pool") != entry.Pool ||
                    Reference<EnemyActor2D>(entry.Pool, "_enemyPrefab") != content.EnemyPrefab)
                    throw new InvalidOperationException(bindings.Level.LevelId + "/" + entry.EntryId + ": Director pool or actor prefab differs from declared content.");
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(entry.Root.gameObject);
                if (source != content.RuntimePrefab)
                    throw new InvalidOperationException(bindings.Level.LevelId + "/" + entry.EntryId + ": runtime module is not an instance of its declared prefab.");
            }
            var pools = entries.Select(e => e.Pool).ToArray();
            var directors = entries.Select(e => e.Director).ToArray();
            var bullets = entries.SelectMany(e => e.ProjectilePools).Distinct().ToArray();
            if (bullets.Length != 1) throw new InvalidOperationException("Phase A requires exactly one enemy projectile pool; multiple pools have no supported world replication yet.");
            var all = Components(bindings.gameObject.scene);
            var kimi = all.OfType<KimiChapterEncounterDriver2D>().ToArray();
            foreach (var driver in kimi)
                if (driver.Chapter != bindings.ChapterRun || !driver.TryValidateConfiguration(out reason))
                    throw new InvalidOperationException("Invalid explicit Kimi chapter ownership: " + reason);
            var encounterPools = kimi.Select(k => k.Encounter.Ultimate.ReinforcementPool).ToArray();
            var encounterDirectors = kimi.Select(k => k.Encounter.Ultimate.Reinforcements).ToArray();
            var encounterBullets = kimi.SelectMany(k => new[] { k.Encounter.Moon.Projectiles, k.Encounter.Ultimate.Blades }).ToArray();
            if (!new HashSet<EnemyActorPool2D>(pools.Concat(encounterPools)).SetEquals(all.OfType<EnemyActorPool2D>()) ||
                !new HashSet<EnemySpawnDirector2D>(directors.Concat(encounterDirectors)).SetEquals(all.OfType<EnemySpawnDirector2D>()) ||
                !new HashSet<EnemyProjectilePool2D>(bullets.Concat(encounterBullets)).SetEquals(all.OfType<EnemyProjectilePool2D>()))
                throw new InvalidOperationException("Scene contains enemy pools/directors/projectile pools outside this level registration. Register or explicitly remove the module first.");
            var writes = new List<Write>();
            void Ref(Object owner, string field, Object value) => writes.Add(new Write { Owner = owner, Field = field, Values = new[] { value } });
            void ArrayRef(Object owner, string field, IEnumerable<Object> values) => writes.Add(new Write { Owner = owner, Field = field, Values = values.ToArray(), IsArray = true });
            void Bool(Object owner, string field, bool value) => writes.Add(new Write { Owner = owner, Field = field, BooleanValue = value });
            Ref(bindings.ChapterRun, "_level", bindings.Level);
            Ref(bindings.ChapterRun, "_config", bindings.Level.ChapterRunConfig);
            Ref(bindings.ChapterRun, "_levelBindings", bindings);
            Ref(bindings.ChapterRun, "_restNode", bindings.RestNode);
            Ref(bindings.ChapterRun, "_session", bindings.Session);
            Ref(bindings.Session, "LevelBindings", bindings);
            var selection = Reference<OpeningCharacterSelectionController>(bindings.ChapterRun, "_selection");
            if (selection == null || selection.gameObject.scene != bindings.gameObject.scene)
                throw new InvalidOperationException("Chapter selection is missing or crosses scene boundaries.");
            Ref(selection, "_chapterRun", bindings.ChapterRun);
            ArrayRef(bindings.TokenRewards, "_enemyPools", pools.Concat(encounterPools));
            ArrayRef(bindings.WorldSnapshot, "EnemyPools", pools);
            ArrayRef(bindings.WorldSnapshot, "EncounterEnemyPools", encounterPools);
            ArrayRef(bindings.WorldSnapshot, "EncounterProjectilePools", encounterBullets);
            foreach (var audio in all.OfType<DeepSleep.Runtime.Presentation.Audio.CombatAudioPresenter>())
                ArrayRef(audio, "EnemyPools", pools.Concat(encounterPools));
            Ref(bindings.WorldSnapshot, "EnemyBullets", bullets[0]);
            foreach (var pool in pools.Concat(encounterPools)) Ref(pool, "_perceptionRegistry", bindings.PerceptionRegistry);
            foreach (var pool in bullets.Concat(encounterBullets)) Ref(pool, "_perceptionRegistry", bindings.PerceptionRegistry);
            foreach (var entry in entries)
            {
                bindings.Level.ContentManifest.TryGetEnemy(entry.EntryId, out var content);
                Ref(entry.Director, "_channel", content.Channel);
                Bool(entry.Director, "_autoStart", false);
            }
            if (includeLifecycle)
            {
                var combatWorld = One<ChapterCombatWorld2D>(all);
                var combatGate = One<RestNodeCombatGate>(all);
                if (combatWorld.gameObject != bindings.ChapterRun.gameObject)
                    throw new InvalidOperationException("Combat World must be explicitly installed on the registered chapter object.");
                ValidateCombatGate(combatGate, bindings.gameObject.scene);
                if (bindings.WorldSnapshot.Rice == null || bindings.WorldSnapshot.Rice.gameObject.scene != bindings.gameObject.scene)
                    throw new InvalidOperationException("Combat World requires this scene's explicitly replicated Rice pool.");
                ValidateParticipants(all, bindings.ChapterRun, combatWorld.ParticipantComponents, bindings.gameObject.scene);
                Ref(bindings.ChapterRun, "_combatWorld", combatWorld);
                Ref(combatWorld, "Bindings", bindings);
                Ref(combatWorld, "Gate", combatGate);
                Ref(combatWorld, "Rice", bindings.WorldSnapshot.Rice);
            }
            var steps = directors.Cast<Object>().Concat(bullets).Concat(pools).ToArray();
            ArrayRef(bindings.SimulationLoop, "worldStepComponents", MergeManaged(References(bindings.SimulationLoop, "worldStepComponents"), steps));
            ArrayRef(bindings.AuthorityGate, "AuthorityOnly", NetworkAuthorityRules.FilterAuthority(
                MergeManaged(References(bindings.AuthorityGate, "AuthorityOnly"), steps).Cast<Behaviour>()));
            // 提前读完所有目标字段，避免字段重命名时写入到一半才失败。
            foreach (var write in writes)
            {
                if (write.BooleanValue.HasValue) ReadBoolean(write.Owner, write.Field);
                else Read(write.Owner, write.Field, write.IsArray);
            }
            return writes;
        }

        private static void ValidateCombatGate(RestNodeCombatGate gate, Scene scene)
        {
            if (gate == null || gate.gameObject.scene != scene)
                throw new InvalidOperationException("Combat gate is missing or outside the level.");
            if (!gate.TryValidateConfiguration(out string reason))
                throw new InvalidOperationException("Combat gate is invalid: " + reason);
            foreach (string field in new[] { "_laser", "_melee" })
                if (Reference<Component>(gate, field) is not Component component || component.gameObject.scene != scene)
                    throw new InvalidOperationException("Combat gate " + field + " must belong to the same level scene.");
            foreach (Object player in References(gate, "_players"))
                if (player is not Component component || component.gameObject.scene != scene)
                    throw new InvalidOperationException("Combat gate player action gates must belong to the same level scene.");
        }

        private static void ValidateParticipants(MonoBehaviour[] all, ChapterRunController chapter,
            MonoBehaviour[] participants, Scene scene)
        {
            if (participants == null || participants.Any(p => p == null || p is not IChapterCombatLifecycle || p.gameObject.scene != scene) ||
                participants.Distinct().Count() != participants.Length)
                throw new InvalidOperationException("Combat World ParticipantComponents contains missing, duplicate, cross-scene or non-lifecycle entries.");
            if (!new HashSet<MonoBehaviour>(participants).SetEquals(all.Where(p => p is IChapterCombatLifecycle)))
                throw new InvalidOperationException("Combat World ParticipantComponents must explicitly register every lifecycle module in this level exactly once; no modules are auto-discovered into registration.");
            foreach (Object objective in References(chapter, "_additionalObjectiveComponents"))
                if (objective is IChapterCombatLifecycle && !participants.Contains((MonoBehaviour)objective))
                    throw new InvalidOperationException("Combat World ParticipantComponents omits chapter lifecycle objective " + objective.name + ".");
        }

        /// <summary>保留非敌模块的相对顺序和全部引用；敌人步骤只在原有槽位按显式清单顺序替换。</summary>
        internal static Object[] MergeManaged(Object[] previous, Object[] desired)
        {
            if (previous.Any(v => v == null)) throw new InvalidOperationException("Existing mixed list contains a missing reference; resolve it before applying registration.");
            var result = new List<Object>();
            int next = 0;
            int lastManaged = System.Array.FindLastIndex(previous, IsManaged);
            for (int i = 0; i < previous.Length; i++)
            {
                if (IsManaged(previous[i])) { if (next < desired.Length) result.Add(desired[next++]); }
                else result.Add(previous[i]);
                if (i == lastManaged) while (next < desired.Length) result.Add(desired[next++]);
            }
            if (lastManaged < 0) result.AddRange(desired);
            return result.ToArray();
        }

        internal static bool IsManaged(Object value) => value is EnemySpawnDirector2D || value is EnemyActorPool2D || value is EnemyProjectilePool2D;
        internal static Object[] References(Object owner, string field) => Read(owner, field, true);
        internal static T Reference<T>(Object owner, string field) where T : Object => Read(owner, field, false)[0] as T;
        private static bool ReadBoolean(Object owner, string field)
        {
            if (owner == null) throw new InvalidOperationException("Missing owner for " + field);
            var value = new SerializedObject(owner).FindProperty(field);
            if (value == null || value.propertyType != SerializedPropertyType.Boolean)
                throw new InvalidOperationException(owner.name + "." + field + " is missing or is not boolean.");
            return value.boolValue;
        }
        private static Object[] Read(Object owner, string field, bool array)
        {
            if (owner == null) throw new InvalidOperationException("Missing owner for " + field);
            var value = new SerializedObject(owner).FindProperty(field);
            if (value == null || value.isArray != array) throw new InvalidOperationException(owner.name + "." + field + " is missing or has the wrong shape.");
            if (!array) return new[] { value.objectReferenceValue };
            var result = new Object[value.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = value.GetArrayElementAtIndex(i).objectReferenceValue;
            return result;
        }
        private static void SetReference(Object owner, string field, Object value)
        {
            Undo.RecordObject(owner, "Bind level definition");
            var data = new SerializedObject(owner); Assign(data, field, value); data.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }
        private static void Assign(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
        private static MonoBehaviour[] Components(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
        private static T One<T>(MonoBehaviour[] all) where T : MonoBehaviour
        {
            var found = all.OfType<T>().ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Selected scene requires exactly one " + typeof(T).Name + "; found " + found.Length + ".");
            return found[0];
        }
        private static void ValidateNewAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/_Project/Configs/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.Ordinal) || path.Contains("..") || path.Contains('\\') ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/')))
                throw new InvalidOperationException("Choose a .asset path inside an existing Assets/_Project/Configs folder.");
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite existing asset: " + path);
        }
        private static void RequireEditScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before explicit level assembly.");
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Select a loaded, saved gameplay scene.");
        }
        private static LevelSceneBindings SelectedBindings()
        {
            var selected = Selection.activeGameObject;
            var bindings = selected != null ? selected.GetComponent<LevelSceneBindings>() : null;
            if (bindings == null) throw new InvalidOperationException("Select the scene object carrying LevelSceneBindings.");
            return bindings;
        }
    }
}
