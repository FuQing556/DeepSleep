using System;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Progression.Bestiary;
using DeepSleep.Runtime.Progression.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class AchievementCompletionInstaller
    {
        public const string Folder = "Assets/_Project/Configs/Progression/Meta/Achievements/";
        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void EditScene(string path, Action<Scene> edit)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
            try { edit(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static MetaLevelDefinition Level(string suffix) => AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(
            "Assets/_Project/Configs/Progression/Meta/CFG_META_Level_" + suffix + ".asset");

        public static AchievementDefinition[] Definitions() => AssetDatabase.FindAssets("t:AchievementDefinition", new[] { Folder.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AchievementDefinition>).ToArray();

        [MenuItem("DeepSleep/Setup/Complete Approved Achievements And Sky Text")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required.");
            var entries = AssetDatabase.FindAssets("t:BestiaryEntryDefinition", new[] { "Assets/_Project/Configs/Progression/Bestiary" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>).OrderBy(e => e.EntryId).ToArray();
            if (entries.Length != 9) throw new InvalidOperationException("Expected nine existing bestiary challenges.");
            ApplyRemainingText(entries);
            var definitions = new System.Collections.Generic.List<AchievementDefinition>();
            void Define(string suffix, string id, string name, string description, string trigger,
                int target = 1, string iconEntry = null, MetaLevelDefinition level = null, bool allChallenges = false, string challengeId = null)
            {
                string path = Folder + "CFG_META_Achievement_" + suffix + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<AchievementDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<AchievementDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }
                if (!string.IsNullOrEmpty(definition.AchievementId) && definition.AchievementId != id)
                    throw new InvalidOperationException("Refusing to replace existing achievement identity.");
                var so = new SerializedObject(definition);
                so.FindProperty("_achievementId").stringValue = id;
                so.FindProperty("_displayName").stringValue = name;
                so.FindProperty("_description").stringValue = description;
                so.FindProperty("_triggerId").stringValue = trigger;
                so.FindProperty("_targetCount").intValue = target;
                so.FindProperty("_hidden").boolValue = false;
                so.FindProperty("_requiredLevel").objectReferenceValue = level;
                if (iconEntry != null) so.FindProperty("_icon").objectReferenceValue = entries.Single(e => e.EntryId == iconEntry).Portrait;
                var challenges = so.FindProperty("_requiredChallenges");
                challenges.arraySize = allChallenges ? entries.Length : challengeId != null ? 1 : 0;
                for (int i = 0; i < challenges.arraySize; i++) challenges.GetArrayElementAtIndex(i).objectReferenceValue =
                    allChallenges ? entries[i] : entries.Single(e => e.EntryId == challengeId);
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                definitions.Add(definition);
            }
            Define("FirstFlight", "first_flight", "最初的飞行", "首次进入普通关卡。\n未来的神明将因果投向过去。", AchievementTriggerIds.JourneyStarted);
            Define("FirstClear", "first_clear", "第一段旅程", "首次通关任意普通关卡。", AchievementTriggerIds.LevelCleared);
            Define("FirstRevive", "first_revive", "不要睡啦", "首次复活队友。", AchievementTriggerIds.TeammateRevived);
            Define("FirstMerit", "first_merit", "第一件藏品", "首次在鲸元券商店购买商品。", AchievementTriggerIds.ProductPurchased);
            Define("FlawlessClear", "flawless_clear", "形影不离", "双方均未倒地，通关任意普通关卡。", AchievementTriggerIds.FlawlessLevelCleared);
            Define("World01Clear", "world01_clear", "黄昏之后", "首次通关第一世界 · 黄昏故都。", "world01_cleared", level: Level("World01_EarlyInternet"));
            Define("World02Clear", "world02_clear", "欢迎来到“2066”", "首次通关第二世界 · “2066”。", "world02_cleared", level: Level("World02_2066"));
            Define("KimiDefeat", "kimi_defeat", "月亮也会下班", "在普通关卡中首次击败 Kimi。", "kimi_defeated", iconEntry: "kimi", level: Level("World01_EarlyInternet"));
            Define("ClaudeDefeat", "claude_defeat", "权限之外", "在普通关卡中首次击败 Claude。", "claude_defeated", iconEntry: "claude", level: Level("World02_2066"));
            Define("PermissionBreak", "permission_break", "您没有此权限", "首次击破一本权限书。", "permission_broken", iconEntry: "claude");
            Define("MirrorBreak", "mirror_break", "镜子不是用来照的", "首次击破 Kimi 棱光镜框的一边。", "mirror_broken", iconEntry: "kimi");
            Define("FluteInterrupt", "flute_interrupt", "演奏暂停", "首次击破次数盾，打断 Kimi 的笛声蓄力。", "flute_interrupted", iconEntry: "kimi");
            Define("QuickAppDefeat", "quick_app_defeat", "拒绝弹窗", "首次击败快应用。", "quick_app_defeated", iconEntry: "quickapp");
            Define("TenRevives", "ten_revives", "还没掉线", "累计复活队友10次，AI队友完成的救援也计入。", AchievementTriggerIds.TeammateRevived, 10);
            Define("AllBestiary", "all_bestiary", "逐一排查", "分别完成全部9种图鉴挑战。重复挑战同一种不重复计数。", "bestiary_cleared", 9, allChallenges: true);

            EditScene("Assets/Scenes/Boot.unity", scene =>
            {
                var service = All<AchievementService>(scene).Single();
                var so = new SerializedObject(service); var list = so.FindProperty("_definitions"); list.arraySize = definitions.Count;
                for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(service);
            });
            foreach (string path in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
                EditScene("Assets/Scenes/" + path + ".unity", BindFacts);

            var sky = Level("PrototypeSky");
            var skyData = new SerializedObject(sky);
            skyData.FindProperty("_displayName").stringValue = "天空测试盒";
            skyData.FindProperty("_description").stringValue = "在云海之间熟悉战斗，与队友完成四波演练。";
            skyData.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(sky); AssetDatabase.SaveAssetIfDirty(sky);
            EditScene("Assets/Scenes/MainMenu.unity", scene =>
            {
                All<MainMenuController>(scene).Single().RefreshLevelCardText();
                var bestiary = All<BestiaryMenuView>(scene).Single();
                bestiary.SelectEntry(Array.IndexOf(bestiary.Entries, bestiary.Entry));
                foreach (var text in All<Text>(scene)) if (text.text == "已解锁 0/5") text.text = "已解锁 0/15";
            });
        }

        public static void BindFacts(Scene scene)
        {
            var reporter = All<GameplayAchievementReporter>(scene).Single();
            var so = new SerializedObject(reporter);
            so.FindProperty("_facts").objectReferenceValue = All<CombatAudioPresenter>(scene).Single();
            var facts = new[] {
                NetworkMessageCatalog.CombatPresentationKind.ReviveCompleted,
                NetworkMessageCatalog.CombatPresentationKind.KimiDefeat,
                NetworkMessageCatalog.CombatPresentationKind.ClaudeDefeat,
                NetworkMessageCatalog.CombatPresentationKind.ClaudeBookBreak,
                NetworkMessageCatalog.CombatPresentationKind.KimiMirrorBreak,
                NetworkMessageCatalog.CombatPresentationKind.KimiInterrupt,
                NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat
            };
            string[] triggers = { AchievementTriggerIds.TeammateRevived, "kimi_defeated", "claude_defeated", "permission_broken", "mirror_broken", "flute_interrupted", "quick_app_defeated" };
            var list = so.FindProperty("_bindings"); list.arraySize = facts.Length;
            for (int i = 0; i < facts.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Fact").intValue = (int)facts[i];
                item.FindPropertyRelative("TriggerId").stringValue = triggers[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(reporter);
        }

        private static void ApplyRemainingText(BestiaryEntryDefinition[] entries)
        {
            foreach (var entry in entries)
            {
                string notes = entry.SkillNotes;
                int challenge = notes.IndexOf("\n\n独立挑战", StringComparison.Ordinal);
                if (challenge >= 0) notes = notes.Substring(0, challenge);
                int claudeStats = notes.IndexOf("\n\n10000生命", StringComparison.Ordinal);
                if (claudeStats >= 0) notes = notes.Substring(0, claudeStats);
                notes = notes.Replace("基础生命3，", string.Empty);
                notes = notes.Replace("\n\n", "\n");
                if (entry.EntryId == "claude") notes = "全屏切割：避开预警线，二阶段连续三次。\n追踪斩：锁定位置后三连斩，二阶段同时追踪双人。\n能量球：内核被击碎也会原地爆炸，外圈无碰撞。\n权限书：击破书本，归还对应权限。";
                entry.SkillNotes = notes;
                EditorUtility.SetDirty(entry); AssetDatabase.SaveAssetIfDirty(entry);
            }
            var upgrades = AssetDatabase.LoadMainAssetAtPath("Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            var upgradeData = new SerializedObject(upgrades);
            var property = upgradeData.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.String && property.name == "_displayName")
                {
                    if (property.stringValue == "饭团加速") property.stringValue = "饭团速射";
                    if (property.stringValue == "递归连锁") property.stringValue = "终端连锁";
                }
            upgradeData.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(upgrades); AssetDatabase.SaveAssetIfDirty(upgrades);
            string[] suffixes = { "ds_borrowed_badge", "little_crown", "little_wings" };
            string[] descriptions = {
                "DS 专属服装 · 永久持有 · 无属性加成\n替换常态与倒地外观。工牌随机显隐，可同时佩戴饰品。",
                "前饰 · 永久持有 · 无属性加成\n双角色独立佩戴，可与背饰同时佩戴。",
                "背饰 · 永久持有 · 无属性加成\n双角色独立佩戴，可与前饰同时佩戴。"
            };
            for (int i = 0; i < suffixes.Length; i++)
            {
                var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(
                    "Assets/_Project/Configs/Progression/Meta/Products/CFG_META_Product_" + suffixes[i] + ".asset");
                var data = new SerializedObject(product);
                data.FindProperty("_description").stringValue = descriptions[i];
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(product); AssetDatabase.SaveAssetIfDirty(product);
            }
        }
    }
}
