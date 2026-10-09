using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Presentation.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>配置检查用只读预览场景；播放检查用静音临时实例，不改资产、存档或偏好。</summary>
    public static class AudioPlaybackChecks
    {
        private static int _checks;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run() => EditorApplication.isPlaying ? RunPlayback() : RunConfiguration();

        public static string RunConfiguration()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play for audio asset/Boot checks.");
            _checks = 0;
            var catalog = LoadCatalog();
            Check(catalog.TryValidate(out var reason), reason);
            int clipCount = 0;
            foreach (var entry in catalog.Entries)
            {
                bool ambience = entry.Cue >= AudioCue.AmbienceSky && entry.Cue <= AudioCue.AmbienceRest || entry.Cue >= AudioCue.AmbienceCyber;
                Check(entry.IsUi == (entry.Cue <= AudioCue.UiReject), entry.Cue + " UI policy mismatch.");
                foreach (var clip in entry.Clips.Concat(entry.HarnessClips))
                {
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                    Check(importer != null, entry.Cue + " missing AudioImporter.");
                    Check(clip.length > 0 && clip.frequency > 0, entry.Cue + " invalid clip metadata.");
                    Check(ambience ? clip.channels >= 1 && clip.channels <= 2 : clip.channels == 1, entry.Cue + " channel count mismatch.");
                    Check(ambience
                        ? importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad
                        : importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad,
                        entry.Cue + " short/long load policy mismatch.");
                    clipCount++;
                }
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Boot.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                var services = roots.SelectMany(r => r.GetComponentsInChildren<GameAudioService>(true)).ToArray();
                var apps = roots.SelectMany(r => r.GetComponentsInChildren<GameAppRoot>(true)).ToArray();
                Check(services.Length == 1 && apps.Length == 1, "Boot requires one explicit audio service and app root.");
                var audio = services[0];
                Check(apps[0].Audio == audio && audio.Catalog == catalog, "Boot audio/catalog references mismatch.");
                Check(audio.Voices.Length == GameAudioService.ShortVoiceCount && audio.Ambience.Length == 2,
                    "Boot voice counts mismatch.");
                var sources = audio.Voices.Concat(audio.Ambience).ToArray();
                Check(sources.All(s => s != null) && sources.Distinct().Count() == 26, "Sources must be explicit and unique.");
                foreach (var source in sources)
                    Check(!source.playOnAwake && source.spatialBlend == 0f, "Sources require silent startup / controlled stereo pan.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return _checks + " audio configuration checks passed; " + clipCount +
                " clip bindings checked. Preview scene closed; no scene/assets/preferences saved.";
        }

        public static string RunPlayback()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play before muted audio playback checks.");
            _checks = 0;
            var original = LoadCatalog();
            var catalog = Object.Instantiate(original);
            catalog.hideFlags = HideFlags.HideAndDontSave;
            var fixture = new GameObject("AudioPlaybackChecks_Temporary");
            fixture.SetActive(false);
            fixture.hideFlags = HideFlags.HideAndDontSave;
            float previousTimeScale = Time.timeScale;
            string[] keys = { "DeepSleep.Audio.Master", "DeepSleep.Audio.Sfx", "DeepSleep.Audio.Ambience" };
            bool[] hadKeys = keys.Select(PlayerPrefs.HasKey).ToArray();
            float[] values = keys.Select(k => PlayerPrefs.GetFloat(k)).ToArray();
            try
            {
                // ScriptableObject Instantiate copies the serialized definitions; mutations stay in this fixture.
                foreach (var entry in catalog.Entries)
                { entry.MinimumInterval = 0; entry.MaximumVoices = 8; entry.Importance = 30; }
                Entry(catalog, AudioCue.PlayerDown).Importance = 90;
                var audio = fixture.AddComponent<GameAudioService>();
                audio.Catalog = catalog;
                audio.Voices = Enumerable.Range(0, 24).Select(_ => AddMutedSource(fixture)).ToArray();
                audio.Ambience = Enumerable.Range(0, 2).Select(_ => AddMutedSource(fixture)).ToArray();
                Time.timeScale = 1f;
                fixture.SetActive(true);
                Check(audio.IsReady, "Fixture did not initialize.");
                audio.SetVolumes(1, 1, 1, false);

                var rate = Entry(catalog, AudioCue.DsShot);
                rate.MinimumInterval = 1f;
                Check(audio.Play(AudioCue.DsShot, sourceId: 1), "First shot rejected.");
                Check(!audio.Play(AudioCue.DsShot, sourceId: 1), "Same-source interval not enforced.");
                Check(!audio.Play(AudioCue.DsShot, sourceId: 2), "Global cue interval not enforced.");
                Check(audio.SuppressedCount == 2, "Suppression accounting mismatch.");
                audio.StopWorld(); rate.MinimumInterval = 0;

                rate.MaximumVoices = 2;
                Check(audio.StartLoop(AudioCue.DsShot, sourceId: 1) != 0, "First bounded voice failed.");
                Check(audio.StartLoop(AudioCue.DsShot, sourceId: 2) != 0, "Second bounded voice failed.");
                Check(audio.StartLoop(AudioCue.DsShot, sourceId: 3) == 0, "Per-cue voice cap failed.");
                audio.StopWorld(); rate.MaximumVoices = 8;
                foreach (var cue in new[] { AudioCue.DsShot, AudioCue.DsHit, AudioCue.DsSplash })
                    for (int i = 0; i < (cue == AudioCue.DsSplash ? 4 : 8); i++)
                        Check(audio.StartLoop(cue, sourceId: i + 1) != 0, "Could not fill ordinary pool.");
                Check(Active(audio) == 20, "Ordinary pool should stop at 20 voices.");
                Check(!audio.Play(AudioCue.HsFire), "Ordinary cue used a reserved voice.");
                for (int i = 0; i < 4; i++)
                    Check(audio.StartLoop(AudioCue.PlayerDown, sourceId: i + 1) != 0, "Reserved danger voice missing.");
                Check(Active(audio) == 24 && audio.PeakActiveVoices == 24, "24-voice total cap mismatch.");
                Check(audio.StartLoop(AudioCue.PlayerDown, sourceId: 9) == 0, "Pool exceeded hard cap.");
                audio.StopWorld(); Check(Active(audio) == 0, "StopWorld left world voices alive.");

                int handle = audio.StartLoop(AudioCue.HsCharge, sourceId: 31);
                audio.StopLoop(handle, .2f); Tick(audio, .1f);
                float envelope = Get<float>(VoiceWithHandle(audio, handle), "Envelope");
                Check(Mathf.Approximately(envelope, .5f), "Stop fade did not reach half level.");
                Check(audio.StartLoop(AudioCue.HsCharge, sourceId: 31) == handle, "Fade cancellation replaced handle.");
                Check(Mathf.Approximately(Get<float>(VoiceWithHandle(audio, handle), "Envelope"), envelope), "Restart jumped fade volume.");
                Tick(audio, .05f);
                Check(VoiceWithHandle(audio, handle) != null &&
                      Mathf.Approximately(Get<float>(VoiceWithHandle(audio, handle), "Envelope"), 1), "Resumed loop faded out later.");
                audio.SetWorldPaused(true);
                Check(Get<bool>(VoiceWithHandle(audio, handle), "Paused"), "World loop not paused immediately.");
                Check(!audio.Play(AudioCue.DsShot), "Paused world accepted new shot.");
                Check(audio.Play(AudioCue.UiConfirm), "UI blocked by world pause.");
                audio.SetWorldPaused(false);
                Check(!Get<bool>(VoiceWithHandle(audio, handle), "Paused"), "World loop not resumed.");
                Time.timeScale = 0; Tick(audio, 0);
                Check(Get<bool>(VoiceWithHandle(audio, handle), "Paused") && !audio.Play(AudioCue.DsShot), "Time-scale pause failed.");
                Check(audio.Play(AudioCue.UiCancel), "UI blocked by timeScale zero.");
                Time.timeScale = 1; Tick(audio, 0);

                audio.SetVolumes(0, 1, 1, false);
                Check(audio.Voices.All(s => s.clip == null || s.volume == 0), "Master zero not applied immediately.");
                audio.SetVolumes(1, 0, 1, false);
                Check(audio.Voices.All(s => s.clip == null || s.volume == 0), "SFX zero not applied immediately.");
                audio.SetVolumes(1, 1, 1, false);

                Entry(catalog, AudioCue.AmbienceSky).Gain = .2f;
                Entry(catalog, AudioCue.AmbienceDusk).Gain = .8f;
                audio.SetAmbience(AudioCue.AmbienceSky); Tick(audio, 1);
                var sky = audio.Ambience.Single(s => s.clip == Entry(catalog, AudioCue.AmbienceSky).Clips[0]);
                Check(Mathf.Approximately(sky.volume, .2f), "Sky own gain not applied.");
                audio.SetAmbience(AudioCue.AmbienceDusk); Tick(audio, .25f);
                var dusk = audio.Ambience.Single(s => s.clip == Entry(catalog, AudioCue.AmbienceDusk).Clips[0]);
                Check(Mathf.Approximately(sky.volume, .15f) && Mathf.Approximately(dusk.volume, .2f), "Outgoing source used incoming gain.");
                float skyVolume = sky.volume, duskVolume = dusk.volume;
                int skySample = sky.timeSamples;
                audio.SetAmbience(AudioCue.AmbienceSky);
                Check(Mathf.Approximately(sky.volume, skyVolume) && Mathf.Approximately(dusk.volume, duskVolume), "Rapid return reset crossfade volumes.");
                Check(sky.timeSamples >= skySample, "Rapid return restarted old ambience.");
                Tick(audio, .25f);
                Check(Mathf.Approximately(sky.volume, .2f) && dusk.clip == null, "Reverse crossfade did not complete.");
                foreach (var cue in new[] { AudioCue.AmbienceCyber, AudioCue.AmbienceRain, AudioCue.AmbienceArcade })
                {
                    audio.SetAmbience(cue); Tick(audio, 1);
                    Check(audio.Ambience.Any(s => s.clip == Entry(catalog, cue).Clips[0] && s.loop),
                        cue + " was filtered out or did not loop.");
                }
                audio.SetAmbience(AudioCue.ClaudeHit);
                Check(audio.Ambience.Any(s => s.clip == Entry(catalog, AudioCue.AmbienceArcade).Clips[0]),
                    "Non-ambient cue replaced ambience.");
                audio.SetVolumes(1, 1, 0, false);
                Check(audio.Ambience.All(s => s.volume == 0), "Ambience zero not immediate.");

                Call(audio, "SetBackground", true);
                Check(Get<bool>(VoiceWithHandle(audio, handle), "Paused"), "Background loop pause was deferred.");
                Check(Voices(audio).Where(v => Get<int>(v, "Handle") != 0).All(v => Get<AudioSource>(v, "Source").loop),
                    "Background kept stale one-shots.");
                Check(!audio.Play(AudioCue.UiConfirm), "Background accepted new UI cue.");
                Call(audio, "SetBackground", false);
                Check(!Get<bool>(VoiceWithHandle(audio, handle), "Paused"), "Foreground loop failed to resume.");
                Set(audio, "_focusLost", true);
                Call(audio, "OnApplicationPause", true); Call(audio, "OnApplicationPause", false);
                Check(Get<bool>(audio, "_backgrounded"), "Pause=false incorrectly overrode lost focus.");
                Set(audio, "_focusLost", false); Call(audio, "OnApplicationPause", false);
                Check(!Get<bool>(audio, "_backgrounded"), "Foreground state did not clear.");

                audio.BindScene(fixture, null);
                audio.SetAmbience(AudioCue.AmbienceRest); Tick(audio, 1);
                audio.StartLoop(AudioCue.HsCharge, sourceId: 41);
                audio.ReleaseScene(fixture);
                Check(Active(audio) == 0 && audio.Ambience.All(s => s.clip == null), "Scene release left world/ambient clips.");
                audio.SetAmbience(AudioCue.AmbienceRest); Tick(audio, 1);
                fixture.SetActive(false);
                Check(audio.Voices.Concat(audio.Ambience).All(s => s.clip == null), "Disable left clips or ambient state.");
                fixture.SetActive(true); audio.SetAmbience(AudioCue.AmbienceRest);
                Check(audio.Ambience.Any(s => s.clip != null), "Re-enable could not restart same ambience.");

                return _checks + " muted audio playback checks passed: caps, interval, mute, pause, background/focus, fades and cleanup. " +
                    "Temporary clone only; no preferences/profile/scene writes; not perceptual or mobile validation.";
            }
            finally
            {
                Object.DestroyImmediate(fixture); Object.DestroyImmediate(catalog);
                Time.timeScale = previousTimeScale;
                for (int i = 0; i < keys.Length; i++)
                    if (PlayerPrefs.HasKey(keys[i]) != hadKeys[i] || PlayerPrefs.GetFloat(keys[i]) != values[i])
                        throw new InvalidOperationException("Audio checks unexpectedly changed preference " + keys[i]);
            }
        }

        private static GameAudioCatalog LoadCatalog()
        {
            var paths = AssetDatabase.FindAssets("t:GameAudioCatalog").Select(AssetDatabase.GUIDToAssetPath).ToArray();
            if (paths.Length != 1) throw new InvalidOperationException("Expected one installed GameAudioCatalog, found " + paths.Length);
            return AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(paths[0]);
        }

        private static AudioSource AddMutedSource(GameObject owner)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.mute = true;
            return source;
        }

        private static AudioCueDefinition Entry(GameAudioCatalog catalog, AudioCue cue) => catalog.Entries.Single(e => e.Cue == cue);
        private static object[] Voices(GameAudioService service) => ((IEnumerable)Get<object>(service, "_voices")).Cast<object>().ToArray();
        private static int Active(GameAudioService service) => Voices(service).Count(v => Get<int>(v, "Handle") != 0);
        private static object VoiceWithHandle(GameAudioService service, int handle) => Voices(service).FirstOrDefault(v => Get<int>(v, "Handle") == handle);
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);
        private static void Tick(GameAudioService service, float seconds) => Call(service, "UpdatePlayback", seconds);
        private static void Check(bool condition, string message)
        { _checks++; if (!condition) throw new InvalidOperationException("[AudioPlaybackChecks] " + message); }
    }
}
