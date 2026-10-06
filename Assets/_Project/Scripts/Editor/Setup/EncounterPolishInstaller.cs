using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Enemies.Presentation;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    public static class EncounterPolishInstaller
    {
        private const string Root = "Assets/_Project/";
        public static string Install()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Resolve Play/unsaved scene first.");
            foreach (string path in Directory.GetFiles(Root + "Art/VFX/InternetEnemies", "*.png"))
            {
                string p = path.Replace('\\','/'); AssetDatabase.ImportAsset(p);
                var imp = (TextureImporter)AssetImporter.GetAtPath(p);
                imp.textureType = TextureImporterType.Sprite; imp.spritePixelsPerUnit = 512;
                imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            InstallEffects("Download", "Download"); InstallEffects("SecurityGuard", "Guard");
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(Root + "Audio/CFG_GameAudio.asset");
            var entries = catalog.Entries.ToList();
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Root + "Audio/DeepSleep.mixer");
            var output = mixer.FindMatchingGroups("Enemy").Single();
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                if (cue < AudioCue.KimiReveal || cue > AudioCue.GuardImpact || entries.Any(e => e.Cue == cue)) continue;
                var paths = Directory.GetFiles(Root + "Audio/SFX/Encounters", "SFX_" + cue + "_*.wav").OrderBy(p=>p).ToArray();
                if (paths.Length == 0) throw new InvalidOperationException("Missing audio " + cue);
                var clips = paths.Select(path => {
                    string p = path.Replace('\\','/'); AssetDatabase.ImportAsset(p);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(p); importer.forceToMono = true;
                    var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM; settings.preloadAudioData = true;
                    importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                }).ToArray();
                bool warn = cue == AudioCue.KimiMoonWarn || cue == AudioCue.KimiLaserCharge || cue == AudioCue.KimiFlute || cue == AudioCue.DownloadCharge;
                entries.Add(new AudioCueDefinition { Cue = cue, Clips = clips, Output = output, Gain = warn ? .5f : .35f,
                    MinimumInterval = .08f, MaximumVoices = warn ? 1 : 3, Importance = warn ? 85 : 45, PitchVariation = .006f });
            }
            catalog.Entries = entries.ToArray();
            if (!catalog.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(catalog);
            foreach (var path in new[] { "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var audio = all.OfType<CombatAudioPresenter>().Single();
                var driver = all.OfType<KimiChapterEncounterDriver2D>().SingleOrDefault();
                audio.Kimi = driver == null ? null : driver.Encounter;
                foreach (var pool in all.OfType<EnemyActorPool2D>())
                {
                    var prefab = (EnemyActor2D)new SerializedObject(pool).FindProperty("_enemyPrefab").objectReferenceValue;
                    bool download = prefab.GetComponent<DownloadChargeMotor2D>() != null;
                    if (!download && prefab.GetComponent<SecurityGuardFacing2D>() == null) continue;
                    var relay = pool.GetComponent<EnemyContentAudio2D>();
                    if (relay == null) relay = pool.gameObject.AddComponent<EnemyContentAudio2D>();
                    relay.Pool = pool; relay.Audio = audio; relay.Download = download;
                }
                foreach (var numbers in all.OfType<CombatDamageNumberPresenter2D>())
                { Set(numbers,"_prewarmCount",64); Set(numbers,"_maximumCount",96); }
                EditorUtility.SetDirty(audio); PrefabUtility.RecordPrefabInstancePropertyModifications(audio);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            var network = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>(Root + "Configs/Networking/CFG_Network.asset");
            Networking.NetworkBuildRevision.Apply(network); EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            return "Dedicated enemy effects, 22 content cues/27 clips, Kimi/enemy audio wiring and 64/96 damage-number pools saved.";
        }
        private static void InstallEffects(string prefabName, string artName)
        {
            string root = Root + "Prefabs/Combat/Enemies/Internet/";
            string path = root + "PF_EnemyRuntime_" + prefabName + ".prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var presenter = go.GetComponentInChildren<EnemyDespawnEffectPresenter2D>(true);
                var so = new SerializedObject(presenter);
                foreach (bool death in new[] { true, false })
                {
                    var pool = (OneShotSpriteEffectPool2D)so.FindProperty(death ? "_deathEffectPool" : "_contactImpactEffectPool").objectReferenceValue;
                    var source = (OneShotSpriteEffect2D)new SerializedObject(pool).FindProperty("_effectPrefab").objectReferenceValue;
                    string kind = death ? "Death" : "Impact";
                    var fx = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
                    try
                    {
                        fx.GetComponentInChildren<SpriteRenderer>(true).sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/VFX/InternetEnemies/VFX_" + artName + "_" + kind + "_v01.png");
                        var asset = PrefabUtility.SaveAsPrefabAsset(fx, root + "PF_" + kind + "_" + prefabName + ".prefab");
                        Set(pool,"_effectPrefab",asset.GetComponent<OneShotSpriteEffect2D>());
                    }
                    finally { PrefabUtility.UnloadPrefabContents(fx); }
                    string configPath = Root + "Configs/Combat/Enemies/Internet/CFG_" + prefabName + "_" + kind + ".asset";
                    var config = AssetDatabase.LoadAssetAtPath<OneShotSpriteEffectConfig>(configPath);
                    if (config == null) { config = ScriptableObject.CreateInstance<OneShotSpriteEffectConfig>(); AssetDatabase.CreateAsset(config,configPath); }
                    Set(config,"_durationSeconds",death ? .38f : .2f); Set(config,"_worldDiameter",death ? 2.5f : .8f);
                    Set(config,"_startScaleMultiplier",death ? .8f : .6f); Set(config,"_endScaleMultiplier",1.2f);
                    Set(config,"_fadeStart01",.12f); Set(pool,"_config",config);
                }
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        private static void Set(Object owner, string field, Object value)
        { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object owner, string field, float value)
        { var so = new SerializedObject(owner); var p = so.FindProperty(field); if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
