using System;
using System.Collections;
using System.Reflection;
using System.Text;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.CharacterSelection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// Play 模式下的真实场景往返检查；会离开当前选角界面，最终停在 MainMenu。
    /// 只创建无客人的本机 LAN 房间及 loopback 连接；不是双机/公网断线验收。
    /// 不改场景/资产、不替换 NetworkManager、不绕过 Router，也不吞掉正常的错误日志。
    /// </summary>
    public static class SceneExitLifecycleChecks
    {
        private const string WorldAsset = "Assets/_Project/Configs/Progression/Meta/CFG_META_Level_World01_EarlyInternet.asset";
        private const string PrototypeAsset = "Assets/_Project/Configs/Progression/Meta/CFG_META_Level_PrototypeSky.asset";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static bool Running { get; private set; }
        public static string Status { get; private set; } = "Not run";
        public static string LastReport { get; private set; } = string.Empty;

        private sealed class Step
        {
            public string Name, Scene;
            public int Count;
            public bool ChangesScene = true;
            public Action Request, Verify;
        }

        public static string Start()
        {
            if (!EditorApplication.isPlaying || GameAppRoot.Instance == null)
                return "Requires Play from Boot/MainMenu or a fresh, unselected gameplay scene.";
            var router = GameAppRoot.Instance.SceneRouter;
            if (Running || router.IsTransitioning) return "A lifecycle check or scene transition is already running.";
            foreach (var session in Find<CoopSessionController>())
                if (session.Phase != SessionPhase.Offline || session.HasPeer || session.Selection.IsSelectionComplete)
                    return "Refused: leave the active match first; this check only accepts an idle selection screen or MainMenu.";
            var world = AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(WorldAsset);
            var prototype = AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(PrototypeAsset);
            if (world == null || prototype == null) return "Missing level definitions.";
            Running = true;
            LastReport = string.Empty;
            Status = "Starting scene lifecycle matrix";
            router.StartCoroutine(GuardedRun(router, RunMatrix(router, world, prototype)));
            return Status;
        }

        private static IEnumerator GuardedRun(GameSceneRouter router, IEnumerator work)
        {
            try
            {
                while (true)
                {
                    bool more = false;
                    object next = null;
                    Exception failure = null;
                    try { more = work.MoveNext(); if (more) next = work.Current; }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null)
                    {
                        Status = "FAILED: " + Status + "\n" + failure;
                        LastReport += "\n" + Status;
                        Debug.LogError("[SceneExitLifecycleChecks] " + LastReport, router);
                        yield break;
                    }
                    if (!more) yield break;
                    yield return next;
                }
            }
            finally
            {
                (work as IDisposable)?.Dispose();
                Running = false;
            }
        }

        private static IEnumerator RunMatrix(GameSceneRouter router, MetaLevelDefinition world, MetaLevelDefinition prototype)
        {
            var report = new StringBuilder();
            var steps = new[]
            {
                Route("Initial menu cleanup", "MainMenu", 0, () => router.LoadMainMenu(MainMenuPage.Home)),
                Route("World solo selection", world.SceneName, 1, () => router.StartLevel(world, GameLaunchMode.Solo)),
                Route("Selection back button", "MainMenu", 0, () => Invoke(One<GameplayEntryFlow>(), "ReturnToModes")),
                Route("World solo re-entry", world.SceneName, 1, () => router.StartLevel(world, GameLaunchMode.Solo)),
                Route("Chapter return after solo selection", "MainMenu", 0, () =>
                {
                    Require(One<CoopSessionController>().Selection.TrySelect(PlayerRole.DeepSeek), "Solo role selection failed");
                    Require(Mathf.Approximately(Time.timeScale, 1f), "Selecting a solo role did not restore normal simulation");
                    Invoke(One<ChapterRunController>(), "ReturnToOpening");
                }),
                Route("World before direct switch", world.SceneName, 1, () => router.StartLevel(world, GameLaunchMode.Solo)),
                Route("Direct StartLevel World -> Prototype", prototype.SceneName, 1, () => router.StartLevel(prototype, GameLaunchMode.Solo)),
                Route("Prototype menu return", "MainMenu", 0, () => router.LoadMainMenu(MainMenuPage.Home)),
                Route("Online selection", world.SceneName, 1, () => router.StartLevel(world, GameLaunchMode.Online)),
                Host("Start local DS host", world.SceneName, PlayerRole.DeepSeek),
                Route("Online menu Leave", "MainMenu", 0, () => Invoke(One<CoopSessionMenu>(), "Leave")),
                Route("Online re-entry after shutdown", world.SceneName, 1, () => router.StartLevel(world, GameLaunchMode.Online)),
                Host("Rebind same port as HS host", world.SceneName, PlayerRole.Harness),
                Route("Host direct Router return", "MainMenu", 0, () => router.LoadMainMenu(MainMenuPage.Home)),
                Route("Client selection", world.SceneName, 1, () => router.StartLevel(world, GameLaunchMode.Online)),
                Route("Exit while loopback client is connecting", "MainMenu", 0, () =>
                {
                    var session = One<CoopSessionController>();
                    SelectLan(session);
                    Require(session.Join("127.0.0.1"), "Loopback StartClient failed");
                    Require(session.Phase == SessionPhase.Connecting, "Expected connecting phase before exit");
                    Invoke(One<CoopSessionMenu>(), "Leave");
                })
            };

            int passed = 0;
            foreach (var step in steps)
            {
                Status = step.Name;
                var oldSessions = Find<CoopSessionController>();
                var oldManagers = Find<NetworkManager>();
                step.Request();
                float deadline = Time.realtimeSinceStartup + 20f;
                // Even a very fast transition must give Awake/Start/OnDestroy their real engine frames.
                yield return null;
                while (router.IsTransitioning)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Router did not finish in 20 seconds");
                    Require(Mathf.Approximately(Time.timeScale, 0f), "Old scene simulation resumed during transition");
                    yield return null;
                }
                Require(string.IsNullOrEmpty(router.LastTransitionError), router.LastTransitionError);
                Require(SceneManager.GetActiveScene().name == step.Scene, "Wrong active scene at " + step.Name);
                Require(Mathf.Approximately(Time.timeScale, step.Count == 0 ? 1f : 0f),
                    "Menu must run normally, while selection/lobby must stay paused");
                if (step.ChangesScene)
                {
                    foreach (var old in oldSessions) Require(old == null, "Previous session survived scene transition");
                    foreach (var old in oldManagers) Require(old == null, "Previous NetworkManager survived scene transition");
                }
                var sessions = Find<CoopSessionController>();
                var managers = Find<NetworkManager>();
                Require(sessions.Length == step.Count, "Session count=" + sessions.Length + "; expected " + step.Count);
                Require(managers.Length == step.Count, "NetworkManager count=" + managers.Length + "; expected " + step.Count);
                Require(router.RegisteredSceneExitParticipantCount == step.Count,
                    "Router participant count=" + router.RegisteredSceneExitParticipantCount + "; expected " + step.Count);
                Require(step.Count == 0 ? NetworkManager.Singleton == null : NetworkManager.Singleton == managers[0],
                    "NetworkManager.Singleton is stale or points at a different instance");
                foreach (var session in sessions) Require(session.enabled && !session.IsExiting, "New session is disabled or already exiting");
                step.Verify?.Invoke();
                // Discovery is unrelated to this check; don't keep scanning while testing local transport teardown.
                foreach (var discovery in Find<LanRoomDiscovery>()) { discovery.SetBrowsing(false); discovery.enabled = false; }
                report.AppendLine(++passed + ". " + step.Name + ": session/manager/registered=" +
                    sessions.Length + "/" + managers.Length + "/" + router.RegisteredSceneExitParticipantCount);
                LastReport = report.ToString();
            }
            Status = "PASSED " + passed + " lifecycle stations; local host/connecting only, not dual-device or relay acceptance.";
            LastReport = Status + "\n" + report;
            Debug.Log("[SceneExitLifecycleChecks] " + LastReport, router);
        }

        private static Step Route(string name, string scene, int count, Action request) =>
            new Step { Name = name, Scene = scene, Count = count, Request = request };

        private static Step Host(string name, string scene, PlayerRole role) => new Step
        {
            Name = name, Scene = scene, Count = 1, ChangesScene = false,
            Request = () =>
            {
                var session = One<CoopSessionController>();
                SelectLan(session);
                Require(session.Create(role), "Local host failed to bind configured port");
            },
            Verify = () => Require(One<NetworkManager>().IsListening && One<CoopSessionController>().IsAuthority,
                "Local host is not listening/authoritative after its first update")
        };

        private static void SelectLan(CoopSessionController session)
        {
            Require(session.TransportComponent is IRelaySelection, "Expected selectable LAN/relay transport");
            ((IRelaySelection)session.TransportComponent).SelectRelay(false, string.Empty, string.Empty);
        }

        private static T[] Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        private static T One<T>() where T : UnityEngine.Object
        {
            var values = Find<T>();
            Require(values.Length == 1, "Expected exactly one " + typeof(T).Name + ", found " + values.Length);
            return values[0];
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, Private);
            Require(method != null, "Missing lifecycle entry point: " + methodName);
            try { method.Invoke(target, null); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
