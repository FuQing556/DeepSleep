using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class World02WaveHealthChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterRunConfig>("Assets/_Project/Configs/Progression/CFG_ChapterRun_World02_2066.asset");
            var original = AssetDatabase.LoadAssetAtPath<ChapterRunConfig>("Assets/_Project/Configs/Progression/CFG_ChapterRun_World01_EarlyInternet.asset");
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            Check(chapter.TryValidate(out var reason), reason);
            foreach (var c in new[] { chapter, original })
                for (int wave = 1; wave <= 4; wave++)
                {
                    var segment = c.GetSegment(wave);
                    foreach (var rule in segment.SpawnRules)
                    {
                        bool changed = c == chapter && ((wave == 2 && (rule.Channel.name == "CFG_QuickApp_Channel" ||
                            rule.Channel.name == "CFG_EN_SpawnChannel_DataCrawlerSnake")) ||
                            (wave == 3 && (rule.Channel.name == "CFG_QuickApp_Channel" || rule.Channel.name == "CFG_Download_Channel")));
                        if (!changed) Check(rule.ResolveHealthMultiplier(segment.EnemyHealthMultiplier) == segment.EnemyHealthMultiplier,
                            "Other channels inherit original wave HP");
                    }
                }
            var root = new GameObject("WaveHealthCheck") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var health = root.AddComponent<HealthComponent>();
                var field = typeof(HealthComponent).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
                void Sample(int wave, string channel, string asset, float expected)
                {
                    var config = AssetDatabase.LoadAssetAtPath<HealthConfig>("Assets/_Project/Configs/Combat/Enemies/" + asset);
                    field.SetValue(health, config);
                    typeof(HealthComponent).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
                    var segment = chapter.GetSegment(wave);
                    var rule = segment.SpawnRules.Single(r => r.Channel.name == channel);
                    health.SetMaximumHealthBonus(0);
                    float baseline = health.MaximumHealth;
                    health.SetMaximumHealthBonus(baseline * rule.ResolveHealthMultiplier(segment.EnemyHealthMultiplier) - baseline);
                    health.ResetToMaximum();
                    Check(health.MaximumHealth == expected && health.CurrentHealth == expected, "Exact spawn HP " + wave + "/" + channel);
                    Check(rule.Enabled, "Adjusted channel is scheduled");
                }
                Sample(2, "CFG_QuickApp_Channel", "QuickApp/CFG_QuickApp_Health.asset", 12);
                Sample(3, "CFG_QuickApp_Channel", "QuickApp/CFG_QuickApp_Health.asset", 24);
                Sample(2, "CFG_EN_SpawnChannel_DataCrawlerSnake", "DataCrawlerSnake/CFG_EN_DataCrawlerSnake_Health_Default.asset", 20);
                Sample(3, "CFG_Download_Channel", "Internet/CFG_Download_Health.asset", 30);
                // 回池后切回第一波：清除前一波加成，不累乘，也不污染图鉴基础值。
                Sample(1, "CFG_QuickApp_Channel", "QuickApp/CFG_QuickApp_Health.asset", 3);
                Check(AssetDatabase.LoadAssetAtPath<HealthConfig>("Assets/_Project/Configs/Combat/Enemies/QuickApp/CFG_QuickApp_Health.asset").MaximumHealth == 3,
                    "Shared base health unchanged");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return checks + " World02 wave-health checks passed; exact health, pooled reset and original-wave inheritance.";
        }
    }
}
