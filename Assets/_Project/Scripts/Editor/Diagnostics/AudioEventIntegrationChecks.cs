using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fact = DeepSleep.Runtime.Networking.NetworkMessageCatalog.CombatPresentationKind;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>Actual-route observation plus isolated message replay. No transport/profile/pref writes.</summary>
    public static class AudioEventIntegrationChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameAudioService _captured;
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static readonly List<string> Errors = new List<string>();
        private static int _checks;
        public static string LastReport { get; private set; } = "Not run";

        public static string StartCapture()
        {
            if (!EditorApplication.isPlaying || GameAppRoot.Instance == null || GameAppRoot.Instance.Audio == null || !GameAppRoot.Instance.Audio.IsReady)
                return "Requires Play from installed Boot.";
            if (_captured != null) return "Audio capture is already running.";
            Counts.Clear(); Errors.Clear();
            _captured = GameAppRoot.Instance.Audio;
            _captured.Played += Count;
            Application.logMessageReceived += Log;
            return "Audio capture started. Run existing ChapterFlowLifecycleChecks.Start(), then call Finish after it stops.";
        }

        public static string Finish()
        {
            if (_captured == null) return "No audio capture running.";
            _captured.Played -= Count;
            Application.logMessageReceived -= Log;
            _captured = null;
            LastReport = (Errors.Count == 0 ? "NO ERRORS" : "ERRORS CAPTURED") + ": observed " + Counts.Values.Sum() +
                " accepted audio cues during actual route.\n" + string.Join("\n", Counts.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) +
                (Errors.Count == 0 ? "" : "\n" + string.Join("\n", Errors)) +
                "\nObservation only: accepted playback is not listening, mobile, or live host/client verification.";
            return LastReport;
        }

        private static void Count(AudioCue cue)
        {
            string key = SceneManager.GetActiveScene().name + "/" + cue;
            Counts[key] = Counts.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        private static void Log(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Errors.Add(type + ": " + condition);
        }

        public static string RunSceneBindings()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play for preview-scene binding checks.");
            _checks = 0;
            foreach (string name in new[] { "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var sceneAudio = All<SceneAudioPresenter>(roots);
                    Check(sceneAudio.Length == 1 && sceneAudio[0].WorldCamera != null, name + " scene audio/camera missing.");
                    if (name != "MainMenu")
                    {
                        var combat = All<CombatAudioPresenter>(roots);
                        Check(combat.Length == 1, name + " combat audio presenter count.");
                        Check(combat[0].TryValidateConfiguration(out var reason), name + ": " + reason);
                        Check(combat[0].EnemyPools.Length > 0 && combat[0].EnemyPools.All(p => p != null) &&
                            combat[0].EnemyProjectiles.Length > 0 && combat[0].EnemyProjectiles.All(p => p != null), name + " enemy audio pools missing.");
                        Check((combat[0].Encounter != null) == (name == "World01_EarlyInternet"), name + " encounter binding mismatch.");
                        Check(sceneAudio[0].Session == combat[0].Session && sceneAudio[0].Chapter == combat[0].Chapter &&
                            sceneAudio[0].Node != null, name + " audio presenters disagree on scene owners.");
                    }
                    foreach (var root in roots) CheckControls(root);
                    var panels = All<AudioSettingsPanel>(roots);
                    Check(panels.Length >= 1, name + " missing audio settings.");
                    foreach (var panel in panels)
                    {
                        Check(panel.Panel != null && panel.OpenButton != null && panel.CloseButton != null,
                            name + " settings open/close/group missing.");
                        var sliders = new[] { panel.MasterSlider, panel.SfxSlider, panel.AmbienceSlider };
                        Check(sliders.All(s => s != null) && sliders.Distinct().Count() == 3, name + " distinct sliders missing.");
                        Check(panel.MasterValue != null && panel.SfxValue != null && panel.AmbienceValue != null, name + " volume labels missing.");
                        Check(!panel.OpenButton.GetComponent<UiAudioFeedback>().PlayClick &&
                              !panel.CloseButton.GetComponent<UiAudioFeedback>().PlayClick, name + " settings duplicate generic click enabled.");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            foreach (string name in new[] { "PF_UI_MetaProductCard", "PF_UI_AchievementCard" })
            {
                var prefab = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Meta/" + name + ".prefab");
                try { CheckControls(prefab); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            return _checks + " scene/prefab audio-binding checks passed; no assets or scenes saved.";
        }

        private static T[] All<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void CheckControls(GameObject root)
        {
            foreach (var control in root.GetComponentsInChildren<Selectable>(true))
            {
                if (!(control is Button) && !(control is Slider)) continue;
                var feedback = control.GetComponent<UiAudioFeedback>();
                Check(feedback != null && feedback.Control == control, control.name + " explicit UI audio missing.");
            }
        }

        public static string RunReplicaReplay()
        {
            if (!EditorApplication.isPlaying || GameAppRoot.Instance == null || GameAppRoot.Instance.Audio == null)
                throw new InvalidOperationException("Requires installed Boot Play.");
            if (_captured != null) throw new InvalidOperationException("Finish actual-route capture before isolated replay.");
            _checks = 0;
            var app = GameAppRoot.Instance;
            var appAudio = typeof(GameAppRoot).GetField("_audio", Private);
            var originalAudio = app.Audio;
            var catalog = Object.Instantiate(originalAudio.Catalog);
            catalog.hideFlags = HideFlags.HideAndDontSave;
            var player = new GameObject("AudioMessageReplay_MutedPlayer");
            player.SetActive(false); player.hideFlags = HideFlags.HideAndDontSave;
            var model = new GameObject("AudioMessageReplay_InactiveModel");
            model.SetActive(false); model.hideFlags = HideFlags.HideAndDontSave;
            float timeScale = Time.timeScale;
            try
            {
                foreach (var entry in catalog.Entries) { entry.MinimumInterval = 0; entry.MaximumVoices = 8; }
                var audio = player.AddComponent<GameAudioService>();
                audio.Catalog = catalog;
                audio.Voices = Enumerable.Range(0, 24).Select(_ => MutedSource(player)).ToArray();
                audio.Ambience = Enumerable.Range(0, 2).Select(_ => MutedSource(player)).ToArray();
                Time.timeScale = 1; player.SetActive(true); audio.SetVolumes(1, 1, 1, false);
                appAudio.SetValue(app, audio);
                var played = new List<AudioCue>(); audio.Played += played.Add;
                // The model never activates: no Awake, subscription, socket, gameplay or save lifecycle runs.
                var session = model.AddComponent<CoopSessionController>();
                SetProperty(session, "Phase", SessionPhase.Playing);
                SetProperty(session, "LocalRole", PlayerRole.DeepSeek);
                var chapter = model.AddComponent<ChapterRunController>();
                SetProperty(chapter, "Phase", ChapterRunPhase.Combat);
                var world = model.AddComponent<ChapterCombatWorld2D>();
                var gate = model.AddComponent<RestNodeCombatGate>();
                SetField(gate, "_hasState", true); SetField(gate, "_combatAllowed", true);
                world.Gate = gate; SetField(chapter, "_combatWorld", world);
                var presenter = model.AddComponent<CombatAudioPresenter>();
                presenter.Session = session; presenter.Chapter = chapter;
                Replay(presenter, 7, 1, Fact.ContextStarted);
                Check(played.Count == 0, "Context-start must not replay history.");
                Replay(presenter, 7, 2, Fact.RiceVolley);
                Check(played.SequenceEqual(new[] { AudioCue.DsShot }), "Valid replica fact was not presented once.");
                Replay(presenter, 7, 2, Fact.RiceVolley); Replay(presenter, 7, 1, Fact.RiceVolley);
                Check(played.Count == 1, "Duplicate/old sequence replayed audio.");
                Replay(presenter, 6, 3, Fact.RiceVolley);
                Check(played.Count == 1, "Old context played audio.");
                Replay(presenter, 7, 4, Fact.RiceVolley, role: 2);
                Replay(presenter, 7, 4, Fact.RiceVolley, truncate: true);
                Check(played.Count == 1, "Invalid role/truncated message played audio.");
                SetProperty(chapter, "Phase", ChapterRunPhase.Node);
                Replay(presenter, 7, 5, Fact.RiceVolley);
                SetProperty(chapter, "Phase", ChapterRunPhase.Combat);
                SetField(gate, "_combatAllowed", false); Replay(presenter, 7, 6, Fact.RiceVolley);
                SetField(gate, "_combatAllowed", true);
                Replay(presenter, 7, 7, Fact.ContextStopped); Replay(presenter, 7, 8, Fact.RiceVolley);
                Check(played.Count == 1, "Node, closed combat gate or remote stop allowed audio.");
                Replay(presenter, 8, 9, Fact.ContextStarted);
                SetField(session, "_exiting", true); Replay(presenter, 8, 10, Fact.RiceVolley);
                SetField(session, "_exiting", false);
                SetProperty(session, "Phase", SessionPhase.Offline); Replay(presenter, 8, 11, Fact.RiceVolley);
                SetProperty(session, "Phase", SessionPhase.Playing);
                Check(played.Count == 1, "Exiting/offline model accepted network audio.");

                // Exercise the actual encounter's state/event suppression with 240 simulated
                // clear callbacks, without creating/damaging live gameplay bubbles.
                var encounter = model.AddComponent<DoubaoWordWallEncounter2D>();
                var effects = model.AddComponent<OneShotSpriteEffectPool2D>();
                SetField(encounter, "_impactEffects", effects); SetField(encounter, "_breakEffects", effects);
                int popped = 0, completed = 0;
                encounter.BubblePopped += _ => popped++;
                encounter.Completed += _ => { completed++; Replay(presenter, 8, 20, Fact.BossDefeated); };
                SetProperty(encounter, "State", DoubaoEncounterState.Active);
                Call(encounter, "OnBlockPopped", Vector2.zero, false);
                Check(popped == 1, "Active bubble-pop positive control failed.");
                Call(encounter, "OnBossDefeated", new object[] { null });
                for (int i = 0; i < 240; i++) Call(encounter, "OnBlockPopped", Vector2.zero, false);
                Call(encounter, "OnBossDefeated", new object[] { null });
                Replay(presenter, 8, 20, Fact.BossDefeated);
                Check(completed == 1 && popped == 1, "Boss clear emitted duplicate completion or per-bubble audio facts.");
                Check(played.Count(c => c == AudioCue.DoubaoDefeat) == 1 && !played.Contains(AudioCue.BubblePop),
                    "Boss-clear presentation did not produce exactly one collective cue.");
                return _checks + " isolated replay checks passed: real Read() validator/sequence/context/world gate, " +
                    "plus actual encounter completion guard and 240 simulated clear callbacks. No transport, gameplay damage, profile or preference writes. " +
                    "This is not a live two-device/network delivery test.";
            }
            finally
            {
                appAudio.SetValue(app, originalAudio);
                Object.DestroyImmediate(player); Object.DestroyImmediate(model); Object.DestroyImmediate(catalog);
                Time.timeScale = timeScale;
            }
        }

        private static AudioSource MutedSource(GameObject owner)
        { var source = owner.AddComponent<AudioSource>(); source.playOnAwake = false; source.mute = true; return source; }
        private static void Replay(CombatAudioPresenter presenter, uint context, uint sequence, Fact kind, byte role = 0, bool truncate = false)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                { writer.Write(context); writer.Write(sequence); writer.Write((byte)kind); writer.Write(role); writer.Write(0f); writer.Write(0f); }
                if (truncate) stream.SetLength(stream.Length - 1);
                stream.Position = 0;
                using (var reader = new BinaryReader(stream))
                    Call(presenter, "Read", NetworkMessageCatalog.Authority.CombatPresentation, reader);
            }
        }
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Check(bool condition, string message)
        { _checks++; if (!condition) throw new InvalidOperationException("[AudioEventIntegrationChecks] " + message); }
    }
}
