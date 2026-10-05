using System;
using System.Reflection;
using System.Text;
using DeepSleep.Adapters.Networking;
using DeepSleep.Runtime.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>隔离调用真实 NGO 批准器与 Relay Hello 编码；从不启动 Manager、Socket 或读取真实重连票据。</summary>
    public static class LevelTransportApprovalChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string TicketA = "11111111111111111111111111111111";
        private const string TicketB = "22222222222222222222222222222222";
        [Serializable] private sealed class HelloView { public string action, version, room, ticket; }

        [MenuItem("DeepSleep/Diagnostics/Check Level Transport Approval")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run isolated transport approval checks in Edit Mode.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            NetworkManager originalSingleton = NetworkManager.Singleton;
            NetworkManager manager = null;
            NetworkTuningConfig config = null;
            int checks = 0;
            try
            {
                var root = new GameObject("LevelTransportApprovalChecks_Isolated"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
                config.Port = 7777; config.ProtocolVersion = NetworkMessageCatalog.ProtocolVersion;
                config.ClientVersion = "1"; config.ContentVersion = "foundation";
                config.SnapshotRate = 20; config.InputTimeout = .2f; config.ConnectionTimeout = 5;
                config.MaximumQueuedCommands = 32; config.MaximumMessageBytes = 16384; config.RemoteInterpolationSpeed = 30;
                manager = Child(root.transform, "InactiveManager").AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                var ngo = Child(root.transform, "Ngo").AddComponent<NgoTransportAdapter>();
                ngo.Manager = manager; ngo.Config = config;
                ngo.Transport = Child(root.transform, "InactiveUtp").AddComponent<UnityTransport>();
                Set(ngo, "_clientTicket", TicketA); // Prepare 不接触 PlayerPrefs 或真实身份。
                var approve = (Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse>)
                    ngo.GetType().GetMethod("Approve", Hidden).CreateDelegate(
                        typeof(Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse>), ngo);
                NetworkManager.ConnectionApprovalResponse Approve(byte[] payload, ulong id = 1)
                {
                    var response = new NetworkManager.ConnectionApprovalResponse();
                    approve(new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = id, Payload = payload }, response);
                    return response;
                }
                void Check(bool valid, string reason)
                { if (!valid) throw new InvalidOperationException("Level transport: " + reason); checks++; }
                byte[] Payload(string level, string ticket = TicketA) => Encoding.UTF8.GetBytes(
                    config.ProtocolVersion + "|" + config.ClientVersion + "|" + config.ContentVersion + "|" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(level)) + "|" + ticket);

                Check(!ngo.StartHost() && !ngo.StartClient("127.0.0.1") && !manager.IsListening,
                    "Missing scope reached NGO startup.");
                Check(!Approve(Payload("World01")).Approved && Get(ngo, "_guestTicket") == null,
                    "Unscoped host approved or reserved an identity.");
                foreach (string invalid in new[] { null, "", " World01", "World01 ", "bad\ud800id", new string('a', 129) })
                    Check(!ngo.TrySetLevelId(invalid) && Get(ngo, "_encodedLevelId") == null, "Invalid level scope accepted.");
                Check(ngo.TrySetLevelId("World01"), "Valid NGO scope rejected.");
                string encoded = (string)Get(ngo, "_encodedLevelId");
                Check(!Approve(Payload("Gameplay_Prototype", TicketB)).Approved &&
                    Get(ngo, "_reservedPeer") == null && Get(ngo, "_guestTicket") == null,
                    "Wrong-level client reserved the reconnect slot before rejection.");
                Check(!Approve(Encoding.UTF8.GetBytes(config.ProtocolVersion + "|1|foundation|" + TicketA)).Approved &&
                    Get(ngo, "_guestTicket") == null, "Old no-scope connection data was accepted.");
                Check(Approve(Payload("World01")).Approved && (string)Get(ngo, "_guestTicket") == TicketA,
                    "Correct-level client could not join after wrong-level rejection.");
                Check(!Approve(Payload("World01"), 2).Approved, "Occupied room accepted another peer.");
                Invoke(ngo, "OnDisconnected", (ulong)1);
                Check(!Approve(Payload("World01", TicketB), 2).Approved && Get(ngo, "_reservedPeer") == null,
                    "Disconnect lost established reconnect identity lock.");
                Check(Approve(Payload("World01"), 3).Approved, "Original ticket could not reconnect.");
                Invoke(ngo, "OnDisconnected", (ulong)3);

                typeof(NetworkManager).GetProperty("IsListening").SetValue(manager, true);
                Check(!ngo.TrySetLevelId("Gameplay_Prototype") && (string)Get(ngo, "_encodedLevelId") == encoded,
                    "Live NGO scope changed.");
                typeof(NetworkManager).GetProperty("IsListening").SetValue(manager, false);
                Set(manager, "m_ShuttingDown", true);
                Check(!ngo.TrySetLevelId("Gameplay_Prototype"), "Closing NGO scope changed.");
                Set(manager, "m_ShuttingDown", false);

                string maximumLevel = new string('界', 42) + "ab"; // 128 UTF-8 B / 172 Base64 characters.
                Check(ngo.TrySetLevelId(maximumLevel), "Maximum legal UTF-8 ID rejected by LAN.");
                config.ClientVersion = new string('v', 40); config.ContentVersion = new string('c', 40);
                Set(ngo, "_guestTicket", null);
                byte[] large = Payload(maximumLevel);
                Check(large.Length > 256 && large.Length <= NgoTransportAdapter.MaximumApprovalPayloadBytes,
                    "Long approval fixture did not exceed old 256 B limit.");
                Check((bool)Invoke(ngo, "Prepare") && Equal(manager.NetworkConfig.ConnectionData, large),
                    "Real NGO Prepare lost scope or produced inconsistent approval data.");
                Check(Approve(large, 4).Approved, "Bounded long approval payload rejected.");
                Invoke(ngo, "OnDisconnected", (ulong)4); Set(ngo, "_guestTicket", null);
                Check(!Approve(new byte[NgoTransportAdapter.MaximumApprovalPayloadBytes + 1]).Approved &&
                    Get(ngo, "_guestTicket") == null, "Oversized approval changed identity state.");
                config.ContentVersion = new string('c', 600);
                Check(!(bool)Invoke(ngo, "Prepare"), "Outgoing oversized approval was allowed to start.");
                config.ClientVersion = "1"; config.ContentVersion = "foundation";

                var relay = Child(root.transform, "Relay").AddComponent<WebSocketRelayAdapter>();
                relay.Config = config; relay.RoomCode = "fixture_room"; Set(relay, "_ticket", TicketA);
                Check(!relay.StartHost() && !relay.StartClient("fixture_room") && relay.IsShutdownComplete,
                    "Missing relay scope started a socket.");
                Check(!relay.TrySetLevelId("bad\ud800id") && !relay.TrySetLevelId(maximumLevel) &&
                    relay.DisconnectReason.Contains("100-character"), "Relay failed to explain its existing service capacity.");
                Check(relay.TrySetLevelId("World01") && ngo.TrySetLevelId("World01"), "Current short level not supported.");
                string ngoVersion = Version(ngo);
                var hello = JsonUtility.FromJson<HelloView>((string)Invoke(relay, "HelloText", true));
                Check(hello.version == ngoVersion && hello.action == "host" && hello.room == "fixture_room" && hello.ticket == TicketA,
                    "Relay Hello did not preserve its schema or match LAN compatibility scope.");
                Check(relay.TrySetLevelId("Gameplay_Prototype"), "Second short level rejected.");
                var other = JsonUtility.FromJson<HelloView>((string)Invoke(relay, "HelloText", false));
                Check(other.version != hello.version && other.action == "join", "Different levels share relay version scope.");
                string beforeLive = (string)Get(relay, "_encodedLevelId");
                Set(relay, "_running", true); // 连接中但尚未 IsConnected 也必须保护 scope。
                Check(!relay.TrySetLevelId("World01") && (string)Get(relay, "_encodedLevelId") == beforeLive &&
                    !relay.IsShutdownComplete, "Connecting relay allowed a scope change.");
                Set(relay, "_running", false);

                var selectable = Child(root.transform, "Selectable").AddComponent<SelectableTransportAdapter>();
                selectable.Lan = ngo; selectable.Relay = relay;
                selectable.SelectRelay(false, relay.Endpoint, relay.RoomCode);
                Check(selectable.TrySetLevelId(maximumLevel) && (string)Get(relay, "_encodedLevelId") == beforeLive,
                    "Unselected relay restricted LAN scope or was partially modified.");
                selectable.SelectRelay(true, relay.Endpoint, relay.RoomCode);
                string beforeLan = (string)Get(ngo, "_encodedLevelId");
                Check(!selectable.TrySetLevelId(maximumLevel) && (string)Get(ngo, "_encodedLevelId") == beforeLan,
                    "Rejected relay scope partially modified LAN.");
                Check(selectable.TrySetLevelId("World01"), "Selectable did not configure active relay.");
                Set(relay, "_running", true); selectable.SelectRelay(false, relay.Endpoint, relay.RoomCode);
                Check(!selectable.TrySetLevelId("World01") && !selectable.IsShutdownComplete,
                    "Inactive-selection relay still connecting did not block a new scope.");
                Set(relay, "_running", false);
                Check(NetworkManager.Singleton == originalSingleton && !manager.IsListening,
                    "Isolated checks touched the active NetworkManager or started transport.");
                return $"PASS: {checks} isolated transport-level checks; wrong-level approval cannot reserve ticket; original reconnect identity preserved; " +
                    "512 B LAN bound, strict UTF-8/Base64 scope, live-change guard, Relay unchanged Hello schema/100-character service bound. No socket, server deployment or saved assets.";
            }
            finally
            {
                if (manager != null)
                {
                    typeof(NetworkManager).GetProperty("IsListening").SetValue(manager, false);
                    Set(manager, "m_ShuttingDown", false);
                }
                EditorSceneManager.ClosePreviewScene(preview);
                if (config != null) Object.DestroyImmediate(config);
            }
        }

        private static GameObject Child(Transform parent, string name)
        { var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(parent, false); return go; }
        private static string Version(object owner) => (string)owner.GetType()
            .GetMethod("VersionText", Hidden, null, Type.EmptyTypes, null).Invoke(owner, null);
        private static object Get(object owner, string field) => owner.GetType().GetField(field, Hidden).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
        private static object Invoke(object owner, string method, params object[] args) =>
            owner.GetType().GetMethod(method, Hidden).Invoke(owner, args);
        private static bool Equal(byte[] a, byte[] b)
        { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
