using System;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Adapters.Networking
{
    public sealed class SelectableTransportAdapter : MonoBehaviour, ITransportAdapter, IRelaySelection
    {
        public NgoTransportAdapter Lan;
        public WebSocketRelayAdapter Relay;
        public bool UseRelay { get; private set; }
        private ITransportAdapter Active => UseRelay ? Relay : Lan;
        public bool IsServer => Active.IsServer;
        public bool IsConnected => Active.IsConnected;
        public string DisconnectReason => Active.DisconnectReason;
        public string RoomCode => Relay.RoomCode;
        public event Action<ulong> Connected;
        public event Action<ulong> Disconnected;
        public event Action<ulong,byte[]> Received;
        private void Awake()
        {
            Lan.Connected += ConnectedEvent; Relay.Connected += ConnectedEvent;
            Lan.Disconnected += DisconnectedEvent; Relay.Disconnected += DisconnectedEvent;
            Lan.Received += ReceivedEvent; Relay.Received += ReceivedEvent;
        }
        private void OnDestroy()
        {
            Lan.Connected -= ConnectedEvent; Relay.Connected -= ConnectedEvent;
            Lan.Disconnected -= DisconnectedEvent; Relay.Disconnected -= DisconnectedEvent;
            Lan.Received -= ReceivedEvent; Relay.Received -= ReceivedEvent;
        }
        private void ConnectedEvent(ulong id) => Connected?.Invoke(id);
        private void DisconnectedEvent(ulong id) => Disconnected?.Invoke(id);
        private void ReceivedEvent(ulong id,byte[] bytes) => Received?.Invoke(id,bytes);
        public void SelectRelay(bool value,string endpoint,string room)
        { UseRelay = value; Relay.Endpoint = endpoint; Relay.RoomCode = room; }
        public bool StartHost() => Active.StartHost();
        public bool StartClient(string address) => Active.StartClient(address);
        public void Send(ulong peer,byte[] bytes,bool reliable) => Active.Send(peer,bytes,reliable);
        public void Stop() => Active.Stop();
    }
}
