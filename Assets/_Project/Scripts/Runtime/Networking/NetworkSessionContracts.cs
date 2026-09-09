using System;
using DeepSleep.Runtime.Players.Identity;

namespace DeepSleep.Runtime.Networking
{
    public enum SessionPhase : byte { Offline, Connecting, Lobby, Playing, Disconnected }
    public interface INetworkTestCapture { void Capture(string path); }
    public enum SlotControl : byte { Human, VoluntaryAi, DisconnectedAi }
    public interface IRelaySelection
    {
        bool UseRelay { get; }
        string RoomCode { get; }
        void SelectRelay(bool value, string endpoint, string room);
    }

    /// <summary>房间业务不依赖 NGO 或某家中继商。地址由传输适配器解释。</summary>
    public interface ISessionService
    {
        SessionPhase Phase { get; }
        bool IsAuthority { get; }
        PlayerRole LocalRole { get; }
        string Status { get; }
        event Action Changed;
        bool Create(PlayerRole role);
        bool Join(string address);
        void SetReady(bool ready);
        void SetLocalAi(bool enabled);
        void Leave();
    }

    public interface ITransportAdapter
    {
        bool IsServer { get; }
        bool IsConnected { get; }
        string DisconnectReason { get; }
        event Action<ulong> Connected;
        event Action<ulong> Disconnected;
        event Action<ulong, byte[]> Received;
        bool StartHost();
        bool StartClient(string address);
        void Send(ulong peer, byte[] payload, bool reliable);
        void Stop();
    }
}
