using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>局域网UDP请求/响应发现；只在大厅扫描，房主用独立端口应答，不承载游戏数据。</summary>
    public sealed class LanRoomDiscovery : MonoBehaviour
    {
        public CoopSessionController Session;
        public sealed class Room
        {
            public string Id, Address, Name;
            public bool Compatible;
            public float LastSeen;
        }
        [Serializable] private sealed class Packet
        {
            public string magic, nonce, id, name, content;
            public int protocol, port, kind;
        }
        private const string Magic = "DeepSleep-LAN-1";
        private readonly List<Room> _rooms = new();
        private readonly List<IPEndPoint> _destinations = new();
        private readonly byte[] _buffer = new byte[2048];
        private Socket _socket;
        private string _nonce, _roomId;
        private bool _browsing, _hosting;
        private float _nextQuery, _retryAt;
        private int DiscoveryPort => Session.Config.Port < 65535 ? Session.Config.Port + 1 : 47778;
        public IReadOnlyList<Room> Rooms => _rooms;
        public string Status { get; private set; } = "连接同一 Wi-Fi 或手机热点后，可自动查找房间。";
        public event Action Changed;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _wifiLock;
#endif
        public void SetBrowsing(bool value)
        {
            if (_browsing == value) return;
            _browsing = value;
            Close();
            if (value) Rescan();
        }
        public void Rescan()
        {
            _rooms.Clear(); _nonce = Guid.NewGuid().ToString("N");
            _nextQuery = _retryAt = 0;
            BuildDestinations();
            Status = "正在查找同网房间… 房主创建房间后会自动显示。";
            Changed?.Invoke();
        }
        private void Update()
        {
            if (Session == null || Session.Config == null) return;
            bool host = Session.IsAuthority && !((Session.TransportComponent as IRelaySelection)?.UseRelay ?? false) &&
                (Session.Phase == SessionPhase.Lobby || Session.Phase == SessionPhase.Playing);
            if (host != _hosting) { Close(); _hosting = host; _retryAt = 0; _roomId = Guid.NewGuid().ToString("N"); }
            if (!host && !_browsing) { Close(); return; }
            if (_socket == null && Time.unscaledTime >= _retryAt) Open(host);
            if (_socket == null) return;
            try
            {
                if (!host && Time.unscaledTime >= _nextQuery)
                {
                    _nextQuery = Time.unscaledTime + 1.5f;
                    var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Packet { magic = Magic, kind = 0, nonce = _nonce }));
                    foreach (var destination in _destinations)
                        try { _socket.SendTo(bytes, destination); } catch (SocketException) { /* 其他网卡仍可发现。 */ }
                }
                for (int i = 0; i < 24 && _socket.Poll(0, SelectMode.SelectRead); i++)
                {
                    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    int count = _socket.ReceiveFrom(_buffer, ref sender);
                    if (count <= 0 || count >= _buffer.Length) continue;
                    Packet packet;
                    try { packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(_buffer, 0, count)); }
                    catch (ArgumentException) { continue; }
                    if (packet == null || packet.magic != Magic || packet.nonce == null || packet.nonce.Length != 32) continue;
                    var endpoint = (IPEndPoint)sender;
                    if (host && packet.kind == 0 && !Session.HasPeer)
                    {
                        var reply = new Packet { magic = Magic, kind = 1, nonce = packet.nonce, id = _roomId,
                            name = (Session.HostRole == Players.Identity.PlayerRole.DeepSeek ? "DS" : "HS") + " 的房间",
                            protocol = Session.Config.ProtocolVersion, content = Session.Config.ContentVersion, port = Session.Config.Port };
                        _socket.SendTo(Encoding.UTF8.GetBytes(JsonUtility.ToJson(reply)), endpoint);
                    }
                    else if (!host && packet.kind == 1 && packet.nonce == _nonce &&
                        !string.IsNullOrEmpty(packet.id) && packet.id.Length <= 64 && packet.port > 0 && packet.port <= 65535)
                    {
                        var room = _rooms.Find(x => x.Id == packet.id);
                        if (room == null)
                        {
                            if (_rooms.Count >= 32) continue;
                            room = new Room { Id = packet.id }; _rooms.Add(room);
                        }
                        room.Address = endpoint.Address.ToString();
                        room.Name = string.IsNullOrEmpty(packet.name) ? "局域网房间" : packet.name.Substring(0, Math.Min(packet.name.Length, 40));
                        room.Compatible = packet.protocol == Session.Config.ProtocolVersion && packet.content == Session.Config.ContentVersion && packet.port == Session.Config.Port;
                        room.LastSeen = Time.unscaledTime;
                        Status = "发现 " + _rooms.Count + " 个房间，点击加入。";
                        Changed?.Invoke();
                    }
                }
                if (_rooms.RemoveAll(x => Time.unscaledTime - x.LastSeen > 5f) > 0)
                {
                    if (_rooms.Count == 0) Status = "暂未发现房间。请确认同一 Wi-Fi／热点；也可使用手动地址。";
                    Changed?.Invoke();
                }
            }
            catch (SocketException e)
            {
                // Windows can report ICMP port-unreachable when a queried peer closes its socket.
                // A missing room is not a failure of our own discovery socket.
                if (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.MessageSize ||
                    e.SocketErrorCode == SocketError.ConnectionReset || e.SocketErrorCode == SocketError.ConnectionRefused) return;
                Fail();
            }
            catch (ObjectDisposedException) { Close(); }
        }
        private void Open(bool host)
        {
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.EnableBroadcast = true; _socket.Blocking = false;
                _socket.Bind(new IPEndPoint(IPAddress.Any, host ? DiscoveryPort : 0));
                if (host) Status = "房间已向同一局域网开放。";
                else if (_destinations.Count == 0) BuildDestinations();
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    _wifiLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "DeepSleepLanDiscovery");
                    _wifiLock.Call("setReferenceCounted", false); _wifiLock.Call("acquire");
                }
#endif
            }
            catch (Exception e) when (e is SocketException || e is PlatformNotSupportedException || e is AndroidJavaException)
            { Fail(); }
        }
        private void BuildDestinations()
        {
            _destinations.Clear();
            _destinations.Add(new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            _destinations.Add(new IPEndPoint(IPAddress.Loopback, DiscoveryPort));
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    foreach (var address in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null) continue;
                        var ip = address.Address.GetAddressBytes(); var mask = address.IPv4Mask.GetAddressBytes();
                        for (int i = 0; i < 4; i++) ip[i] |= (byte)~mask[i];
                        var endpoint = new IPEndPoint(new IPAddress(ip), DiscoveryPort);
                        if (!_destinations.Contains(endpoint)) _destinations.Add(endpoint);
                    }
                }
            }
            catch (Exception e) when (e is NetworkInformationException || e is PlatformNotSupportedException || e is NotImplementedException) { }
        }
        private void Fail()
        {
            Close(); _retryAt = Time.unscaledTime + 5;
            Status = "自动发现暂不可用，可展开手动地址加入；检查局域网权限和防火墙。";
            Changed?.Invoke();
        }
        private void Close()
        {
            _socket?.Dispose(); _socket = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_wifiLock != null) { _wifiLock.Call("release"); _wifiLock.Dispose(); _wifiLock = null; }
#endif
        }
        private void OnDisable() { Close(); _rooms.Clear(); }
        private void OnDestroy() => Close();
    }
}
