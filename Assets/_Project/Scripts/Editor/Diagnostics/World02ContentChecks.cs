using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Progression.Bestiary;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class World02ContentChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target,value);
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit mode.");
            int checks=0;
            void Check(bool ok,string reason) { checks++; if(!ok)throw new Exception(reason); }
            var entries=AssetDatabase.FindAssets("t:BestiaryEntryDefinition",new[]{"Assets/_Project/Configs/Progression/Bestiary"})
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>).ToArray();
            Check(entries.Length==9 && entries.Select(e=>e.EntryId).Distinct().Count()==9,"Nine unique bestiary entries");
            foreach(var e in entries) Check(e.TryValidate(out _),"Valid entry "+e.EntryId);
            var extra=entries.Where(e=>e.Level.SceneName=="World02_2066").ToArray();
            Check(extra.Length==3,"Three World02 entries");
            var template=entries.Single(e=>e.EntryId=="kimi");
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            try
            {
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var chapter=all.OfType<ChapterRunController>().Single();
                var boss=all.OfType<ClaudeChapterEncounterDriver2D>().Single();
                Check(boss.enabled && boss.TryValidateConfiguration(out _),"Formal Claude driver enabled/valid");
                var audio=all.OfType<CombatAudioPresenter>().Single();
                Check(audio.TryValidateConfiguration(out _) && audio.Claude==boss.Encounter && audio.SceneEffects==boss.SceneEffects,"Explicit Claude/state audio");
                Check(audio.EnemyPools.All(p=>p!=null) && chapter.LevelBindings.Enemies.All(e=>audio.EnemyPools.Contains(e.Pool)),"All chapter enemy pools audible");
                var content=all.OfType<EnemyContentAudio2D>().Where(a=>a.Recursive||a.QuickApp).ToArray();
                Check(content.Length==4 && content.All(a=>a.Pool!=null && a.Audio==audio),"Three recursive pools and QuickApp bind once");
                Check(content.Count(a=>a.RecursiveSplits)==2,"Only large/medium get split sound");
                var ambience=all.OfType<SceneAudioPresenter>().Single();
                Check(ambience.CombatAmbience==AudioCue.AmbienceCyber && ambience.RestAmbience==AudioCue.AmbienceArcade &&
                      ambience.ClaudePresentation==boss.Encounter.Presentation,"Cyber/arcade/rain environmental transitions");
                Set(chapter,"_runConfig",chapter.LevelBindings.Level.ChapterRunConfig); Set(chapter,"_enemies",chapter.LevelBindings.Enemies);
                foreach(var e in extra)
                {
                    Set(chapter,"<Challenge>k__BackingField",e); Set(chapter,"_segmentNumber",e.CombatSegment);
                    chapter.GetType().GetMethod("ApplyCurrentSegmentTuning",Private).Invoke(chapter,null);
                    Check(e.PreparationBackdrop==template.PreparationBackdrop && e.PreparationLayout.Hotspots.Length==template.PreparationLayout.Hotspots.Length,"Test island preparation "+e.EntryId);
                    Check(boss.IsRequiredForSegment(e.CombatSegment)==e.IsBossChallenge,"Claude exclusive "+e.EntryId);
                    Check(!all.OfType<KimiChapterEncounterDriver2D>().Single().IsRequiredForSegment(e.CombatSegment),"Never runs Kimi in World02 challenge");
                    Check(!all.OfType<DoubaoChapterEncounterDriver2D>().Single().IsRequiredForSegment(e.CombatSegment),"Never runs Doubao in World02 challenge");
                    foreach(var binding in chapter.LevelBindings.Enemies)
                    {
                        bool expected=e.ChallengeKind==BestiaryChallengeKind.EnemyChannel && e.EnemyChannel==binding.Director.Channel;
                        Check((bool)binding.Director.GetType().GetField("_runtimeEnabled",Private).GetValue(binding.Director)==expected,"Exclusive spawn "+e.EntryId);
                    }
                    Check(e.StartingTokensPerRole==(e.IsBossChallenge?1000:300) && e.CompletionVoucherReward==(e.IsBossChallenge?5:0),"Challenge economy "+e.EntryId);
                    if(!e.IsBossChallenge)Check(e.EnemyCount==10 && e.EnemyBatchSize==5 && e.EnemyBatchIntervalSeconds==3 && e.SpawnAllAtStart && e.MaximumAlive==10,"Two batches of five, three seconds apart "+e.EntryId);
                    if(!e.IsBossChallenge)
                    {
                        var binding=chapter.LevelBindings.Enemies.Single(b=>b.Director.Channel==e.EnemyChannel);
                        BestiaryExpansionChecks.CheckTimedBatches(binding.Director,binding.Pool,Check);
                    }
                }
                var recursion=extra.Single(e=>e.EntryId=="recursive");
                Check(recursion.PoolCapacities.Select(p=>p.Capacity).SequenceEqual(new[]{10,40,80}),"Capacity reserved for whole ten-root family tree");
                var large=chapter.LevelBindings.Enemies.Single(e=>e.Director.Channel==recursion.EnemyChannel).Pool;
                var normalPool=(EnemyPoolConfig)new SerializedObject(large).FindProperty("_config").objectReferenceValue;
                Check(normalPool.MaximumCapacity==4,"Normal pool asset unchanged by challenge-only prewarm");
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
            scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu.unity");
            try
            {
                var view=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BestiaryMenuView>(true)).Single();
                Check(view.Entries.Length==9 && view.EntryButtons.Length==9,"Nine independent selectors");
                var scroll=view.transform.Find("EntryStrip").GetComponent<UnityEngine.UI.ScrollRect>();
                Check(scroll.horizontal && !scroll.vertical && scroll.content.childCount==9,"Swipe strip, not mixed combat list");
                for(int i=0;i<9;i++){view.SelectEntry(i);Check(view.Entry==view.Entries[i]&&view.Portrait.sprite==view.Entry.Portrait,"Detail/portrait swaps "+i);}
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
            var catalog=AssetDatabase.LoadAssetAtPath<GameAudioCatalog>("Assets/_Project/Audio/CFG_GameAudio.asset");
            Check(catalog.TryValidate(out _),"Complete sound catalog");
            var cues=catalog.Entries.Where(e=>e.Cue>=AudioCue.RecursiveHit).ToArray();
            Check(cues.Length==27 && cues.Sum(e=>e.Clips.Length)==37,"27 cues,37 clips");
            foreach(var cue in cues)Check(cue.Output!=null && cue.MaximumVoices<=2,"Bounded non-spam voices "+cue.Cue);
            for(int i=(int)AudioCue.KimiReveal;i<=(int)AudioCue.SceneStateEnd;i++)
                Check(((NetworkMessageCatalog.CombatPresentationKind)((int)NetworkMessageCatalog.CombatPresentationKind.KimiReveal+i-(int)AudioCue.KimiReveal)).ToString()==((AudioCue)i).ToString(),"Wire cue identity "+i);
            return "World02 content: "+checks+" isolated checks passed; bestiary identity, single challenge routing, capacities, audio bindings/catalog and wire mappings. Not listening/device acceptance.";
        }
    }
}
