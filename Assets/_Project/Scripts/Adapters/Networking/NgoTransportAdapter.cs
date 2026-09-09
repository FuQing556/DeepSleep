using System;
using System.Text;
using DeepSleep.Runtime.Networking;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DeepSleep.Adapters.Networking
{
    /// <summary>NGO/UTP 唯一入口。现有玩法程序集无需引用网络 SDK。</summary>
    public sealed class NgoTransportAdapter : MonoBehaviour, ITransportAdapter
    {
        // 固定协议消息名，不是可调玩法数据。
        private const string MESSAGE = "deepsleep.session.v1";
        public NetworkManager Manager;
        public UnityTransport Transport;
        public NetworkTuningConfig Config;
        private ulong? _reservedPeer;
        private string _clientTicket;
        private string _guestTicket;

        public bool IsServer => Manager != null && Manager.IsServer;
        public bool IsConnected => Manager != null && Manager.IsConnectedClient;
        public string DisconnectReason => Manager != null ? Manager.DisconnectReason : "Network manager missing";
        public event Action<ulong> Connected;
        public event Action<ulong> Disconnected;
        public event Action<ulong, byte[]> Received;

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
            Manager.OnServerStarted -= Register;
            Manager.OnClientConnectedCallback -= OnConnected;
            Manager.OnClientDisconnectCallback -= OnDisconnected;
            Manager.ConnectionApprovalCallback = null;
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
            if (!enabled || Manager.IsListening || Manager.ShutdownInProgress) return false;
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

        private string VersionText() => Config.ProtocolVersion + "|" + Config.ClientVersion + "|" + Config.ContentVersion;

        private void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Pending = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }
            string payload = request.Payload != null && request.Payload.Length <= 256 ? Encoding.UTF8.GetString(request.Payload) : "";
            string prefix = VersionText() + "|";
            bool compatible = payload.StartsWith(prefix, StringComparison.Ordinal);
            string ticket = compatible ? payload.Substring(prefix.Length) : "";
            bool identity = ticket.Length == 32 && Guid.TryParseExact(ticket, "N", out _) &&
                (_guestTicket == null || _guestTicket == ticket);
            response.Approved = compatible && identity && !_reservedPeer.HasValue;
            response.Reason = !compatible ? "Client/protocol/content version mismatch" :
                !identity ? "This slot is reserved for its reconnecting guest" : "Room full";
            if (response.Approved) { _reservedPeer = request.ClientNetworkId; _guestTicket = ticket; }
        }

        private void Register() => Manager.CustomMessagingManager.RegisterNamedMessageHandler(MESSAGE, OnMessage);
        private void OnConnected(ulong id) => Connected?.Invoke(id);
        private void OnDisconnected(ulong id)
        {
            if (_reservedPeer == id) _reservedPeer = null;
            Disconnected?.Invoke(id);
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            int remaining = reader.Length - reader.Position;
            if (remaining < 1 || remaining > Config.MaximumMessageBytes) return;
            byte[] bytes = new byte[remaining];
            reader.ReadBytesSafe(ref bytes, remaining);
            Received?.Invoke(sender, bytes);
        }

        public void Send(ulong peer, byte[] payload, bool reliable)
        {
            if (!Manager.IsListening || payload == null || payload.Length > Config.MaximumMessageBytes) return;
            using var writer = new FastBufferWriter(payload.Length, Allocator.Temp);
            writer.WriteBytesSafe(payload);
            Manager.CustomMessagingManager.SendNamedMessage(MESSAGE, peer, writer,
                reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced);
        }

        public void Stop() { if (Manager.IsListening) Manager.Shutdown(); }
    }
}
