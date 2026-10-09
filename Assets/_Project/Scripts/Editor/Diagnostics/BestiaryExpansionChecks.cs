using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Progression.Bestiary;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class BestiaryExpansionChecks
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
        static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            int checks = 0;
            void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
            var entries = AssetDatabase.FindAssets("t:BestiaryEntryDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(e => e.Level.SceneName == "World01_EarlyInternet").ToArray();
            Check(entries.Length == 6 && entries.Select(e => e.EntryId).Distinct().Count() == 6, "Six independent entries");
            var kimi = entries.Single(e => e.EntryId == "kimi");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World01_EarlyInternet.unity");
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var chapter = all.OfType<ChapterRunController>().Single();
                var doubao = all.OfType<DoubaoChapterEncounterDriver2D>().Single();
                var boss = all.OfType<KimiChapterEncounterDriver2D>().Single();
                Set(chapter, "_runConfig", chapter.LevelBindings.Level.ChapterRunConfig);
                Set(chapter, "_enemies", chapter.LevelBindings.Enemies);
                Set(chapter, "_phase", ChapterRunPhase.Combat);
                foreach (var entry in entries)
                {
                    Check(entry.TryValidate(out _), "Valid entry " + entry.EntryId);
                    Set(chapter, "<Challenge>k__BackingField", entry); Set(chapter, "_segmentNumber", entry.CombatSegment);
                    Call(chapter, "ApplyCurrentSegmentTuning");
                    Check(doubao.IsRequiredForSegment(entry.CombatSegment) == (entry.ChallengeKind == BestiaryChallengeKind.Doubao), "Doubao isolation " + entry.EntryId);
                    Check(boss.IsRequiredForSegment(entry.CombatSegment) == (entry.ChallengeKind == BestiaryChallengeKind.Kimi), "Kimi isolation " + entry.EntryId);
                    if (entry != kimi)
                    {
                        Check(entry.StartingTokensPerRole == 300 && entry.CompletionVoucherReward == 0 && entry.Level == kimi.Level,
                            "New entry uses300/zero vouchers/dusk level");
                        Check(entry.PreparationBackdrop == kimi.PreparationBackdrop && entry.PreparationLayout.Hotspots.Length == kimi.PreparationLayout.Hotspots.Length,
                            "Test island preparation");
                    }
                    foreach (var binding in chapter.LevelBindings.Enemies)
                    {
                        var director = binding.Director;
                        bool selected = entry.ChallengeKind == BestiaryChallengeKind.EnemyChannel && entry.EnemyChannel == director.Channel;
                        Check((bool)Get(director, "_runtimeEnabled") == selected, "Only selected channel enabled");
                        if (!selected) continue;
                        var poolConfig = (EnemyPoolConfig)Get(binding.Pool, "_config");
                        Check(poolConfig.InitialCapacity >= 10 && poolConfig.MaximumCapacity >= 10, "Pool prewarmed for ten simultaneous enemies");
                        Check(entry.EnemyCount==10 && entry.EnemyBatchSize==5 && entry.EnemyBatchIntervalSeconds==3 &&
                            (int)Get(director, "_runtimeSpawnLimit") == 10 && (int)Get(director,"_runtimeBatchQuota")==5 && (bool)Get(director, "_runtimeSpawnAllAtOnce") &&
                            (float)Get(director, "_runtimeInitialDelaySeconds") == 0 && director.EffectiveMaximumAliveCount == 10,
                            "Ten total / five per same-tick batch / no delay");
                        Set(director, "_random", new System.Random(1)); Set(director, "_spawnedCount", 5);
                        Check(!director.TrySpawnNow(), "Cannot spawn sixth enemy in one batch");
                        CheckTimedBatches(director, binding.Pool, Check);
                        Set(chapter, "_defeats", 0);
                        Set(chapter, "_challengeResolvedEnemies", 0);
                        var death = new EnemyDespawnRequest2D(EnemyDespawnReason.Defeated, Vector2.zero, 0, Vector2.left);
                        foreach (var other in chapter.LevelBindings.Enemies)
                            Call(chapter, "OnEnemyDespawned", death, other.Director.Channel);
                        Check(chapter.Defeats == 1, "Only selected enemy counts");
                        Check(chapter.ChallengeResolvedEnemies == 1, "Only selected channel resolves");
                        for (int i = 0; i < 9; i++)
                            Call(chapter, "OnEnemyDespawned", new EnemyDespawnRequest2D(EnemyDespawnReason.ExitedPlayfield, Vector2.zero, 0, Vector2.left), director.Channel);
                        Check(chapter.ChallengeResolvedEnemies == 10 && chapter.Defeats == 1,
                            "One kill and nine escaped enemies finish ten, without awarding fake kills");
                        Call(chapter, "OnEnemyDespawned", new EnemyDespawnRequest2D(EnemyDespawnReason.RunReset, Vector2.zero, 0, Vector2.left), director.Channel);
                        Check(chapter.ChallengeResolvedEnemies == 10, "Checkpoint/forced cleanup does not resolve challenge");
                        Set(chapter, "_challengeResolvedEnemies", 0);
                        Call(chapter, "OnEnemyDespawned", new EnemyDespawnRequest2D(EnemyDespawnReason.ContactImpact, Vector2.zero, 0, Vector2.left), director.Channel);
                        Check(chapter.ChallengeResolvedEnemies == 1 && chapter.Defeats == 1, "Contact despawn resolves without kill");
                    }
                }
                Set(chapter, "<Challenge>k__BackingField", null); Set(chapter, "_segmentNumber", 2);
                Call(chapter, "ApplyCurrentSegmentTuning");
                foreach (var binding in chapter.LevelBindings.Enemies)
                    Check((int)Get(binding.Director, "_runtimeSpawnLimit") == 0 && (float)Get(binding.Director, "_runtimeSpawnInterval") == 0 && !(bool)Get(binding.Director, "_runtimeSpawnAllAtOnce") &&
                        (int)Get(binding.Director,"_runtimeBatchSize")==0,
                        "Ordinary level restores unlimited original schedule");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu.unity");
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BestiaryMenuView>(true)).Single();
                Check(view.Entries.Length == view.EntryButtons.Length && entries.All(e => view.Entries.Contains(e)), "Original six remain independently selectable");
                for (int i = 0; i < view.Entries.Length; i++)
                {
                    view.SelectEntry(i);
                    Check(view.Entry == view.Entries[i] && view.Portrait.sprite == view.Entry.Portrait && view.NameLabel.text == view.Entry.DisplayName,
                        "Selection updates single-entry details");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return "BESTIARY PASS " + checks + " isolated checks:6 entries, exclusive channel/encounter selection,300 tokens/island/dusk,two five-enemy batches three game seconds apart, kill filtering, selector UI. No Play or user save changed.";
        }

        // Scheduler/pool integration only: do not advance enemy movement or combat.
        public static void CheckTimedBatches(EnemySpawnDirector2D director, EnemyActorPool2D pool, Action<bool,string> check)
        {
            if(pool.TotalCount==0) Call(pool,"Awake");
            check(pool.PrepareChallengeCapacity(10),"Prepare ten slots for overlapping batches");
            foreach(var actor in pool.Instances) Call(actor.Health,"Awake");
            Set(director,"_random",new System.Random(7));
            director.ApplyRuntimeTuning(true,0,1,10,1,10,2,true,5,3);
            director.Simulate(.02f);
            check(pool.ActiveCount==5 && (int)Get(director,"_spawnedCount")==5,"First simulation spawns exactly five");
            director.Simulate(0);
            director.Simulate(2.99f);
            check(pool.ActiveCount==5,"Second batch does not spawn early or while paused");
            director.Simulate(.02f);
            check(pool.ActiveCount==10 && (int)Get(director,"_spawnedCount")==10 && !director.IsRunning,
                "Second five spawn after three seconds while first five remain alive");
            director.Simulate(30);
            check(pool.ActiveCount==10 && !director.TrySpawnNow(),"No third batch or eleventh enemy");
            pool.DespawnAll(EnemyDespawnReason.RunReset);
            check(pool.ActiveCount==0,"Batch fixture cleans up pooled actors");
            director.ApplyRuntimeTuning(true,0,1,10,1,10,2,true);
            check((int)Get(director,"_runtimeBatchSize")==0 && (int)Get(director,"_runtimeBatchQuota")==10 &&
                (float)Get(director,"_remainingSeconds")==0,"Legacy unbatched finite spawn retains full immediate quota");
        }
    }
}
