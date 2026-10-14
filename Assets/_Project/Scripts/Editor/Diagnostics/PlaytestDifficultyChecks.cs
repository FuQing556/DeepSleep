using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>正式场景绑定及两档耐久回归；预览场景不保存，不改变用户偏好。</summary>
    public static class PlaytestDifficultyChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int count = 0;
            void Check(bool ok, string reason) { count++; if (!ok) throw new Exception(reason); }
            foreach (string level in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + level + ".unity");
                try
                {
                    var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    var context = all.OfType<RunDifficultyContext>().Single();
                    Check(context.Config != null && context.Config.EasyDurabilityMultiplier == .5f, level + " difficulty asset");
                    Check(all.OfType<ControlSettingsPanel>().All(p => p.EasyModeButton == null), "No in-run difficulty editing");
                    foreach (var c in all)
                    {
                        var field = c != null ? c.GetType().GetField("Difficulty") : null;
                        if (field != null && field.FieldType == typeof(RunDifficultyContext))
                            Check(field.GetValue(c) == context, c.name + " difficulty binding");
                    }
                    foreach (bool easy in new[] { false, true, false })
                    {
                        context.ApplyAuthority(easy);
                        float scale = easy ? .5f : 1;
                        Check(context.Health(999) == 999 * scale, "Odd HP remains fractional");
                        Check(context.Hits(3) == (easy ? 2 : 3), "Odd hit shield rounds up");
                        foreach (var boss in all.OfType<KimiBoss2D>())
                            Check(boss.MaximumHealth == boss.Config.MaximumHealth * scale, "Kimi health reset");
                        foreach (var boss in all.OfType<ClaudeBossActor2D>())
                            Check(boss.MaximumHealth == boss.Config.MaximumHealth * scale, "Claude health reset");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Combat/Enemies" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var hp in root.GetComponentsInChildren<HealthComponent>(true))
                    {
                        typeof(HealthComponent).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hp, null);
                        float original = hp.MaximumHealth;
                        foreach (float wave in new[] { 1f, 1.5f, 2f, 3f })
                        {
                            hp.SetSpawnHealthMultiplier(1); hp.SetMaximumHealthBonus(original * (wave - 1));
                            float standard = hp.MaximumHealth;
                            hp.SetSpawnHealthMultiplier(.5f); hp.ResetToMaximum();
                            Check(hp.CurrentHealth == standard * .5f, path + " easy wave health");
                            hp.SetSpawnHealthMultiplier(1); hp.ResetToMaximum();
                            Check(hp.CurrentHealth == standard, path + " pool reuse restores standard");
                        }
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return count + " difficulty binding/health checks passed (not device or full encounter tests)";
        }
    }
}
