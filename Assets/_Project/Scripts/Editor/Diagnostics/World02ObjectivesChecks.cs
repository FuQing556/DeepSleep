using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class World02ObjectivesChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit mode.");
            int checks = 0;
            void Check(bool condition, string message)
            { checks++; if (!condition) throw new InvalidOperationException(message); }
            var config = AssetDatabase.LoadAssetAtPath<ChapterRunConfig>(World02ObjectivesInstaller.RunPath);
            Check(config.TryValidate(out _), "Valid wave configuration");
            string[] names = { "递归调用", "来杯好茶", "销冠王老板", "？？？" };
            int[] counts = { 3, 6, 6, 1 };
            float[] durations = { 45, 55, 60, 65 };
            string[] paths = {
                "RecursiveJelly/CFG_Recursive_Large_Channel", "QuickApp/CFG_QuickApp_Channel",
                "Internet/CFG_Download_Channel", "RecursiveJelly/CFG_Recursive_Medium_Channel",
                "RecursiveJelly/CFG_Recursive_Small_Channel", "SpawnChannels/CFG_EN_SpawnChannel_DataCrawlerSnake"
            };
            var channels = paths.Select(p => AssetDatabase.LoadAssetAtPath<EnemySpawnChannelDefinition>(
                "Assets/_Project/Configs/Combat/Enemies/" + p + ".asset")).ToArray();
            Check(channels.All(c => c != null), "All test channels exist");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var chapter = all.OfType<ChapterRunController>().Single();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Set(string field, object value) => typeof(ChapterRunController).GetField(field, flags).SetValue(chapter, value);
                Set("_runConfig", config);
                Set("_session", null); // isolated authority; never touches the live session/profile
                Set("_phase", ChapterRunPhase.Combat);
                var despawn = typeof(ChapterRunController).GetMethod("OnEnemyDespawned", flags);
                for (int wave = 1; wave <= 4; wave++)
                {
                    var segment = config.GetSegment(wave);
                    Check(segment.DisplayName == names[wave - 1], "Exact wave name " + wave);
                    Check(segment.RequiredDefeats == counts[wave - 1], "Goal count " + wave);
                    Check(segment.DurationSeconds == durations[wave - 1] && segment.EnemyHealthMultiplier == (wave == 4 ? 3 : 1 + .5f * (wave - 1)), "Original timing/health " + wave);
                    Set("_segmentNumber", wave); Set("_defeats", 0);
                    foreach (var channel in channels)
                    {
                        int before = (int)typeof(ChapterRunController).GetField("_defeats", flags).GetValue(chapter);
                        despawn.Invoke(chapter, new object[] { new EnemyDespawnRequest2D(EnemyDespawnReason.Defeated, Vector2.zero, 0, Vector2.left), channel });
                        int after = (int)typeof(ChapterRunController).GetField("_defeats", flags).GetValue(chapter);
                        Check(after - before == (wave <= 3 && channel == channels[wave - 1] ? 1 : 0), "Real defeat routing " + wave + "/" + channel.name);
                        foreach (var reason in new[] { EnemyDespawnReason.ExitedPlayfield, EnemyDespawnReason.ContactImpact })
                        {
                            despawn.Invoke(chapter, new object[] { new EnemyDespawnRequest2D(reason, Vector2.zero, 0, Vector2.left), channel });
                            Check((int)typeof(ChapterRunController).GetField("_defeats", flags).GetValue(chapter) == after, "No kill credit for " + reason);
                        }
                    }
                    Check(segment.ObjectiveMode == (wave < 4 ? ChapterObjectiveMode.EnemyChannel : ChapterObjectiveMode.EncountersOnly), "Objective mode " + wave);
                }
                var boss = all.OfType<ClaudeChapterEncounterDriver2D>().Single();
                Check(boss.IsRequiredForSegment(4) && !boss.IsRequiredForSegment(3), "Claude required only in fourth wave");
                Check(boss.DisplayTitle == "权限之外" && boss.ObjectiveText == "击败 Claude", "Boss takeover text");
                Check(chapter.LevelBindings.Level.ChapterRunConfig == config, "Formal scene uses updated config");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return checks + " World02 objective checks passed: exact names, target-only kills, non-kill exclusions, timings, health, formal bindings and boss takeover.";
        }
    }
}
