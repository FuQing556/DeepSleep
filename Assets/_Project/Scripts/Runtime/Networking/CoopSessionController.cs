using System;
using System.IO;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>房间与槽位控制权。角色/伤害/技能仍由原有分发器执行。</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CoopSessionController : MonoBehaviour, ISessionService
    {
        private enum Message : byte { Welcome = 1, Ready = 2, Start = 3, Control = 4, Input = 5, Room = 6 }
        public MonoBehaviour TransportComponent;
        public NetworkTuningConfig Config;
        public PlayerControlAssignment Assignment;
        public OpeningCharacterSelectionController Selection;
        public MonoBehaviour LocalInput;
        public PlayerActor DeepSeek, Harness;
        public CompanionCommandSource2D DeepSeekAi, HarnessAi;
        private ITransportAdapter _transport;
        private ICommandSource _input;
        private RemoteCommandSource _remote;
        private ulong _peer;
        private bool _hasPeer, _hostReady, _guestReady;
        private uint _epoch, _inputTick;
        private float _connectStarted;
        private SlotControl _hostControl, _guestControl;
        private ICommandSource _boundHost, _boundGuest;
        public bool AutoTakeoverOnFocusLoss = true;
        private bool _previousBackground;

        public SessionPhase Phase { get; private set; }
        public bool IsAuthority => _transport != null && _transport.IsServer;
        public PlayerRole LocalRole { get; private set; }
        public PlayerRole HostRole { get; private set; }
        public bool HasPeer => _hasPeer;
        public bool LocalReady => IsAuthority ? _hostReady : _guestReady;
        public bool HostReady => _hostReady;
        public bool GuestReady => _guestReady;
        public bool LocalAi => (IsAuthority ? _hostControl : _guestControl) != SlotControl.Human;
        public SlotControl GuestControl => _guestControl;
        public uint LastGuestCommand => _remote?.LastConsumedSequence ?? 0;
        public void SetDevelopmentInput(ICommandSource source)
        { if (Debug.isDebugBuild) _input = source ?? _input; }
        public void InterruptConnectionForDevelopmentTest()
        { if (Debug.isDebugBuild && !IsAuthority) { _transport.Stop(); OnDisconnected(0); } }
        public string Status { get; private set; } = "Offline";
        public event Action Changed;
        public event Action<bool> SessionOpened;
        public event Action SessionClosed;
        public event Action PlayingStarted;
        public event Action<byte, BinaryReader> AuthorityMessage;
        public event Action PeerJoined;
        public event Action<PlayerCommand> LocalCommandSent;

        private void Awake()
        {
            _transport = TransportComponent as ITransportAdapter; _input = LocalInput as ICommandSource;
            if (_transport == null || _input == null || Config == null || !Config.IsValid || Assignment == null ||
                Selection == null || DeepSeek == null || Harness == null || DeepSeekAi == null || HarnessAi == null)
            { Debug.LogError("[CoopSessionController] 联机装配不完整。", this); enabled = false; return; }
            _remote = new RemoteCommandSource(Config.MaximumQueuedCommands, Config.InputTimeout);
            _previousBackground = Application.runInBackground;
            _transport.Connected += OnConnected; _transport.Disconnected += OnDisconnected;
            _transport.Received += Receive;
        }

        private void OnDestroy()
        {
            if (_transport == null) return;
            _transport.Connected -= OnConnected; _transport.Disconnected -= OnDisconnected;
            _transport.Received -= Receive;
            Application.runInBackground = _previousBackground;
        }

        public bool Create(PlayerRole role)
        {
            if (!enabled || Phase != SessionPhase.Offline || (byte)role > 1) return false;
            if (Selection.IsSelectionComplete) { Status = "Return to opening menu before creating an online match"; Changed?.Invoke(); return false; }
            HostRole = LocalRole = role; _hostControl = SlotControl.Human; _guestControl = SlotControl.DisconnectedAi;
            _hostReady = _guestReady = false; _hasPeer = false;
            if (!_transport.StartHost()) { Status = "Host could not start"; Changed?.Invoke(); return false; }
            PrepareSelection(role);
            Phase = SessionPhase.Lobby; SessionOpened?.Invoke(true); Time.timeScale = 0;
            Application.runInBackground = true;
            Status = "Host ready. Waiting for guest"; Changed?.Invoke(); return true;
        }

        public bool Join(string address)
        {
            if (!enabled || (Phase != SessionPhase.Offline && Phase != SessionPhase.Disconnected)) return false;
            if (Phase == SessionPhase.Offline && Selection.IsSelectionComplete)
            { Status = "Return to opening menu before joining an online match"; Changed?.Invoke(); return false; }
            if (!_transport.StartClient(address)) { Status = "Invalid address or transport unavailable"; Changed?.Invoke(); return false; }
            Phase = SessionPhase.Connecting; _connectStarted = Time.unscaledTime;
            Application.runInBackground = true;
            SessionOpened?.Invoke(false); Status = "Connecting"; Changed?.Invoke(); return true;
        }

        private void PrepareSelection(PlayerRole role)
        {
            if (!Selection.IsSelectionComplete) Selection.TrySelect(role);
            else Assignment.TrySelectLocalPlayerRole(role);
        }

        private void OnConnected(ulong id)
        {
            if (!IsAuthority || id == 0) return;
            _peer = id; _hasPeer = true; _guestReady = false;
            _guestControl = SlotControl.Human; _epoch++; _remote.Clear();
            Send(Message.Welcome, w => { w.Write((byte)HostRole); w.Write(_epoch); w.Write(Phase == SessionPhase.Playing); });
            if (Phase == SessionPhase.Playing) BindSources();
            BroadcastRoom(); PeerJoined?.Invoke();
        }

        private void OnDisconnected(ulong id)
        {
            if (Phase == SessionPhase.Offline) return;
            if (IsAuthority && _hasPeer && id == _peer)
            {
                _hasPeer = false; _guestReady = false; _guestControl = SlotControl.DisconnectedAi;
                _epoch++; _remote.Clear();
                if (Phase == SessionPhase.Playing) BindSources();
                Status = "Guest disconnected. AI has control"; Changed?.Invoke();
            }
            else if (!IsAuthority || id == 0)
            {
                Phase = SessionPhase.Disconnected; Status = string.IsNullOrEmpty(_transport.DisconnectReason)
                    ? "Host connection lost. Join again to reconnect" : _transport.DisconnectReason;
                Time.timeScale = 0; Changed?.Invoke();
            }
        }

        public void SetReady(bool ready)
        {
            if (Phase != SessionPhase.Lobby) return;
            if (IsAuthority) { _hostReady = ready; BroadcastRoom(); TryStart(); }
            else Send(Message.Ready, w => w.Write(ready));
        }

        private void TryStart()
        {
            if (!_hasPeer || !_hostReady || !_guestReady) return;
            Phase = SessionPhase.Playing; BindSources();
            Send(Message.Start, null); Time.timeScale = 1;
            Status = "Online"; PlayingStarted?.Invoke(); Changed?.Invoke();
        }

        public void SetLocalAi(bool value)
        {
            if (Phase != SessionPhase.Playing) return;
            if (IsAuthority)
            {
                _hostControl = value ? SlotControl.VoluntaryAi : SlotControl.Human;
                BindSources(); BroadcastRoom();
            }
            else Send(Message.Control, w => w.Write(value));
        }

        private void BindSources()
        {
            PlayerActor host = HostRole == PlayerRole.DeepSeek ? DeepSeek : Harness;
            PlayerActor guest = HostRole == PlayerRole.DeepSeek ? Harness : DeepSeek;
            var hostAi = HostRole == PlayerRole.DeepSeek ? DeepSeekAi : HarnessAi;
            var guestAi = HostRole == PlayerRole.DeepSeek ? HarnessAi : DeepSeekAi;
            ICommandSource nextHost = _hostControl == SlotControl.Human ? _input : hostAi;
            ICommandSource nextGuest = _guestControl == SlotControl.Human ? _remote : guestAi;
            if (!ReferenceEquals(_boundHost, nextHost))
            { hostAi.ReleaseControl(); if (_hostControl != SlotControl.Human) hostAi.ResetIntent(); _boundHost = nextHost; }
            if (!ReferenceEquals(_boundGuest, nextGuest))
            { guestAi.ReleaseControl(); if (_guestControl != SlotControl.Human) guestAi.ResetIntent(); _boundGuest = nextGuest; }
            host.CommandDispatcher.TryBindCommandSource(nextHost);
            guest.CommandDispatcher.TryBindCommandSource(nextGuest);
        }

        private void FixedUpdate()
        {
            if (Phase != SessionPhase.Playing || IsAuthority || LocalAi) return;
            if (_input.TryGetCommand(++_inputTick, out var command))
            {
                Send(Message.Input, w => { w.Write(_epoch); NetworkCommandCodec.Write(w, command); });
                LocalCommandSent?.Invoke(command);
            }
        }

        private void Update()
        {
            if (Phase == SessionPhase.Connecting && Time.unscaledTime - _connectStarted > Config.ConnectionTimeout)
            {
                _transport.Stop(); Phase = SessionPhase.Disconnected;
                Status = "Connection timed out. Check host, address and UDP port"; Changed?.Invoke();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // 客人进入后台前请求托管；若请求未到达，断线事件仍兜底接管。
            if (paused && AutoTakeoverOnFocusLoss && Phase == SessionPhase.Playing) SetLocalAi(true);
        }
        private void OnApplicationFocus(bool focused)
        { if (!focused && AutoTakeoverOnFocusLoss && Phase == SessionPhase.Playing) SetLocalAi(true); }

        public void Leave()
        {
            Phase = SessionPhase.Offline; _transport.Stop(); _hasPeer = false; _remote.Clear();
            SessionClosed?.Invoke(); Assignment.TrySelectLocalPlayerRole(LocalRole);
            Time.timeScale = 1; Status = "Offline"; Changed?.Invoke();
            Application.runInBackground = _previousBackground;
        }

        private void BroadcastRoom()
        {
            Send(Message.Room, w => { w.Write(_hostReady); w.Write(_guestReady);
                w.Write((byte)_hostControl); w.Write((byte)_guestControl); w.Write(_epoch); });
            Changed?.Invoke();
        }

        private void Receive(ulong sender, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > Config.MaximumMessageBytes) return;
            if (IsAuthority ? !_hasPeer || sender != _peer : sender != 0) return;
            try
            {
                using var stream = new MemoryStream(bytes, false); using var r = new BinaryReader(stream);
                byte kind = r.ReadByte();
                if (kind >= 32)
                {
                    if (!IsAuthority && AuthorityMessage != null)
                        foreach (Action<byte, BinaryReader> handler in AuthorityMessage.GetInvocationList())
                        { stream.Position = 1; handler(kind, r); }
                    return;
                }
                switch ((Message)kind)
                {
                    case Message.Welcome when !IsAuthority:
                        HostRole = (PlayerRole)r.ReadByte(); if ((byte)HostRole > 1) return;
                        LocalRole = HostRole == PlayerRole.DeepSeek ? PlayerRole.Harness : PlayerRole.DeepSeek;
                        _epoch = r.ReadUInt32(); bool playing = r.ReadBoolean(); _hasPeer = true;
                        PrepareSelection(LocalRole); Phase = playing ? SessionPhase.Playing : SessionPhase.Lobby;
                        Time.timeScale = playing ? 1 : 0; Status = playing ? "Online" : "Choose Ready";
                        if (playing) PlayingStarted?.Invoke(); Changed?.Invoke(); break;
                    case Message.Ready when IsAuthority && Phase == SessionPhase.Lobby:
                        _guestReady = r.ReadBoolean(); BroadcastRoom(); TryStart(); break;
                    case Message.Start when !IsAuthority:
                        Phase = SessionPhase.Playing; Time.timeScale = 1; Status = "Online";
                        PlayingStarted?.Invoke(); Changed?.Invoke(); break;
                    case Message.Control when IsAuthority && Phase == SessionPhase.Playing:
                        _guestControl = r.ReadBoolean() ? SlotControl.VoluntaryAi : SlotControl.Human;
                        _epoch++; _remote.Clear(); BindSources(); BroadcastRoom(); break;
                    case Message.Input when IsAuthority && Phase == SessionPhase.Playing && _guestControl == SlotControl.Human:
                        uint epoch = r.ReadUInt32();
                        if (epoch == _epoch && NetworkCommandCodec.TryRead(r, out var command) && stream.Position == stream.Length)
                        {
                            var guestActor = HostRole == PlayerRole.DeepSeek ? Harness : DeepSeek;
                            if (guestActor.CommandDispatcher.isActiveAndEnabled) _remote.Enqueue(command, Time.unscaledTime);
                            else _remote.DiscardThrough(command.Sequence);
                        }
                        break;
                    case Message.Room when !IsAuthority:
                        _hostReady = r.ReadBoolean(); _guestReady = r.ReadBoolean();
                        _hostControl = (SlotControl)r.ReadByte(); _guestControl = (SlotControl)r.ReadByte();
                        _epoch = r.ReadUInt32(); Changed?.Invoke(); break;
                }
            }
            catch (EndOfStreamException) { /* 不完整的包没有执行权限。 */ }
            catch (IOException) { /* 损坏的帧直接拒绝。 */ }
        }

        private void Send(Message kind, Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
            w.Write((byte)kind); write?.Invoke(w);
            if (!IsAuthority || _hasPeer) _transport.Send(IsAuthority ? _peer : 0, stream.ToArray(), true);
        }

        public void SendAuthority(byte kind, Action<BinaryWriter> write, bool reliable)
        {
            if (!IsAuthority || !_hasPeer || kind < 32) return;
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
            w.Write(kind); write(w); _transport.Send(_peer, stream.ToArray(), reliable);
        }
    }
}
