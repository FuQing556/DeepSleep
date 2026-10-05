using System;
using System.Linq;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只在独立预览场景注入错误并修复；不保存场景，不修改正式配置资产。</summary>
    public static class LevelRegistrationChecks
    {
        [MenuItem("DeepSleep/验证/所选关卡登记与幂等回归")]
        private static void RunSelected()
        {
            if (Selection.activeObject is not MetaLevelDefinition level)
                throw new InvalidOperationException("Select the exact MetaLevelDefinition asset first.");
            var active = SceneManager.GetActiveScene();
            if (active.name != level.SceneName) throw new InvalidOperationException("Active scene does not match selected level.");
            Debug.Log(RunScenePath(active.path));
        }

        /// <summary>要求场景已迁移并保存；通过不代表玩法或真实双端网络通过。</summary>
        public static string RunScenePath(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode for registration checks.");
            Scene preview = EditorSceneManager.OpenPreviewScene(path);
            MetaLevelDefinition levelCopy = null;
            LevelContentManifest manifestCopy = null;
            ChapterRunConfig runCopy = null;
            try
            {
                var bindings = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
                var originalLevel = bindings.Level;
                var originalManifest = originalLevel.ContentManifest;
                string originalLevelJson = EditorJsonUtility.ToJson(originalLevel);
                string originalManifestJson = EditorJsonUtility.ToJson(originalManifest);
                string originalRunJson = EditorJsonUtility.ToJson(originalLevel.ChapterRunConfig);
                int checks = 0;
                void Check(bool condition, string failure)
                {
                    if (!condition) throw new InvalidOperationException("Level registration regression: " + failure);
                    checks++;
                }

                Check(LevelSceneInstaller.TryValidateDerived(bindings, out string reason), "Saved scene must already be migrated: " + reason);
                var chapterSchema = new SerializedObject(bindings.ChapterRun);
                Check(chapterSchema.FindProperty("_spawnDirectors") == null && chapterSchema.FindProperty("_enemyPools") == null,
                    "Chapter still keeps independently serialized enemy mirror arrays instead of the explicit level source.");
                string chapterJson = EditorJsonUtility.ToJson(bindings.ChapterRun);
                string nodeJson = EditorJsonUtility.ToJson(bindings.RestNode);
                var combatWorld = bindings.ChapterRun.CombatWorld;
                string combatWorldJson = EditorJsonUtility.ToJson(combatWorld);
                var nonEnemySteps = LevelSceneInstaller.References(bindings.SimulationLoop, "worldStepComponents").Where(v => !LevelSceneInstaller.IsManaged(v)).ToArray();
                var nonEnemyAuthority = LevelSceneInstaller.References(bindings.AuthorityGate, "AuthorityOnly").Where(v => !LevelSceneInstaller.IsManaged(v)).ToArray();
                Check(LevelSceneInstaller.Apply(bindings, false) == 0, "Second application is not a no-op.");
                Check(chapterJson == EditorJsonUtility.ToJson(bindings.ChapterRun) && nodeJson == EditorJsonUtility.ToJson(bindings.RestNode),
                    "Idempotent application changed chapter/node state.");

                SetReference(bindings.Session, "LevelBindings", null);
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("LevelBindings"),
                    "Session missing its direct level identity binding was accepted.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1 && bindings.Session.LevelBindings == bindings,
                    "Explicit apply did not repair exactly the session's derived level binding.");

                // 刻意污染唯一真源，不能靠已经删除的章节镜像数组继续开战/装配。
                var registeredPool = bindings.Enemies[0].Pool;
                SetReference(bindings, "_enemies.Array.data[0]._pool", null);
                Check(!bindings.TryValidateConfiguration(out _) && !LevelSceneInstaller.TryValidateDerived(bindings, out _),
                    "Missing pool in the authoritative enemy binding was accepted.");
                string beforeRejectedSourceApply = EditorJsonUtility.ToJson(bindings);
                bool invalidSourceRejected = false;
                try { LevelSceneInstaller.Apply(bindings, false); }
                catch (InvalidOperationException) { invalidSourceRejected = true; }
                Check(invalidSourceRejected && beforeRejectedSourceApply == EditorJsonUtility.ToJson(bindings),
                    "Invalid source was silently restored from an independent enemy list.");
                SetReference(bindings, "_enemies.Array.data[0]._pool", registeredPool);

                SetReference(bindings.ChapterRun, "_combatWorld", null);
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("_combatWorld"),
                    "Missing lifecycle cleanup owner did not produce an actionable error.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1, "Explicit apply did not repair exactly the missing combat World reference.");
                Check(nodeJson == EditorJsonUtility.ToJson(bindings.RestNode), "Lifecycle repair changed unrelated node parameters.");
                SetReference(combatWorld, "Bindings", null);
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("Bindings"),
                    "Missing combat World binding was accepted.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1, "Explicit apply did not repair the World binding.");
                SetReference(combatWorld, "Rice", null);
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("Rice"),
                    "Missing player projectile cleanup pool was accepted.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1, "Explicit apply did not repair the World Rice pool.");
                Check(combatWorldJson == EditorJsonUtility.ToJson(combatWorld), "Lifecycle repair changed participant registration or unrelated state.");

                SetBoolean(bindings.Enemies[0].Director, "_autoStart", true);
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("_autoStart"),
                    "Independent auto-start was not rejected.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1, "Explicit apply did not restore the sole chapter start owner.");

                var previousAuthority = LevelSceneInstaller.References(bindings.AuthorityGate, "AuthorityOnly");
                SetArray(bindings.AuthorityGate, "AuthorityOnly", previousAuthority.Concat(new Object[] { combatWorld, combatWorld.Gate }).ToArray());
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out _), "Lifecycle World/action gate may be authority-disabled on guests.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1 &&
                    LevelSceneInstaller.References(bindings.AuthorityGate, "AuthorityOnly").SequenceEqual(previousAuthority),
                    "Explicit apply did not restore guest lifecycle access without altering unrelated authority entries.");

                var originalParticipants = combatWorld.ParticipantComponents.Cast<Object>().ToArray();
                SetArray(combatWorld, "ParticipantComponents", new Object[] { bindings.RestNode });
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("ParticipantComponents"),
                    "Non-lifecycle participant was accepted.");
                SetArray(combatWorld, "ParticipantComponents", originalParticipants);
                if (originalParticipants.Length > 0)
                {
                    SetArray(combatWorld, "ParticipantComponents", Array.Empty<Object>());
                    Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("ParticipantComponents"),
                        "A lifecycle module omitted from cleanup was accepted.");
                    string beforeMissingParticipantApply = EditorJsonUtility.ToJson(combatWorld);
                    bool missingParticipantRejected = false;
                    try { LevelSceneInstaller.Apply(bindings, false); }
                    catch (InvalidOperationException) { missingParticipantRejected = true; }
                    Check(missingParticipantRejected && beforeMissingParticipantApply == EditorJsonUtility.ToJson(combatWorld),
                        "Missing participant was silently auto-discovered or consumers were partially changed.");
                    SetArray(combatWorld, "ParticipantComponents", new[] { originalParticipants[0], originalParticipants[0] });
                    Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("ParticipantComponents"),
                        "Duplicate lifecycle participant was accepted.");
                    SetArray(combatWorld, "ParticipantComponents", originalParticipants);
                }
                LevelSceneInstaller.InstallLifecycle(preview);
                Check(chapterJson == EditorJsonUtility.ToJson(bindings.ChapterRun) && nodeJson == EditorJsonUtility.ToJson(bindings.RestNode) &&
                    combatWorldJson == EditorJsonUtility.ToJson(combatWorld), "Repeated explicit lifecycle migration changed registered content or parameters.");

                var previousSteps = LevelSceneInstaller.References(bindings.SimulationLoop, "worldStepComponents");
                SetArray(bindings.SimulationLoop, "worldStepComponents", previousSteps.Where(v => v != bindings.Enemies[0].Director).ToArray());
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out _), "Missing simulation registration was accepted.");
                LevelSceneInstaller.Apply(bindings, false);
                Check(LevelSceneInstaller.References(bindings.SimulationLoop, "worldStepComponents").Where(v => !LevelSceneInstaller.IsManaged(v)).SequenceEqual(nonEnemySteps),
                    "Repair reordered or removed non-enemy simulation steps.");
                Check(LevelSceneInstaller.References(bindings.AuthorityGate, "AuthorityOnly").Where(v => !LevelSceneInstaller.IsManaged(v)).SequenceEqual(nonEnemyAuthority),
                    "Repair reordered or removed non-enemy authority entries.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 0, "Repair is not idempotent.");

                SetReference(bindings.Enemies[0].Director, "_channel", null);
                Check(!bindings.TryValidateConfiguration(out _), "Runtime accepted a director with no channel.");
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("_channel"),
                    "New prefab's missing channel is not an explicit derived write.");
                Check(LevelSceneInstaller.Apply(bindings, false) == 1 && bindings.TryValidateConfiguration(out _),
                    "Explicit registration cannot wire a new prefab's declared channel.");

                // 只篡改预览场景中的绑定，确认错误在写入前阻断。
                string secondBindingId = bindings.Enemies[1].EntryId;
                var bindingData = new SerializedObject(bindings);
                var enemyEntries = bindingData.FindProperty("_enemies");
                Check(enemyEntries.arraySize > 1, "Existing gameplay fixture must contain at least two enemy modules.");
                enemyEntries.GetArrayElementAtIndex(1).FindPropertyRelative("_entryId").stringValue = bindings.Enemies[0].EntryId;
                bindingData.ApplyModifiedPropertiesWithoutUndo();
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out _), "Duplicate binding ID was accepted.");
                string beforeRejectedApply = EditorJsonUtility.ToJson(bindings.RestNode);
                bool rejected = false;
                try { LevelSceneInstaller.Apply(bindings, false); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && beforeRejectedApply == EditorJsonUtility.ToJson(bindings.RestNode), "Invalid registration partially wrote consumers.");
                // 预览场景引用必须保留原对象句柄；整组件 JSON 恢复会按原场景标识重新解引用。
                SetString(bindings, "_enemies.Array.data[1]._entryId", secondBindingId);
                Check(bindings.TryValidateConfiguration(out reason), "Duplicate-ID fixture restoration failed: " + reason);

                levelCopy = Object.Instantiate(originalLevel);
                manifestCopy = Object.Instantiate(originalManifest);
                SetReference(levelCopy, "_contentManifest", manifestCopy);
                SetReference(bindings, "_level", levelCopy);
                var manifestData = new SerializedObject(manifestCopy);
                var manifestEntries = manifestData.FindProperty("_enemies");
                manifestEntries.GetArrayElementAtIndex(1).FindPropertyRelative("_entryId").stringValue = originalManifest.Enemies[0].EntryId;
                manifestData.ApplyModifiedPropertiesWithoutUndo();
                Check(!manifestCopy.TryValidate(out _), "Duplicate manifest ID was accepted.");
                SetString(manifestCopy, "_enemies.Array.data[1]._entryId", originalManifest.Enemies[1].EntryId);
                runCopy = Object.Instantiate(originalLevel.ChapterRunConfig);
                SetReference(levelCopy, "_chapterRunConfig", runCopy);
                var invalidRunData = new SerializedObject(runCopy);
                invalidRunData.FindProperty("_segments").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("_durationSeconds").floatValue = 0f;
                invalidRunData.ApplyModifiedPropertiesWithoutUndo();
                Check(!bindings.TryValidateConfiguration(out _),
                    "Edit-mode static validation accepted an invalid authoritative segment configuration.");
                SetReference(levelCopy, "_chapterRunConfig", originalLevel.ChapterRunConfig);
                Check(LevelIdentityValidation.TryValidate(originalLevel, preview.name, null, false, out _), "Direct editor launch identity failed.");
                Check(!LevelIdentityValidation.TryValidate(originalLevel, preview.name, levelCopy, false, out _), "Same-ID different level asset was accepted as launch intent.");
                Check(!LevelIdentityValidation.TryValidate(originalLevel, preview.name + "_wrong", null, false, out _), "Wrong-scene level binding was accepted.");

                // 不支持的第二敌弹池必须在派生任何数组前明确拒绝。
                SetReference(bindings, "_level", originalLevel);
                Check(bindings.TryValidateConfiguration(out reason), "Level-copy fixture restoration failed: " + reason);
                var originalProjectilePools = bindings.Enemies[0].ProjectilePools.Cast<Object>().ToArray();
                var temporary = new GameObject("LevelRegistration_UnsupportedBulletPool");
                SceneManager.MoveGameObjectToScene(temporary, preview);
                var extraPool = temporary.AddComponent<EnemyProjectilePool2D>();
                var extraData = new SerializedObject(bindings);
                var bullets = extraData.FindProperty("_enemies").GetArrayElementAtIndex(0).FindPropertyRelative("_projectilePools");
                int oldCount = bullets.arraySize;
                bullets.arraySize++;
                bullets.GetArrayElementAtIndex(oldCount).objectReferenceValue = extraPool;
                extraData.ApplyModifiedPropertiesWithoutUndo();
                Check(!LevelSceneInstaller.TryValidateDerived(bindings, out reason) && reason.Contains("projectile pool"),
                    "Unsupported multiple enemy projectile pools did not produce the explicit policy error: " + reason);
                SetArray(bindings, "_enemies.Array.data[0]._projectilePools", originalProjectilePools);
                Object.DestroyImmediate(temporary);

                Check(originalLevelJson == EditorJsonUtility.ToJson(originalLevel) && originalManifestJson == EditorJsonUtility.ToJson(originalManifest) &&
                    originalRunJson == EditorJsonUtility.ToJson(originalLevel.ChapterRunConfig), "Checks changed a production definition/config asset.");
                Check(LevelSceneInstaller.TryValidateDerived(bindings, out reason), "Fixture did not restore cleanly: " + reason);
                return "PASS: " + checks + " level registration assertions in " + path +
                    ". Lifecycle migration/idempotence, cleanup/participant/simulation omission, sole chapter spawn ownership, guest lifecycle access, unrelated ordering, duplicate identity, launch mismatch, unsupported multiple projectile pools. Preview scene only; no scenes/assets saved.";
            }
            finally
            {
                if (levelCopy != null) Object.DestroyImmediate(levelCopy);
                if (manifestCopy != null) Object.DestroyImmediate(manifestCopy);
                if (runCopy != null) Object.DestroyImmediate(runCopy);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void SetArray(Object owner, string field, Object[] values)
        {
            var data = new SerializedObject(owner);
            var array = data.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetReference(Object owner, string field, Object value)
        {
            var data = new SerializedObject(owner);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetString(Object owner, string field, string value)
        {
            var data = new SerializedObject(owner);
            data.FindProperty(field).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetBoolean(Object owner, string field, bool value)
        {
            var data = new SerializedObject(owner);
            data.FindProperty(field).boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
