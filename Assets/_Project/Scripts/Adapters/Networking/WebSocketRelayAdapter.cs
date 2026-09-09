using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Adapters.Networking
{
    /// <summary>Windows/Android WSS 转发适配器。Socket 线程不访问场景；事件回到 Update 派发。</summary>
    public sealed class WebSocketRelayAdapter : MonoBehaviour, ITransportAdapter
    {
        public NetworkTuningConfig Config;
        public string Endpoint = "ws://127.0.0.1:8765";
        public string RoomCode = "";
        private ClientWebSocket _socket;
        private CancellationTokenSource _cancel;
        private readonly ConcurrentQueue<Action> _main = new();
        private readonly ConcurrentQueue<byte[]> _outgoing = new();
        private readonly SemaphoreSlim _signal = new(0);
        private string _ticket;
        private void Awake() => _ticket = NetworkClientIdentity.LoadOrCreate();
        private int _generation;
        private bool _running;
        public bool IsServer { get; private set; }
        public bool IsConnected { get; private set; }
        public string DisconnectReason { get; private set; }
        public event Action<ulong> Connected;
        public event Action<ulong> Disconnected;
        public event Action<ulong, byte[]> Received;
        [Serializable] private sealed class Hello { public string action, version, room, ticket; }
        public bool StartHost() => Begin(true);
        public bool StartClient(string address) { RoomCode = address.Trim(); return Begin(false); }
        private bool Begin(bool host)
        {
            if (_running || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "wss" && !(uri.Scheme == "ws" && uri.IsLoopback))) return false;
            if (host && string.IsNullOrWhiteSpace(RoomCode)) RoomCode = Guid.NewGuid().ToString("N").Substring(0,20);
            if (string.IsNullOrWhiteSpace(RoomCode)) return false;
            IsServer = host; IsConnected = false; DisconnectReason = null; _running = true;
            int generation = ++_generation; _socket = new ClientWebSocket(); _cancel = new CancellationTokenSource();
            string hello = JsonUtility.ToJson(new Hello { action = host ? "host" : "join", room = RoomCode,
                version = Config.ProtocolVersion + "|" + Config.ClientVersion + "|" + Config.ContentVersion, ticket = _ticket });
            _ = Run(_socket, uri, hello, _cancel.Token, generation);
            return true;
        }
        private async Task Run(ClientWebSocket socket, Uri uri, string hello, CancellationToken cancel, int generation)
        {
            try
            {
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                await socket.ConnectAsync(uri, cancel).ConfigureAwait(false);
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(hello)), WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
                var send = SendLoop(socket, cancel);
                byte[] buffer = new byte[Config.MaximumMessageBytes];
                while (!cancel.IsCancellationRequested)
                {
                    int length = 0; WebSocketReceiveResult part;
                    do
                    {
                        if (length == buffer.Length) throw new InvalidOperationException("Relay message exceeds limit");
                        part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length-length), cancel).ConfigureAwait(false);
                        if (part.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Relay closed");
                        length += part.Count;
                    } while (!part.EndOfMessage);
                    if (_main.Count > 2048) throw new InvalidOperationException("Relay receive queue overloaded");
                    if (part.MessageType == WebSocketMessageType.Text)
                    {
                        string text = Encoding.UTF8.GetString(buffer,0,length);
                        _main.Enqueue(() => { if (generation == _generation) Control(text); });
                    }
                    else
                    {
                        var payload = new byte[length]; Buffer.BlockCopy(buffer,0,payload,0,length);
                        _main.Enqueue(() => { if (generation == _generation) Received?.Invoke(IsServer ? 1ul : 0ul,payload); });
                    }
                }
                await send.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!cancel.IsCancellationRequested)
                    _main.Enqueue(() => { if (generation == _generation) Fail(ex.Message); });
            }
        }
        private async Task SendLoop(ClientWebSocket socket, CancellationToken cancel)
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    await _signal.WaitAsync(cancel).ConfigureAwait(false);
                    while (_outgoing.TryDequeue(out var bytes))
                        await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Binary,true,cancel).ConfigureAwait(false);
                }
            }
            catch (Exception) { if (!cancel.IsCancellationRequested) socket.Abort(); }
        }
        private void Control(string value)
        {
            if (value == "READY") { IsConnected = true; Connected?.Invoke(0); }
            else if (value == "PEER") Connected?.Invoke(1);
            else if (value == "LEFT") Disconnected?.Invoke(1);
            else if (value.StartsWith("ERROR:")) Fail(value.Substring(6));
        }
        private void Fail(string reason)
        { DisconnectReason = reason; Stop(); Disconnected?.Invoke(0); }
        private void Update() { for (int i=0;i<512 && _main.TryDequeue(out var action);i++) action(); }
        public void Send(ulong peer, byte[] payload, bool reliable)
        {
            if (!_running || payload == null || payload.Length > Config.MaximumMessageBytes) return;
            if (_outgoing.Count >= 1024) { Fail("Relay send queue overloaded"); return; }
            _outgoing.Enqueue(payload); _signal.Release();
        }
        public void Stop()
        {
            _running = false; IsConnected = false; ++_generation;
            _cancel?.Cancel(); _socket?.Abort(); _socket?.Dispose(); _socket = null;
            while (_outgoing.TryDequeue(out _)) { }
        }
        private void OnDestroy() => Stop();
    }
}
