using System;
using System.Text;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Levels;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DeepSleep.Adapters.Networking
{
    /// <summary>NGO/UTP 唯一入口。现有玩法程序集无需引用网络 SDK。</summary>
    public sealed class NgoTransportAdapter : MonoBehaviour, ITransportAdapter, ITransportShutdownStatus, ITransportLevelScope
    {
        // 固定协议消息名，不是可调玩法数据。
        private const string MESSAGE = "deepsleep.session.v1";
        // 128 UTF-8 字节的 LevelId 经 Base64 最多 172B，另有版本字段、分隔符和 32B ticket。
        // 保持有界，同时为版本文本留余量；发送端也检查同一上限，不能制造必被拒绝的批准包。
        public const int MaximumApprovalPayloadBytes = 512;
        public NetworkManager Manager;
        public UnityTransport Transport;
        public NetworkTuningConfig Config;
        private ulong? _reservedPeer;
        private string _clientTicket;
        private string _guestTicket;
        private string _encodedLevelId;
        private bool _stopping;
        private bool _messageRegistered;

        public bool IsServer => Manager != null && Manager.IsServer;
        public bool IsConnected => Manager != null && Manager.IsConnectedClient;
        public string DisconnectReason => Manager != null ? Manager.DisconnectReason : "Network manager missing";
        public bool IsShutdownComplete => Manager == null ||
            (!Manager.IsListening && !Manager.ShutdownInProgress && !Manager.IsServer && !Manager.IsClient);
        public event Action<ulong> Connected;
        public event Action<ulong> Disconnected;
        public event Action<ulong, byte[]> Received;

        public bool TrySetLevelId(string levelId)
        {
            if (!IsShutdownComplete || !LevelIdentityValidation.TryValidateLevelId(levelId, out _)) return false;
            _encodedLevelId = Convert.ToBase64String(Encoding.UTF8.GetBytes(levelId));
            return true;
        }

        private void Awake()
        {
            if (Manager == null || Transport == null || Config == null || !Config.IsValid)
            {
                Debug.LogError("[NgoTransportAdapter] NetworkManager/Transport/Config 配置不完整。", this);
                enabled = false; return;
            }
            Manager.OnServerStarted += Register;
            Manager.OnClientConnectedCallback += OnConnected;
            Manager.OnClientDisconnectCallback += OnDisconnected;
            Manager.ConnectionApprovalCallback = Approve;
        }

        private void OnDestroy()
        {
            if (Manager == null) return;
            UnregisterMessage();
            Manager.OnServerStarted -= Register;
            Manager.OnClientConnectedCallback -= OnConnected;
            Manager.OnClientDisconnectCallback -= OnDisconnected;
            Manager.ConnectionApprovalCallback -= Approve;
        }

        public bool StartHost()
        {
            if (!Prepare()) return false;
            _guestTicket = null;
            Transport.SetConnectionData("127.0.0.1", Config.Port, "0.0.0.0");
            return Manager.StartHost();
        }

        public bool StartClient(string address)
        {
            if (!System.Net.IPAddress.TryParse(address, out var ip) ||
                ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || !Prepare()) return false;
            Transport.SetConnectionData(address, Config.Port);
            bool started = Manager.StartClient();
            if (started) Register();
            return started;
        }

        private bool Prepare()
        {
            if (!enabled || Manager == null || Transport == null || Config == null || !Config.IsValid ||
                !IsShutdownComplete || string.IsNullOrEmpty(_encodedLevelId)) return false;
            if (Encoding.UTF8.GetByteCount(VersionText()) + 1 + 32 > MaximumApprovalPayloadBytes) return false;
            UnregisterMessage();
            _stopping = false;
            _reservedPeer = null;
            if (string.IsNullOrEmpty(_clientTicket)) _clientTicket = NetworkClientIdentity.LoadOrCreate();
            Manager.NetworkConfig.NetworkTransport = Transport;
            Manager.NetworkConfig.EnableSceneManagement = false;
            Manager.NetworkConfig.ForceSamePrefabs = false;
            Manager.NetworkConfig.ConnectionApproval = true;
            Manager.NetworkConfig.ProtocolVersion = Config.ProtocolVersion;
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(VersionText() + "|" + _clientTicket);
            return true;
        }

        private string VersionText() => Config.ProtocolVersion + "|" + Config.ClientVersion + "|" + Config.ContentVersion + "|" + _encodedLevelId;

        private void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Pending = false;
            if (string.IsNullOrEmpty(_encodedLevelId))
            { response.Approved = false; response.Reason = "Level scope missing"; return; }
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }
            string payload = request.Payload != null && request.Payload.Length <= MaximumApprovalPayloadBytes
                ? Encoding.UTF8.GetString(request.Payload) : "";
            string prefix = VersionText() + "|";
            bool compatible = payload.StartsWith(prefix, StringComparison.Ordinal);
            string ticket = compatible ? payload.Substring(prefix.Length) : "";
            bool identity = ticket.Length == 32 && Guid.TryParseExact(ticket, "N", out _) &&
                (_guestTicket == null || _guestTicket == ticket);
            response.Approved = compatible && identity && !_reservedPeer.HasValue;
            response.Reason = !compatible ? "Client/protocol/content/level mismatch" :
                !identity ? "This slot is reserved for its reconnecting guest" : "Room full";
            if (response.Approved) { _reservedPeer = request.ClientNetworkId; _guestTicket = ticket; }
        }

        private void Register()
        {
            if (_stopping || Manager == null || Manager.CustomMessagingManager == null || _messageRegistered) return;
            Manager.CustomMessagingManager.RegisterNamedMessageHandler(MESSAGE, OnMessage);
            _messageRegistered = true;
        }
        private void UnregisterMessage()
        {
            if (_messageRegistered && Manager != null && Manager.CustomMessagingManager != null)
                Manager.CustomMessagingManager.UnregisterNamedMessageHandler(MESSAGE);
            _messageRegistered = false;
        }
        private void OnConnected(ulong id) { if (!_stopping) Connected?.Invoke(id); }
        private void OnDisconnected(ulong id)
        {
            if (_reservedPeer == id) _reservedPeer = null;
            if (!_stopping) Disconnected?.Invoke(id);
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            if (_stopping) return;
            int remaining = reader.Length - reader.Position;
            if (remaining < 1 || remaining > Config.MaximumMessageBytes) return;
            byte[] bytes = new byte[remaining];
            reader.ReadBytesSafe(ref bytes, remaining);
            Received?.Invoke(sender, bytes);
        }

        public void Send(ulong peer, byte[] payload, bool reliable)
        {
            if (_stopping || Manager == null || !Manager.IsListening || payload == null || payload.Length > Config.MaximumMessageBytes) return;
            using var writer = new FastBufferWriter(payload.Length, Allocator.Temp);
            writer.WriteBytesSafe(payload);
            Manager.CustomMessagingManager.SendNamedMessage(MESSAGE, peer, writer,
                reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced);
        }

        public void Stop()
        {
            _stopping = true;
            UnregisterMessage();
            if (Manager != null && (Manager.IsListening || Manager.IsServer || Manager.IsClient) && !Manager.ShutdownInProgress)
                Manager.Shutdown(discardMessageQueue: true);
        }
    }
}
