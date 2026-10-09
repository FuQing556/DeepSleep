using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Progression.Bestiary;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>Explicit, repeatable World02 audio and separate bestiary assembly.</summary>
    public static class World02ContentInstaller
    {
        private static T[] All<T>(UnityEngine.SceneManagement.Scene s) where T : Component =>
            s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static EnemySpawnChannelDefinition Channel(string suffix) => AssetDatabase.LoadAssetAtPath<EnemySpawnChannelDefinition>(
            "Assets/_Project/Configs/Combat/Enemies/" + suffix + ".asset");

        public static string Install()
        {
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || original.isDirty) throw new InvalidOperationException("Require clean Edit mode.");
            InstallAudioCatalog();
            var entries = InstallEntries();
            string previous = original.path;
            try
            {
                var world = EditorSceneManager.OpenScene("Assets/Scenes/World02_2066.unity");
                typeof(CoreAudioInstaller).GetMethod("InstallScene", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { world });
                var combat = All<CombatAudioPresenter>(world).Single();
                combat.Kimi = null; combat.Encounter = null;
                combat.Claude = All<ClaudeEncounter2D>(world).Single();
                combat.SceneEffects = All<ChapterSceneEffectsController>(world).Single();
                var ambience = All<SceneAudioPresenter>(world).Single();
                ambience.CombatAmbience = AudioCue.AmbienceCyber; ambience.RestAmbience = AudioCue.AmbienceArcade;
                ambience.ClaudePresentation = combat.Claude.Presentation;
                foreach (var pool in combat.EnemyPools)
                {
                    var binder = pool.GetComponent<RecursiveJellyRuntimeBinder2D>();
                    var director = pool.GetComponent<EnemySpawnDirector2D>();
                    bool quick = director != null && director.Channel == Channel("QuickApp/CFG_QuickApp_Channel");
                    if (binder == null && !quick) continue;
                    var content = pool.GetComponent<EnemyContentAudio2D>() ?? pool.gameObject.AddComponent<EnemyContentAudio2D>();
                    content.Pool = pool; content.Audio = combat; content.Recursive = binder != null;
                    content.RecursiveSplits = binder != null && binder.Children != null; content.QuickApp = quick; content.Download = false;
                    EditorUtility.SetDirty(content);
                }
                // The copied world needs the existing preparation/retry UI, not a second challenge scene.
                var hud = All<ChapterRunHudView>(world).Single();
                var hudSO = new SerializedObject(hud);
                if (hudSO.FindProperty("_retryButton").objectReferenceValue == null)
                    throw new InvalidOperationException("World02 challenge retry button missing.");
                EditorUtility.SetDirty(combat); EditorUtility.SetDirty(ambience);
                LevelSceneInstaller.Apply(combat.Chapter.LevelBindings);
                EditorSceneManager.MarkSceneDirty(world); EditorSceneManager.SaveScene(world);
                InstallMenu(entries);
                var network = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>(
                    "Assets/_Project/Configs/Networking/CFG_Network.asset");
                Networking.NetworkBuildRevision.Apply(network); EditorUtility.SetDirty(network);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.OpenScene(previous); }
            return "World02: 3 independent bestiary entries, 37 clips / 27 cues, explicit core + enemy + Claude + state audio bindings saved.";
        }

        private static BestiaryEntryDefinition[] InstallEntries()
        {
            var template = AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>(BestiaryInstaller.EntryPath);
            var level = AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>("Assets/_Project/Configs/Progression/Meta/CFG_META_Level_World02_2066.asset");
            var large = Channel("RecursiveJelly/CFG_Recursive_Large_Channel");
            var medium = Channel("RecursiveJelly/CFG_Recursive_Medium_Channel");
            var small = Channel("RecursiveJelly/CFG_Recursive_Small_Channel");
            string[] ids = { "recursive", "quickapp", "claude" };
            string[] names = { "递归", "快应用", "Claude" };
            string[] art = { "Enemies/RecursiveJelly/SPR_EN_RecursiveJelly_Idle_v01.png", "Enemies/QuickApp/SPR_EN_QuickApp_Idle_v01.png", "Characters/Claude/SPR_CL_IdleHover.png" };
            string[] descriptions = {
                "一团还没算完的函数。沉重的果冻向前蠕动，结束一次调用，却留下更多调用。",
                "关不掉的广告拼成了一个快应用。它不追踪目标，只沿大幅弧线乱飞，穿过屏幕后又从另一边回来。",
                "踩着无人机穿行雨夜，以手诀排布几何切线。她会把行动权限写进书页，也会将积聚的能量掷向战场。" };
            string[] notes = {
                "大型被击败后，沿四个对角方向分成4只中型；每只中型再分成2只小型。\n\n接触会造成伤害并击退，但不会让它消失。留意分裂后的包围。\n\n独立挑战：分2波，每波5只大型，第一波刷出3秒后刷第二波。最后清空全部分裂后代；双角色各300 Token，无鲸元券奖励。",
                "从左右两侧出现，沿随机的巨大S形轨迹飞行，上下左右穿屏。撞击玩家后继续存活。\n\n不要把屏幕边缘当成安全墙。基础生命3，利用它经过的路线攻击。\n\n独立挑战：分2波，每波5只，第一波刷出3秒后刷第二波；双角色各300 Token，无鲸元券奖励。",
                "全屏切割：线条预警后瞬间切下，随后炸开裂痕；二阶段连续三次。\n追踪斩：锁定位置的短斜斩；二阶段重新追踪连斩。\n能量球：内核可击碎，击碎也会原地爆炸，外圈没有碰撞。\n权限书：攻击、移动、技能分别写在书页，击碎对应书本归还权限。\n\n10000生命，半血锁血转阶段。独立挑战：双角色各1000 Token，通关5鲸元券。" };
            for (int i = 0; i < 3; i++)
            {
                string path = "Assets/_Project/Configs/Progression/Bestiary/CFG_Bestiary_" + ids[i] + ".asset";
                var e = AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>(path);
                if (e == null) { e = ScriptableObject.CreateInstance<BestiaryEntryDefinition>(); AssetDatabase.CreateAsset(e, path); }
                e.EntryId = ids[i]; e.DisplayName = names[i]; e.Subtitle = i == 2 ? "“2066” · 雨夜的权限编织者" : "“2066” · 单怪练习";
                e.Description = descriptions[i]; e.SkillNotes = notes[i]; e.Level = level;
                e.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/" + art[i]);
                e.PreparationBackdrop = template.PreparationBackdrop; e.PreparationLayout = template.PreparationLayout;
                e.StartingTokensPerRole = i == 2 ? 1000 : 300; e.CompletionVoucherReward = i == 2 ? 5 : 0;
                e.CombatSegment = i == 2 ? 4 : 1; e.PreludeSeconds = 3; e.ChallengeSeconds = 300;
                e.ChallengeKind = i == 2 ? BestiaryChallengeKind.Claude : BestiaryChallengeKind.EnemyChannel;
                e.EnemyChannel = i == 0 ? large : i == 1 ? Channel("QuickApp/CFG_QuickApp_Channel") : null;
                e.EnemyCount = i == 2 ? 1 : 10; e.MaximumAlive = 10; e.SpawnAllAtStart = i != 2; e.SpawnIntervalSeconds = 2;
                e.EnemyBatchSize = i == 2 ? 0 : 5;
                e.EnemyBatchIntervalSeconds = i == 2 ? 0 : 3;
                e.PoolCapacities = i == 0 ? new[] {
                    new BestiaryEntryDefinition.ChallengePoolCapacity { Channel = large, Capacity = 10 },
                    new BestiaryEntryDefinition.ChallengePoolCapacity { Channel = medium, Capacity = 40 },
                    new BestiaryEntryDefinition.ChallengePoolCapacity { Channel = small, Capacity = 80 } } :
                    i == 1 ? new[] { new BestiaryEntryDefinition.ChallengePoolCapacity { Channel = e.EnemyChannel, Capacity = 10 } } :
                    Array.Empty<BestiaryEntryDefinition.ChallengePoolCapacity>();
                e.PreparationPrompt = names[i] + " 独立挑战 · 双角色各" + e.StartingTokensPerRole + " Token · 配装后进入传送门";
                if (!e.TryValidate(out string reason)) throw new InvalidOperationException(names[i] + ": " + reason);
                EditorUtility.SetDirty(e);
            }
            return AssetDatabase.FindAssets("t:BestiaryEntryDefinition", new[] { "Assets/_Project/Configs/Progression/Bestiary" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>)
                .OrderBy(e => Array.IndexOf(new[] { "404", "crawler", "download", "360", "doubao", "kimi", "recursive", "quickapp", "claude" }, e.EntryId)).ToArray();
        }

        private static void InstallAudioCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>("Assets/_Project/Audio/CFG_GameAudio.asset");
            var list = catalog.Entries.ToList();
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                if (cue < AudioCue.RecursiveHit) continue;
                bool ambient = cue >= AudioCue.AmbienceCyber;
                string folder = ambient ? "Assets/_Project/Audio/Ambience" : "Assets/_Project/Audio/SFX/World02";
                var paths = Directory.GetFiles(folder, (ambient ? "AMB_" : "SFX_") + cue + "_*.wav").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray();
                if (paths.Length == 0) throw new InvalidOperationException("Missing sound " + cue);
                foreach (var p in paths)
                {
                    AssetDatabase.ImportAsset(p);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(p);
                    importer.forceToMono = true; importer.loadInBackground = ambient;
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = ambient ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = ambient ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                    settings.quality = .75f; settings.preloadAudioData = !ambient;
                    importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                }
                var entry = list.SingleOrDefault(e => e.Cue == cue);
                if (entry == null) { entry = new AudioCueDefinition { Cue = cue }; list.Add(entry); }
                var group = catalog.Entries.First(e => e.Cue == (ambient ? AudioCue.AmbienceDusk : AudioCue.EnemyDefeat)).Output;
                entry.Clips = paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray(); entry.Output = group;
                entry.Gain = ambient ? .5f : cue == AudioCue.ClaudeEnergyBurst ? .55f : .45f;
                entry.PitchVariation = ambient ? 0 : .01f; entry.MinimumInterval = .12f;
                entry.MaximumVoices = cue == AudioCue.ClaudeHit || cue == AudioCue.RecursiveHit ? 2 : 1;
                entry.Importance = cue == AudioCue.ClaudeCutFire || cue == AudioCue.ClaudeEnergyBurst ? 80 : cue == AudioCue.ClaudeHit || cue == AudioCue.RecursiveHit ? 20 : 55;
                entry.IsUi = false; entry.PauseWithWorld = cue != AudioCue.ClaudeDefeat;
            }
            catalog.Entries = list.ToArray();
            if (!catalog.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var t = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            t.SetParent(parent, false); t.anchorMin = t.anchorMax = t.pivot = new Vector2(.5f, .5f);
            t.sizeDelta = size; t.anchoredPosition = position; return t;
        }
        private static void InstallMenu(BestiaryEntryDefinition[] entries)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var view = All<BestiaryMenuView>(scene).Single(); var page = view.transform;
            var strip = page.Find("EntryStrip") as RectTransform ?? Rect(page, "EntryStrip", new Vector2(1308, 64), new Vector2(0,280));
            if (strip.GetComponent<RectMask2D>() == null) strip.gameObject.AddComponent<RectMask2D>();
            var content = strip.Find("Content") as RectTransform ?? Rect(strip, "Content", new Vector2(entries.Length*218,64), Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0,.5f); content.pivot = new Vector2(0,.5f);
            content.sizeDelta = new Vector2(entries.Length*218,64); content.anchoredPosition = Vector2.zero;
            var scroll = strip.GetComponent<ScrollRect>() ?? strip.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = strip; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = true; scroll.scrollSensitivity = 24;
            var buttons = new Button[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var existing = content.Find("Entry_"+entries[i].EntryId) ?? page.Find("Entry_"+entries[i].EntryId);
                var b = existing != null ? existing.GetComponent<Button>() : Object.Instantiate(view.EntryButtons[0],content,false);
                b.name = "Entry_"+entries[i].EntryId; b.transform.SetParent(content,false);
                var arrow = b.transform.Find("Arrow");
                if (arrow != null) arrow.gameObject.SetActive(false);
                b.onClick = new Button.ButtonClickedEvent();
                var rect = (RectTransform)b.transform; rect.anchorMin = rect.anchorMax = new Vector2(0,.5f); rect.pivot = new Vector2(.5f,.5f);
                rect.sizeDelta = new Vector2(200,44); rect.anchoredPosition = new Vector2(109+i*218,0);
                b.GetComponentInChildren<Text>(true).text = entries[i].DisplayName; buttons[i] = b;
            }
            // Scroll cue uses the same generated theme border as the buttons.
            var rail = page.Find("EntryScrollHint") as RectTransform ?? Rect(page,"EntryScrollHint",new Vector2(420,6),new Vector2(0,231));
            var image = rail.GetComponent<Image>() ?? rail.gameObject.AddComponent<Image>();
            var theme = view.EntryButtons[0].GetComponent<UiThemeView>();
            image.sprite = theme.DeepSeek.ButtonFrame; image.type = Image.Type.Sliced; image.color = new Color(1,1,1,.65f); image.raycastTarget = false;
            var railTheme = rail.GetComponent<UiThemeView>() ?? rail.gameObject.AddComponent<UiThemeView>();
            railTheme.DeepSeek = theme.DeepSeek; railTheme.Harness = theme.Harness;
            railTheme.Artwork = new[] { new UiThemeView.ArtworkBinding { Target = image, Kind = UiArtworkKind.ButtonFrame } };
            view.Entries = entries; view.EntryButtons = buttons; view.Entry = entries[0];
            foreach (var t in page.GetComponentsInChildren<Text>(true)) if (t.text.StartsWith("收录 ·")) t.text = "收录 · 9种敌人";
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
