using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>隔离检查加载后缺失绑定时的单人/联机开战门；不打开真实房间或修改正式场景。</summary>
    public static class LevelStartGateChecks
    {
        private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("DeepSleep/验证/关卡开战门禁回归")]
        private static void Menu() => Debug.Log(Run());

        /// <summary>只在 Edit Mode 的临时预览场景中执行；真实主客入关仍由场景生命周期回归覆盖。</summary>
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run level start gate checks in Edit Mode.");
            float previousTimeScale = Time.timeScale;
            bool previousBackground = Application.runInBackground;
            Scene preview = EditorSceneManager.NewPreviewScene();
            NetworkTuningConfig config = null;
            int checks = 0;
            try
            {
                var root = new GameObject("LevelStartGateChecks");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                var selection = root.AddComponent<OpeningCharacterSelectionController>();
                var session = root.AddComponent<CoopSessionController>();
                var transport = new CaptureTransport();
                config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
                config.MaximumMessageBytes = 128;
                session.Config = config;
                session.Selection = selection;
                Set(session, "_transport", transport);
                Set(session, "_previousBackground", previousBackground);

                void Check(bool value, string message)
                {
                    if (!value) throw new InvalidOperationException("Level start gate: " + message);
                    checks++;
                }

                Time.timeScale = 0f;
                Check(LevelIdentityValidation.TryValidateLevelId(new string('a', 128), out _) &&
                    !LevelIdentityValidation.TryValidateLevelId(new string('a', 129), out _),
                    "Level identity ignored the UTF-8 wire boundary.");
                Check(LevelIdentityValidation.TryValidateLevelId(new string('界', 42) + "ab", out _) &&
                    !LevelIdentityValidation.TryValidateLevelId("bad\uD800id", out _),
                    "Level identity confused character count with bytes or replaced malformed UTF-16.");
                Check(!selection.TryValidateLevelStart(out string reason) && reason.Contains("_chapterRun"),
                    "Missing chapter binding was accepted or lacked an actionable reason.");
                Check(!selection.IsSelectionComplete && Time.timeScale == 0f,
                    "Validation committed selection or released its pause.");

                Check(!session.Create(PlayerRole.DeepSeek) && transport.HostStarts == 0 &&
                    session.Phase == SessionPhase.Offline && Time.timeScale == 0f,
                    "Invalid host started a transport or entered a room.");
                Time.timeScale = 1f;
                Check(!session.Join("unused.invalid") && transport.ClientStarts == 0 && Time.timeScale == 0f,
                    "Invalid client started a transport or left simulation running.");

                transport.Server = true;
                SetPhase(session, SessionPhase.Lobby);
                session.SetReady(true);
                Check(!session.HostReady && transport.Sends == 0, "Invalid host submitted readiness.");
                Set(session, "_hasPeer", true);
                Set(session, "_hostReady", true);
                Set(session, "_guestReady", true);
                Invoke(session, "TryStart");
                Check(session.Phase == SessionPhase.Lobby && transport.Sends == 0 && Time.timeScale == 0f,
                    "Host ready/start path bypassed the gate.");
                int hostStops = transport.Stops;
                Invoke(session, "OnConnected", (ulong)1);
                Check(session.Phase == SessionPhase.Disconnected && transport.Stops == hostStops + 1 &&
                    transport.Sends == 0 && Time.timeScale == 0f,
                    "Host sent Welcome or left a room open after its level binding became invalid.");

                transport.Server = false;
                foreach (bool playing in new[] { false, true })
                {
                    SetPhase(session, SessionPhase.Connecting);
                    int stops = transport.Stops;
                    Invoke(session, "Receive", (ulong)0, Welcome(playing));
                    Check(session.Phase == SessionPhase.Disconnected && transport.Stops == stops + 1 &&
                        Time.timeScale == 0f && !selection.IsSelectionComplete &&
                        session.Status.Contains("level binding"),
                        "Client welcome/reconnect bypassed validation or lost its rejection reason.");
                }

                SetPhase(session, SessionPhase.Lobby);
                int previousStops = transport.Stops;
                Invoke(session, "Receive", (ulong)0, new[] { NetworkMessageCatalog.Authority.Start });
                Check(session.Phase == SessionPhase.Disconnected && transport.Stops == previousStops + 1 &&
                    Time.timeScale == 0f && session.Status.Contains("level binding"),
                    "Client Start packet bypassed validation or retained a live invalid connection.");
                string rejection = session.Status;
                Invoke(session, "OnDisconnected", (ulong)0);
                Check(session.Status == rejection && Time.timeScale == 0f,
                    "Delayed disconnect callback replaced the actionable level rejection.");

                session.Leave();
                Check(session.Phase == SessionPhase.Offline && Time.timeScale == 0f,
                    "Leave after rejection either failed or restarted an unselected invalid level.");
                return $"Level start gate: {checks} checks passed; no real transport or saved assets used.";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (config != null) Object.DestroyImmediate(config);
                Time.timeScale = previousTimeScale;
                Application.runInBackground = previousBackground;
            }
        }

        /// <summary>在已迁移场景的隔离副本验证同协议、不同关卡被拒绝；不保存资产或开启真实传输。</summary>
        public static string RunWrongLevelScenePath(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run level identity handshake checks in Edit Mode.");
            float previousTimeScale = Time.timeScale;
            bool previousBackground = Application.runInBackground;
            Scene preview = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var bindings = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
                var chapter = bindings.ChapterRun;
                var selection = (OpeningCharacterSelectionController)chapter.GetType()
                    .GetField("_selection", PRIVATE_INSTANCE).GetValue(chapter);
                if (chapter.GetType().GetField("_profile", PRIVATE_INSTANCE).GetValue(chapter) == null)
                {
                    var profileRoot = new GameObject("PreviewOnlyLevelProfile");
                    profileRoot.SetActive(false);
                    SceneManager.MoveGameObjectToScene(profileRoot, preview);
                    Set(chapter, "_profile", profileRoot.AddComponent<LocalPlayerProfileStore>());
                }
                var session = bindings.Session;
                if (!chapter.TryValidateConfiguration(out string reason))
                    throw new InvalidOperationException("Migrated scene static validation is invalid: " + reason);
                if (!TryReadSessionLevelId(session, out string localId))
                    throw new InvalidOperationException("Migrated session identity is invalid: " + session.Status);
                var transport = new CaptureTransport();
                Set(session, "_transport", transport);
                Set(session, "_previousBackground", previousBackground);
                string remoteId = localId == "different_level" ? "alternate_level" : "different_level";
                int checks = 0;
                // 破坏 UI 私有的章节接线应使选角失败，但不应阻止 Session 读取自己的已绑定关卡身份。
                Set(selection, "_chapterRun", null);
                bool identityWithoutUi = TryReadSessionLevelId(session, out string independentId);
                bool selectionRejected = !selection.TryValidateLevelStart(out _);
                Set(selection, "_chapterRun", chapter);
                if (!identityWithoutUi || independentId != localId || !selectionRejected)
                    throw new InvalidOperationException("Session identity still depends on the selection UI's chapter wiring.");
                checks++;
                foreach (bool playing in new[] { false, true })
                {
                    SetPhase(session, SessionPhase.Connecting);
                    Time.timeScale = 0f;
                    int stops = transport.Stops;
                    Invoke(session, "Receive", (ulong)0, Welcome(playing, remoteId));
                    if (session.Phase != SessionPhase.Disconnected || transport.Stops != stops + 1 ||
                        Time.timeScale != 0f || selection.IsSelectionComplete ||
                        !session.Status.Contains(localId) || !session.Status.Contains(remoteId))
                        throw new InvalidOperationException("Wrong-level Welcome/reconnect selected a role, started simulation, or hid the two level IDs.");
                    checks++;
                }
                SetPhase(session, SessionPhase.Offline);
                if (session.Create(PlayerRole.DeepSeek) || transport.HostStarts != 0 ||
                    Time.timeScale != 0f || !session.Status.Contains("ITransportLevelScope"))
                    throw new InvalidOperationException("A transport without level scope started a host.");
                checks++;
                if (session.Join("unused.invalid") || transport.ClientStarts != 0 ||
                    Time.timeScale != 0f || !session.Status.Contains("ITransportLevelScope"))
                    throw new InvalidOperationException("A transport without level scope started a client.");
                checks++;

                var scoped = new ScopedCaptureTransport { AcceptScope = false, StartSucceeds = false };
                Set(session, "_transport", scoped);
                if (session.Create(PlayerRole.DeepSeek) || scoped.ScopeCalls != 1 || scoped.HostStarts != 0 ||
                    scoped.RequestedLevelId != localId || Time.timeScale != 0f)
                    throw new InvalidOperationException("Host ignored transport scope rejection or prepared the wrong level ID.");
                checks++;
                if (session.Join("unused.invalid") || scoped.ScopeCalls != 2 || scoped.ClientStarts != 0 ||
                    Time.timeScale != 0f)
                    throw new InvalidOperationException("Client ignored transport scope rejection.");
                checks++;

                scoped.AcceptScope = true;
                if (session.Create(PlayerRole.DeepSeek) || scoped.HostStarts != 1 ||
                    !scoped.ScopeWasReadyBeforeStart || scoped.PreparedLevelId != localId)
                    throw new InvalidOperationException("Host did not prepare the verified scope before starting its transport.");
                checks++;
                if (session.Join("unused.invalid") || scoped.ClientStarts != 1 ||
                    !scoped.ScopeWasReadyBeforeStart || scoped.PreparedLevelId != localId)
                    throw new InvalidOperationException("Client did not prepare the verified scope before starting its transport.");
                checks++;
                return $"Level handshake/scope {localId}: {checks} isolated checks passed.";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                Time.timeScale = previousTimeScale;
                Application.runInBackground = previousBackground;
            }
        }

        /// <summary>隔离验证普通场景正例、别关选角、外部根服务和 Editor 跨场景反例。</summary>
        public static string RunNetworkOwnershipScenePath(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run preview network ownership checks in Edit Mode.");
            Scene preview = EditorSceneManager.OpenPreviewScene(path);
            Scene otherScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var bindings = preview.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
                if (!bindings.TryValidateNetworkOwnership(out string reason))
                    throw new InvalidOperationException("Valid scene-local network ownership failed: " + reason);
                int checks = 1;
                var otherRoot = new GameObject("ForeignGameplaySelection");
                otherRoot.SetActive(false);
                SceneManager.MoveGameObjectToScene(otherRoot, otherScene);
                var otherSelection = otherRoot.AddComponent<OpeningCharacterSelectionController>();
                var ownSelection = bindings.Session.Selection;
                var foreignBindings = otherRoot.AddComponent<LevelSceneBindings>();
                bindings.Session.LevelBindings = foreignBindings;
                if (bindings.TryValidateNetworkOwnership(out _))
                    throw new InvalidOperationException("Session direct level binding accepted another gameplay owner.");
                checks++;
                bindings.Session.LevelBindings = bindings;
                bindings.Session.Selection = otherSelection;
                if (bindings.TryValidateNetworkOwnership(out _))
                    throw new InvalidOperationException("Network session referencing another gameplay scene was accepted.");
                checks++;
                bindings.Session.Selection = ownSelection;

                var outsideRoot = new GameObject("OutsideDeclaredSessionRoot");
                outsideRoot.SetActive(false);
                SceneManager.MoveGameObjectToScene(outsideRoot, preview);
                var outsideGate = outsideRoot.AddComponent<NetworkAuthorityGate>();
                outsideGate.Session = bindings.Session;
                var originalGate = bindings.AuthorityGate;
                Set(bindings, "_authorityGate", outsideGate);
                if (bindings.TryValidateNetworkOwnership(out _))
                    throw new InvalidOperationException("Authority gate outside SceneExitRoot was accepted.");
                checks++;
                Set(bindings, "_authorityGate", originalGate);

                var outsideWorld = outsideRoot.AddComponent<NetworkWorldSnapshotChannel>();
                outsideWorld.Session = bindings.Session;
                var originalWorld = bindings.WorldSnapshot;
                Set(bindings, "_worldSnapshot", outsideWorld);
                if (bindings.TryValidateNetworkOwnership(out _))
                    throw new InvalidOperationException("World snapshot outside SceneExitRoot was accepted.");
                checks++;
                Set(bindings, "_worldSnapshot", originalWorld);

                GameObject sessionRoot = bindings.Session.SceneExitRoot;
                SceneManager.MoveGameObjectToScene(sessionRoot, otherScene);
                bool acceptedForeignScene = bindings.TryValidateNetworkOwnership(out _);
                SceneManager.MoveGameObjectToScene(sessionRoot, preview);
                if (acceptedForeignScene)
                    throw new InvalidOperationException("Editor preview accepted a network root from another scene.");
                checks++;
                if (!bindings.TryValidateNetworkOwnership(out reason))
                    throw new InvalidOperationException("Ownership did not recover after isolated test restoration: " + reason);
                checks++;
                return $"Network ownership {bindings.Level.LevelId}: {checks} isolated checks passed.";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(otherScene);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        /// <summary>只读确认实际 Play 的本关网络根已移入 NGO 的持久场景，并通过所属关卡校验。</summary>
        public static string CheckLiveNetworkRoot(LevelSceneBindings bindings)
        {
            if (!EditorApplication.isPlaying || bindings == null)
                throw new InvalidOperationException("Pass the current gameplay bindings in Play Mode.");
            if (!bindings.TryValidateConfiguration(out string reason))
                throw new InvalidOperationException("Live persistent network root failed: " + reason);
            if (bindings.Session.SceneExitRoot.scene == bindings.gameObject.scene ||
                bindings.Session.SceneExitRoot.scene.name != "DontDestroyOnLoad")
                throw new InvalidOperationException("This check requires the actual NGO root in DontDestroyOnLoad.");
            return $"Live network ownership passed: {bindings.Level.LevelId}, explicitly owned DontDestroyOnLoad root.";
        }

        private static byte[] Welcome(bool playing, string levelId = "gate_fixture")
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(NetworkMessageCatalog.Authority.Welcome);
            writer.Write((byte)PlayerRole.DeepSeek);
            writer.Write((uint)1);
            writer.Write(playing);
            writer.Write(levelId);
            return stream.ToArray();
        }

        private static bool TryReadSessionLevelId(CoopSessionController session, out string levelId)
        {
            object[] arguments = { null };
            bool accepted = (bool)session.GetType().GetMethod("TryGetLevelId", PRIVATE_INSTANCE).Invoke(session, arguments);
            levelId = arguments[0] as string;
            return accepted;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, PRIVATE_INSTANCE).SetValue(target, value);

        private static void SetPhase(CoopSessionController target, SessionPhase phase) =>
            Set(target, "<Phase>k__BackingField", phase);

        private static void Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, PRIVATE_INSTANCE).Invoke(target, arguments);

        private class CaptureTransport : ITransportAdapter
        {
            public bool Server;
            public bool StartSucceeds = true;
            public int HostStarts, ClientStarts, Sends, Stops;
            public bool IsServer => Server;
            public bool IsConnected => false;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public virtual bool StartHost() { HostStarts++; return StartSucceeds; }
            public virtual bool StartClient(string address) { ClientStarts++; return StartSucceeds; }
            public void Send(ulong peer, byte[] payload, bool reliable) => Sends++;
            public void Stop() => Stops++;
        }

        private sealed class ScopedCaptureTransport : CaptureTransport, ITransportLevelScope
        {
            public bool AcceptScope;
            public int ScopeCalls;
            public string RequestedLevelId, PreparedLevelId;
            public bool ScopeWasReadyBeforeStart;

            public bool TrySetLevelId(string levelId)
            {
                ScopeCalls++;
                RequestedLevelId = levelId;
                if (!AcceptScope) return false;
                PreparedLevelId = levelId;
                return true;
            }

            public override bool StartHost()
            {
                ScopeWasReadyBeforeStart = !string.IsNullOrEmpty(PreparedLevelId);
                return base.StartHost();
            }

            public override bool StartClient(string address)
            {
                ScopeWasReadyBeforeStart = !string.IsNullOrEmpty(PreparedLevelId);
                return base.StartClient(address);
            }
        }
    }
}
