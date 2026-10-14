using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class PlaytestDifficultyInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing edits first.");
            const string path = "Assets/_Project/Configs/Progression/CFG_RunDifficulty.asset";
            var config = AssetDatabase.LoadAssetAtPath<RunDifficultyConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<RunDifficultyConfig>(); AssetDatabase.CreateAsset(config, path); }
            config.EasyDurabilityMultiplier = .5f; EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            var active = SceneManager.GetActiveScene();
            foreach (string level in new[] { "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                var scene = SceneManager.GetSceneByPath("Assets/Scenes/" + level + ".unity");
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Additive);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var components = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    if (level == "MainMenu")
                    {
                        var controls = components.OfType<ControlSettingsPanel>().Single();
                        if (controls.EasyModeButton == null)
                        {
                            var palette = components.OfType<UiThemeView>().First(t => t.DeepSeek != null && t.Harness != null);
                            controls.EasyModeButton = PlaytestAssistanceInstaller.ButtonCopy(controls.AimAssistButton,
                                controls.transform, "EasyMode", "简单模式（全部关卡）：关", new Vector2(0, -120), new Vector2(740, 88), palette);
                            controls.EasyModeLabel = controls.EasyModeButton.GetComponentInChildren<Text>(true);
                        }
                        controls.EasyModeTitle = "简单模式（全部关卡）：";
                        EditorUtility.SetDirty(controls);
                    }
                    else
                    {
                        var chapter = components.OfType<ChapterRunController>().Single();
                        var difficulty = chapter.GetComponent<RunDifficultyContext>();
                        if (difficulty == null) difficulty = chapter.gameObject.AddComponent<RunDifficultyContext>();
                        difficulty.Config = config;
                        foreach (var c in components)
                        {
                            switch (c)
                            {
                                case EnemySpawnDirector2D x: x.Difficulty = difficulty; break;
                                case KimiBoss2D x: x.Difficulty = difficulty; break;
                                case ClaudeBossActor2D x: x.Difficulty = difficulty; break;
                                case ClaudeEnergyPattern2D x: x.Difficulty = difficulty; break;
                                case ClaudePermissionBook2D x: x.Difficulty = difficulty; break;
                                case DoubaoWordWallEncounter2D x: x.Difficulty = difficulty; break;
                                case CoopSessionController x: x.Difficulty = difficulty; break;
                                case CoopSessionMenu x: x.StandardDifficultyText = "标准模式"; x.EasyDifficultyText = "简单模式 · 敌方耐久减半"; break;
                            }
                            if (c is EnemySpawnDirector2D || c is KimiBoss2D || c is ClaudeBossActor2D ||
                                c is ClaudeEnergyPattern2D || c is ClaudePermissionBook2D || c is DoubaoWordWallEncounter2D ||
                                c is CoopSessionController || c is CoopSessionMenu)
                            {
                                EditorUtility.SetDirty(c);
                                if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                            }
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            return "Difficulty configured across all combat scenes; main-menu-only toggle. Enemy/boss/mechanic durability x0.5.";
        }
    }
}
