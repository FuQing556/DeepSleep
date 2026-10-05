using System;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Adapters.Networking
{
    public sealed class SelectableTransportAdapter : MonoBehaviour, ITransportAdapter, IRelaySelection, ITransportShutdownStatus, ITransportLevelScope
    {
        public NgoTransportAdapter Lan;
        public WebSocketRelayAdapter Relay;
        public bool UseRelay { get; private set; }
        private ITransportAdapter Active => UseRelay ? (Relay != null ? Relay : null) : (Lan != null ? Lan : null);
        public bool IsServer => Active != null && Active.IsServer;
        public bool IsConnected => Active != null && Active.IsConnected;
        public string DisconnectReason => Active != null ? Active.DisconnectReason : "Transport unavailable";
        public string RoomCode => Relay != null ? Relay.RoomCode : string.Empty;
        public bool IsShutdownComplete => (Lan == null || Lan.IsShutdownComplete) &&
            (Relay == null || Relay.IsShutdownComplete);
        public event Action<ulong> Connected;
        public event Action<ulong> Disconnected;
        public event Action<ulong,byte[]> Received;
        private void Awake()
        {
            if (Lan == null || Relay == null)
            {
                Debug.LogError("[SelectableTransport] LAN 与 Relay 必须显式配置。", this);
                enabled = false;
                return;
            }
            Lan.Connected += ConnectedEvent; Relay.Connected += ConnectedEvent;
            Lan.Disconnected += DisconnectedEvent; Relay.Disconnected += DisconnectedEvent;
            Lan.Received += ReceivedEvent; Relay.Received += ReceivedEvent;
        }
        private void OnDestroy()
        {
            if (Lan != null)
            { Lan.Connected -= ConnectedEvent; Lan.Disconnected -= DisconnectedEvent; Lan.Received -= ReceivedEvent; }
            if (Relay != null)
            { Relay.Connected -= ConnectedEvent; Relay.Disconnected -= DisconnectedEvent; Relay.Received -= ReceivedEvent; }
        }
        private void ConnectedEvent(ulong id) => Connected?.Invoke(id);
        private void DisconnectedEvent(ulong id) => Disconnected?.Invoke(id);
        private void ReceivedEvent(ulong id,byte[] bytes) => Received?.Invoke(id,bytes);
        public bool TrySetLevelId(string levelId)
        {
            if (Lan == null || Relay == null || !IsShutdownComplete ||
                !DeepSleep.Runtime.Progression.Levels.LevelIdentityValidation.TryValidateLevelId(levelId, out _)) return false;
            // 两种传输的容量不同（Relay version 限 100 字符），未选中的 Relay 不能限制 LAN。
            return Active is ITransportLevelScope scope && scope.TrySetLevelId(levelId);
        }
        public void SelectRelay(bool value,string endpoint,string room)
        { UseRelay = value; if (Relay != null) { Relay.Endpoint = endpoint; Relay.RoomCode = room; } }
        public bool StartHost() => enabled && Active != null && Active.StartHost();
        public bool StartClient(string address) => enabled && Active != null && Active.StartClient(address);
        public void Send(ulong peer,byte[] bytes,bool reliable) => Active?.Send(peer,bytes,reliable);
        public void Stop()
        {
            // 即使当前选择 Relay，场景中的 NGO 根仍然 DDOL；退出要关闭两条传输的资源。
            try { if (Lan != null) Lan.Stop(); }
            finally { if (Relay != null) Relay.Stop(); }
        }
    }
}
