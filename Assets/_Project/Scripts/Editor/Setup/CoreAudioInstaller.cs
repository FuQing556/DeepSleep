using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Progression.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    /// <summary>一次性编辑器装配：原生 Mixer/AudioClip/场景引用。运行时不搜索或补造组件。</summary>
    public static class CoreAudioInstaller
    {
        private const string AudioRoot = "Assets/_Project/Audio";
        private const string CatalogPath = AudioRoot + "/CFG_GameAudio.asset";
        private const string MixerPath = AudioRoot + "/DeepSleep.mixer";

        public static string InstallAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene changes first.");
            ImportClips();
            var catalog = CreateCatalog(CreateMixer());
            var original = SceneManager.GetActiveScene();
            foreach (string path in new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/MainMenu.unity",
                         "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    if (scene.name == "Boot") InstallBoot(scene, catalog);
                    else { InstallScene(scene); AudioUiInstaller.InstallInActiveScene(); }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (original.isLoaded) SceneManager.SetActiveScene(original);
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            AudioUiInstaller.InstallPrefabs();
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            Networking.NetworkBuildRevision.Apply(network); EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            return "Core audio installed: catalog, native Mixer, Boot 24+2 voices, MainMenu and both gameplay scenes, volume UI, protocol 5.";
        }

        private static void ImportClips()
        {
            foreach (string path in Directory.GetFiles(AudioRoot, "*.wav", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                AssetDatabase.ImportAsset(assetPath);
                var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
                bool ambient = Path.GetFileName(path).StartsWith("AMB_");
                importer.forceToMono = !ambient;
                importer.loadInBackground = ambient;
                var settings = importer.defaultSampleSettings;
                settings.loadType = ambient ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = ambient ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = .7f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = !ambient;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static AudioMixer CreateMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null) return mixer;
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;
            mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", flags | BindingFlags.Static)
                .Invoke(null, new object[] { MixerPath });
            object master = type.GetProperty("masterGroup", flags | BindingFlags.Instance).GetValue(mixer);
            foreach (string name in new[] { "UI", "Player", "Enemy", "World", "Ambience" })
            {
                object group = type.GetMethod("CreateNewGroup", flags | BindingFlags.Instance)
                    .Invoke(mixer, new object[] { name, false });
                type.GetMethod("AddChildToParent", flags | BindingFlags.Instance).Invoke(mixer, new[] { group, master });
            }
            EditorUtility.SetDirty(mixer); AssetDatabase.SaveAssetIfDirty(mixer);
            return mixer;
        }

        private static GameAudioCatalog CreateCatalog(AudioMixer mixer)
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<GameAudioCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Entries = Enum.GetValues(typeof(AudioCue)).Cast<AudioCue>().Select(cue =>
            {
                bool ui = cue <= AudioCue.UiReject;
                bool ambient = cue >= AudioCue.AmbienceSky;
                bool enemy = cue >= AudioCue.BubblePop && cue <= AudioCue.SnakeShot;
                bool flow = cue >= AudioCue.NodeOpen && !ambient;
                string prefix = (ambient ? "AMB_" : "SFX_") + cue + "_";
                var entry = new AudioCueDefinition
                {
                    Cue = cue,
                    Clips = clips.Where(c => c.name.StartsWith(prefix + (ui ? "DS_" : ""))).OrderBy(c => c.name).ToArray(),
                    HarnessClips = ui ? clips.Where(c => c.name.StartsWith(prefix + "HS_")).OrderBy(c => c.name).ToArray() : Array.Empty<AudioClip>(),
                    Output = mixer.FindMatchingGroups(ui ? "UI" : ambient ? "Ambience" : enemy ? "Enemy" : flow ? "World" : "Player").Single(),
                    Gain = ambient ? .4f : ui ? .46f : .42f,
                    IsUi = ui,
                    PauseWithWorld = !ui && !flow && !ambient,
                    MinimumInterval = ui ? .07f : .06f,
                    MaximumVoices = 3,
                    Importance = ui ? 65 : enemy ? 20 : flow ? 70 : 40,
                    PitchVariation = ui || ambient ? 0 : .012f
                };
                if (cue == AudioCue.UiFocus) { entry.Gain = .2f; entry.MinimumInterval = .1f; entry.Importance = 15; }
                if (cue == AudioCue.DsShot) { entry.Gain = .3f; entry.MinimumInterval = .05f; }
                if (cue == AudioCue.DsHit || cue == AudioCue.HsHit) { entry.Gain = .24f; entry.MinimumInterval = .075f; }
                if (cue == AudioCue.BubblePop) { entry.Gain = .28f; entry.MinimumInterval = .07f; entry.MaximumVoices = 3; }
                if (cue == AudioCue.EnemyDefeat || cue == AudioCue.SnakeShot) { entry.Gain = .25f; entry.MinimumInterval = .12f; entry.MaximumVoices = 2; }
                if (cue == AudioCue.DsSplash || cue == AudioCue.HsWave) entry.Gain = .33f;
                if (cue == AudioCue.PlayerHurtDs || cue == AudioCue.PlayerHurtHs || cue == AudioCue.GuardBlock ||
                    cue == AudioCue.PlayerDown || cue == AudioCue.ReviveDone || cue == AudioCue.Victory || cue == AudioCue.Defeat)
                { entry.Importance = 90; entry.MaximumVoices = 2; entry.MinimumInterval = .1f; entry.Gain = .56f; }
                if (cue == AudioCue.HsCharge || cue == AudioCue.ReviveStart) { entry.Gain = .32f; entry.MaximumVoices = 2; }
                return entry;
            }).ToArray();
            if (!catalog.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }

        private static void InstallBoot(Scene scene, GameAudioCatalog catalog)
        {
            var root = All<GameAppRoot>(scene).Single();
            var audio = Get<GameAudioService>(root.gameObject);
            audio.Catalog = catalog;
            audio.Voices = Enumerable.Range(0, GameAudioService.ShortVoiceCount).Select(i => Source(root.transform, "AudioVoice_" + i.ToString("00"))).ToArray();
            audio.Ambience = Enumerable.Range(0, 2).Select(i => Source(root.transform, "Ambience_" + i)).ToArray();
            var so = new SerializedObject(root); so.FindProperty("_audio").objectReferenceValue = audio; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audio);
        }

        private static void InstallScene(Scene scene)
        {
            var camera = All<Camera>(scene).Single(c => c.enabled);
            var sceneAudio = Get<SceneAudioPresenter>(camera.gameObject);
            sceneAudio.WorldCamera = camera;
            sceneAudio.CombatAmbience = scene.name == "World01_EarlyInternet" ? AudioCue.AmbienceDusk : AudioCue.AmbienceSky;
            var bindings = All<LevelSceneBindings>(scene).SingleOrDefault();
            if (bindings == null) { EditorUtility.SetDirty(sceneAudio); return; }
            sceneAudio.Chapter = bindings.ChapterRun; sceneAudio.Node = bindings.RestNode; sceneAudio.Session = bindings.Session;
            var combat = Get<CombatAudioPresenter>(bindings.gameObject);
            combat.Session = bindings.Session; combat.Chapter = bindings.ChapterRun;
            combat.Shooter = All<DeepSeekRiceAutoShooter>(scene).Single(); combat.Rice = All<RiceProjectilePool>(scene).Single();
            combat.Laser = All<HarnessTerminalLaserController>(scene).Single(); combat.LaserDamage = All<HarnessTerminalLaserDamageExecutor2D>(scene).Single();
            combat.Melee = All<HarnessMeleeController>(scene).Single(); combat.MeleeDamage = All<HarnessMeleeDamageExecutor2D>(scene).Single();
            combat.Guard = All<DeepSeekRiceGuardController>(scene).Single();
            var ds = All<PlayerActor>(scene).Single(a => a.Definition.Role == PlayerRole.DeepSeek);
            var hs = All<PlayerActor>(scene).Single(a => a.Definition.Role == PlayerRole.Harness);
            combat.DeepSeekDamage = ds.GetComponent<PlayerDamageReceiver2D>(); combat.HarnessDamage = hs.GetComponent<PlayerDamageReceiver2D>();
            combat.DeepSeekLife = ds.GetComponent<PlayerLifeStateController2D>(); combat.HarnessLife = hs.GetComponent<PlayerLifeStateController2D>();
            combat.DeepSeekRevive = ds.GetComponent<PlayerReviveCoordinator2D>(); combat.HarnessRevive = hs.GetComponent<PlayerReviveCoordinator2D>();
            combat.DeepSeekReviveChannel = ds.GetComponent<PlayerReviveActionChannel>(); combat.HarnessReviveChannel = hs.GetComponent<PlayerReviveActionChannel>();
            combat.CombatFeedback = All<NetworkCombatFeedbackChannel>(scene).Single(); combat.PlayerFeedback = All<NetworkPlayerHitFeedbackChannel>(scene).Single();
            combat.Encounter = All<DoubaoWordWallEncounter2D>(scene).SingleOrDefault();
            combat.EnemyPools = bindings.Enemies.Select(e => e.Pool).Distinct().ToArray();
            combat.EnemyProjectiles = bindings.Enemies.SelectMany(e => e.ProjectilePools).Distinct().ToArray();
            if (!combat.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(sceneAudio); EditorUtility.SetDirty(combat);
        }

        private static AudioSource Source(Transform root, string name)
        {
            var child = root.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(root, false); }
            var source = Get<AudioSource>(child.gameObject);
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.dopplerLevel = 0;
            source.volume = 0; source.priority = 128;
            return source;
        }
        private static T Get<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
