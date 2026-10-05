using System;
using System.Collections;
using System.IO;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>房间与槽位控制权。角色/伤害/技能仍由原有分发器执行。</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CoopSessionController : MonoBehaviour, ISessionService, ISceneExitParticipant
    {
        private enum Message : byte
        {
            Welcome = NetworkMessageCatalog.Authority.Welcome,
            Ready = NetworkMessageCatalog.Peer.Ready,
            Start = NetworkMessageCatalog.Authority.Start,
            Control = NetworkMessageCatalog.Peer.Control,
            Input = NetworkMessageCatalog.Peer.Input,
            Room = NetworkMessageCatalog.Authority.Room
        }
        public MonoBehaviour TransportComponent;
        public NetworkTuningConfig Config;
        public PlayerControlAssignment Assignment;
        public OpeningCharacterSelectionController Selection;
        public LevelSceneBindings LevelBindings;
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
        private bool _soloAi;
        private bool _exiting;
        private string _levelStartRejection;
        private GameSceneRouter _sceneRouter;
        private MemoryStream _sendBuffer;
        private BinaryWriter _sendWriter;
        private bool _sendBufferInUse;
        private Action<byte, BinaryReader> _cachedAuthorityHandlers, _cachedPeerHandlers;
        private Delegate[] _authorityInvocationSnapshot = Array.Empty<Delegate>();
        private Delegate[] _peerInvocationSnapshot = Array.Empty<Delegate>();
        public NetworkSessionDiagnostics Diagnostics { get; } = new NetworkSessionDiagnostics();
        public GameObject SceneExitRoot => gameObject;
        public bool IsExiting => _exiting;
        public bool IsSoloPlaying => !_exiting && Phase == SessionPhase.Offline && Selection != null && Selection.IsSelectionComplete &&
            AppFlow.GameAppRoot.Instance != null && AppFlow.GameAppRoot.Instance.LaunchContext.Mode == AppFlow.GameLaunchMode.Solo;
        public bool CanControlLocally => !_exiting && (IsSoloPlaying || Phase == SessionPhase.Playing);

        public SessionPhase Phase { get; private set; }
        public bool IsAuthority => _transport != null && _transport.IsServer;
        public PlayerRole LocalRole { get; private set; }
        public PlayerRole HostRole { get; private set; }
        public bool HasPeer => _hasPeer;
        public bool LocalReady => IsAuthority ? _hostReady : _guestReady;
        public bool HostReady => _hostReady;
        public bool GuestReady => _guestReady;
        public bool LocalAi => IsSoloPlaying ? _soloAi : (IsAuthority ? _hostControl : _guestControl) != SlotControl.Human;
        public SlotControl GuestControl => _guestControl;
        public SlotControl GetRoleControl(PlayerRole role)
        {
            if (IsSoloPlaying)
                return role == Assignment.CurrentLocalPlayerRole && !_soloAi ? SlotControl.Human : SlotControl.VoluntaryAi;
            return role == HostRole ? _hostControl : _guestControl;
        }

        public bool IsRoleDisconnected(PlayerRole role)
        {
            return Phase == SessionPhase.Playing &&
                GetRoleControl(role) == SlotControl.DisconnectedAi;
        }
        public uint LastGuestCommand => _remote?.LastConsumedSequence ?? 0;
        public void SetDevelopmentInput(ICommandSource source)
        { if (Debug.isDebugBuild) _input = source ?? _input; }
        public void InterruptConnectionForDevelopmentTest()
        { if (Debug.isDebugBuild && !IsAuthority) { _transport.Stop(); OnDisconnected(0); } }
        public string Status { get; private set; } = "Offline";
        public event Action Changed;
        public event Action<bool> SessionOpened;
        public event Action SessionClosed;
        /// <summary>仅 Router 真正退出本关时通知玩法取消；普通离房/离线切换不等同于退出关卡。</summary>
        public event Action SceneExitStarted;
        public event Action PlayingStarted;
        public event Action<byte, BinaryReader> AuthorityMessage;
        public event Action<byte, BinaryReader> PeerMessage;
        public event Action PeerJoined;
        public event Action<PlayerCommand> LocalCommandSent;

        private void Awake()
        {
            _transport = TransportComponent as ITransportAdapter; _input = LocalInput as ICommandSource;
            if (_transport == null || _input == null || Config == null || !Config.IsValid || Assignment == null ||
                Selection == null || LevelBindings == null || DeepSeek == null || Harness == null || DeepSeekAi == null || HarnessAi == null)
            { Debug.LogError("[CoopSessionController] 联机装配不完整。", this); enabled = false; return; }
            if (Config.ProtocolVersion != NetworkMessageCatalog.ProtocolVersion)
            {
                Debug.LogError("[CoopSessionController] 网络配置协议版本与消息目录不一致，请通过当前网络版本入口更新配置。", this);
                enabled = false;
                return;
            }
            _remote = new RemoteCommandSource(Config.MaximumQueuedCommands, Config.InputTimeout);
            _previousBackground = Application.runInBackground;
            _transport.Connected += OnConnected; _transport.Disconnected += OnDisconnected;
            _transport.Received += Receive;
            if (GameAppRoot.Instance != null)
            {
                _sceneRouter = GameAppRoot.Instance.SceneRouter;
                _sceneRouter.RegisterSceneExitParticipant(this);
            }
        }

        private void OnDestroy()
        {
            _sendWriter?.Dispose();
            _sendBuffer?.Dispose();
            if (_sceneRouter != null) _sceneRouter.UnregisterSceneExitParticipant(this);
            if (_transport == null) return;
            _transport.Connected -= OnConnected; _transport.Disconnected -= OnDisconnected;
            _transport.Received -= Receive;
            Application.runInBackground = _previousBackground;
        }

        public bool Create(PlayerRole role)
        {
            if (_exiting || (_sceneRouter != null && _sceneRouter.IsTransitioning) || !enabled || Phase != SessionPhase.Offline || (byte)role > 1) return false;
            if (Selection != null && Selection.IsSelectionComplete) { Status = "Return to opening menu before creating an online match"; Changed?.Invoke(); return false; }
            if (!TryPrepareTransportLevel()) return false;
            HostRole = LocalRole = role; _hostControl = SlotControl.Human; _guestControl = SlotControl.DisconnectedAi;
            _hostReady = _guestReady = false; _hasPeer = false;
            if (!_transport.StartHost()) { Status = "Host could not start"; Changed?.Invoke(); return false; }
            if (!PrepareSelection(role))
            {
                CloseSession(false);
                Status = "Level selection was rejected; host was stopped";
                Changed?.Invoke();
                return false;
            }
            Phase = SessionPhase.Lobby; SessionOpened?.Invoke(true); Time.timeScale = 0;
            Application.runInBackground = true;
            Status = "Host ready. Waiting for guest"; Changed?.Invoke(); return true;
        }

        public bool Join(string address)
        {
            if (_exiting || (_sceneRouter != null && _sceneRouter.IsTransitioning) || !enabled || (Phase != SessionPhase.Offline && Phase != SessionPhase.Disconnected)) return false;
            if (Phase == SessionPhase.Offline && Selection != null && Selection.IsSelectionComplete)
            { Status = "Return to opening menu before joining an online match"; Changed?.Invoke(); return false; }
            if (!TryPrepareTransportLevel()) return false;
            if (!_transport.StartClient(address)) { Status = "Invalid address or transport unavailable"; Changed?.Invoke(); return false; }
            Phase = SessionPhase.Connecting; _connectStarted = Time.unscaledTime;
            Application.runInBackground = true;
            SessionOpened?.Invoke(false); Status = "Connecting"; Changed?.Invoke(); return true;
        }

        private bool PrepareSelection(PlayerRole role)
        {
            if (!Selection.IsSelectionComplete) return Selection.TrySelect(role);
            return Assignment.TrySelectLocalPlayerRole(role);
        }

        private bool TryValidateLevelStart() => TryGetLevelId(out _);

        private bool TryPrepareTransportLevel()
        {
            if (!TryGetLevelId(out string levelId)) return false;
            if (_transport is not ITransportLevelScope scope)
            {
                Status = $"Cannot start level {levelId}: transport is missing ITransportLevelScope";
                _levelStartRejection = Status;
                Time.timeScale = 0f;
                Changed?.Invoke();
                return false;
            }
            if (!scope.TrySetLevelId(levelId))
            {
                string transportReason = _transport.DisconnectReason;
                Status = $"Cannot start level {levelId}: transport rejected the level scope" +
                    (string.IsNullOrEmpty(transportReason) ? "; leave the previous connection before retrying" : ": " + transportReason);
                _levelStartRejection = Status;
                Time.timeScale = 0f;
                Changed?.Invoke();
                return false;
            }
            return true;
        }

        private bool TryGetLevelId(out string levelId)
        {
            levelId = null;
            string reason = "Missing or mismatched level binding";
            if (LevelBindings != null && LevelBindings.Session == this &&
                LevelBindings.ChapterRun != null && LevelBindings.ChapterRun.LevelBindings == LevelBindings &&
                LevelBindings.ChapterRun.TryValidateLevelStart(out reason))
            {
                levelId = LevelBindings.Level.LevelId;
                _levelStartRejection = null;
                return true;
            }
            Status = "Cannot start level: " + reason;
            _levelStartRejection = Status;
            Time.timeScale = 0f;
            Changed?.Invoke();
            return false;
        }

        private void RejectIncomingLevelStart()
        {
            // 客机已连接后发现关卡错绑，停止连接但保留离房/返回菜单入口与具体错误。
            string rejection = Status;
            _levelStartRejection = rejection;
            _transport.Stop();
            _hasPeer = false;
            Phase = SessionPhase.Disconnected;
            Time.timeScale = 0f;
            Status = rejection;
            Changed?.Invoke();
        }

        private void OnConnected(ulong id)
        {
            if (_exiting || !IsAuthority || id == 0) return;
            if (!TryGetLevelId(out string levelId)) { RejectIncomingLevelStart(); return; }
            _peer = id; _hasPeer = true; _guestReady = false;
            _guestControl = SlotControl.Human; _epoch++; _remote.Clear();
            Send(Message.Welcome, w =>
            {
                w.Write((byte)HostRole); w.Write(_epoch); w.Write(Phase == SessionPhase.Playing);
                w.Write(levelId);
            });
            if (Phase == SessionPhase.Playing) BindSources();
            BroadcastRoom(); PeerJoined?.Invoke();
            Diagnostics.Event("Peer connected; phase=" + Phase + ", host=" + HostRole);
        }

        private void OnDisconnected(ulong id)
        {
            if (_exiting || Phase == SessionPhase.Offline) return;
            Diagnostics.Event("Disconnected; authority=" + IsAuthority + ", phase=" + Phase);
            if (IsAuthority && _hasPeer && id == _peer)
            {
                _hasPeer = false; _guestReady = false; _guestControl = SlotControl.DisconnectedAi;
                _epoch++; _remote.Clear();
                if (Phase == SessionPhase.Playing) BindSources();
                Status = "Guest disconnected. AI has control"; Changed?.Invoke();
            }
            else if (!IsAuthority || id == 0)
            {
                Phase = SessionPhase.Disconnected;
                Status = !string.IsNullOrEmpty(_levelStartRejection) ? _levelStartRejection :
                    string.IsNullOrEmpty(_transport.DisconnectReason)
                        ? "Host connection lost. Join again to reconnect" : _transport.DisconnectReason;
                Time.timeScale = 0; Changed?.Invoke();
            }
        }

        public void SetReady(bool ready)
        {
            if (_exiting || Phase != SessionPhase.Lobby) return;
            if (ready && !TryValidateLevelStart()) return;
            if (IsAuthority) { _hostReady = ready; BroadcastRoom(); TryStart(); }
            else Send(Message.Ready, w => w.Write(ready));
        }

        private void TryStart()
        {
            if (!_hasPeer || !_hostReady || !_guestReady) return;
            if (!TryValidateLevelStart()) return;
            Phase = SessionPhase.Playing; BindSources();
            Send(Message.Start, null); Time.timeScale = 1;
            Status = "Online"; PlayingStarted?.Invoke(); Changed?.Invoke();
        }

        public void SetLocalAi(bool value)
        {
            if (_exiting) return;
            Diagnostics.Event("Local AI=" + value + ", phase=" + Phase + ", role=" + LocalRole);
            if (IsSoloPlaying)
            {
                if (_soloAi == value) return;
                var brain = Assignment.CurrentLocalPlayerRole == PlayerRole.DeepSeek ? DeepSeekAi : HarnessAi;
                brain.ReleaseControl();
                if (value) brain.ResetIntent();
                if (Assignment.CurrentLocalPlayerActor.CommandDispatcher.TryBindCommandSource(value ? brain : _input))
                    _soloAi = value;
                Changed?.Invoke();
                return;
            }
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
            if (_exiting || Phase != SessionPhase.Playing || IsAuthority || LocalAi) return;
            if (_input.TryGetCommand(++_inputTick, out var command))
            {
                Send(Message.Input, w => { w.Write(_epoch); NetworkCommandCodec.Write(w, command); });
                LocalCommandSent?.Invoke(command);
            }
        }

        private void Update()
        {
            if (!_exiting && Phase == SessionPhase.Connecting && Time.unscaledTime - _connectStarted > Config.ConnectionTimeout)
            {
                _transport.Stop(); Phase = SessionPhase.Disconnected;
                Status = "Connection timed out. Check host, address and UDP port"; Changed?.Invoke();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // 客人进入后台前请求托管；若请求未到达，断线事件仍兜底接管。
            if (paused && AutoTakeoverOnFocusLoss && CanControlLocally) SetLocalAi(true);
        }
        private void OnApplicationFocus(bool focused)
        { if (!focused && AutoTakeoverOnFocusLoss && CanControlLocally) SetLocalAi(true); }

        public void Leave()
        {
            if (_exiting) return;
            CloseSession(true);
        }

        private void CloseSession(bool restoreLocalControl)
        {
            if (IsSoloPlaying) { SetLocalAi(false); LocalRole = Assignment.CurrentLocalPlayerRole; }
            _soloAi = false;
            _levelStartRejection = null;
            Phase = SessionPhase.Offline; _transport?.Stop(); _hasPeer = false; _remote?.Clear();
            SessionClosed?.Invoke();
            if (restoreLocalControl && Assignment != null) Assignment.TrySelectLocalPlayerRole(LocalRole);
            Time.timeScale = restoreLocalControl && Selection != null && Selection.IsSelectionComplete &&
                LevelBindings != null && LevelBindings.ChapterRun != null &&
                LevelBindings.ChapterRun.TryValidateLevelStart(out _) ? 1f : 0f;
            Status = "Offline"; Changed?.Invoke();
            Application.runInBackground = _previousBackground;
            Diagnostics.Event("Session closed; restoreLocalControl=" + restoreLocalControl);
            LogDiagnostics();
        }

        [ContextMenu("Log network session diagnostics")]
        public void LogDiagnostics() => Debug.Log("[NetworkSessionDiagnostics] " + Diagnostics.Describe(), this);

        public IEnumerator PrepareForSceneExit()
        {
            if (!_exiting)
            {
                _exiting = true;
                Diagnostics.Event("Scene exit started");
                SceneExitStarted?.Invoke();
                CloseSession(false);
                if (DeepSeekAi != null) DeepSeekAi.ReleaseControl();
                if (HarnessAi != null) HarnessAi.ReleaseControl();
            }
            Time.timeScale = 0;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (_transport is ITransportShutdownStatus shutdown ? !shutdown.IsShutdownComplete :
                _transport != null && _transport.IsConnected)
            {
                if (Time.realtimeSinceStartup >= deadline)
                    throw new TimeoutException("Network shutdown timed out; scene transition cancelled.");
                yield return null;
            }
            Diagnostics.Event("Scene exit transport stopped");
        }

        private void BroadcastRoom()
        {
            Send(Message.Room, w => { w.Write(_hostReady); w.Write(_guestReady);
                w.Write((byte)_hostControl); w.Write((byte)_guestControl); w.Write(_epoch); });
            Changed?.Invoke();
        }

        private void Receive(ulong sender, byte[] bytes)
        {
            if (_exiting) return;
            if (bytes == null || bytes.Length == 0 || bytes.Length > Config.MaximumMessageBytes)
            { Diagnostics.Rejected(bytes != null && bytes.Length > 0 ? bytes[0] : (byte)0, "message size"); return; }
            if (IsAuthority ? !_hasPeer || sender != _peer : sender != 0) return;
            var direction = IsAuthority ? NetworkMessageCatalog.Direction.PeerToAuthority : NetworkMessageCatalog.Direction.AuthorityToPeer;
            if (!NetworkMessageCatalog.TryValidatePacket(bytes, direction, out string invalid))
            { Diagnostics.Rejected(bytes[0], invalid); return; }
            Diagnostics.Received(bytes[0]);
            try
            {
                using var stream = new MemoryStream(bytes, false); using var r = new BinaryReader(stream);
                byte kind = r.ReadByte();
                if (kind >= 32)
                {
                    // 委托对象改变时才重建快照；本包仍按原订阅顺序执行，订阅增删到下一包生效。
                    // 局部快照也保证嵌套 Receive 不会替换外层正在遍历的列表。
                    Delegate[] handlers = GetInvocationSnapshot(IsAuthority);
                    foreach (Action<byte, BinaryReader> handler in handlers)
                    {
                        stream.Position = 1;
                        try { handler(kind, r); }
                        catch (EndOfStreamException) { Diagnostics.Rejected(kind, "handler truncated read"); }
                        catch (InvalidDataException) { Diagnostics.Rejected(kind, "handler invalid fields"); }
                        catch (IOException) { Diagnostics.Rejected(kind, "handler malformed read"); }
                        catch (Exception exception)
                        {
                            // 单一模块故障不能中断其它独立订阅者；保留错误，不能假装成功。
                            Diagnostics.HandlerFault(kind, handler.Method.DeclaringType?.Name);
                            if (Diagnostics.HandlerFaults <= 8) Debug.LogException(exception, this);
                        }
                    }
                    return;
                }
                switch ((Message)kind)
                {
                    case Message.Welcome when !IsAuthority:
                        var hostRole = (PlayerRole)r.ReadByte(); if ((byte)hostRole > 1) return;
                        uint welcomeEpoch = r.ReadUInt32(); bool playing = r.ReadBoolean();
                        string remoteLevelId = r.ReadString();
                        if (!TryGetLevelId(out string localLevelId)) { RejectIncomingLevelStart(); break; }
                        if (!string.Equals(remoteLevelId, localLevelId, StringComparison.Ordinal))
                        {
                            Status = $"关卡不一致：房主为 {remoteLevelId}，本机为 {localLevelId}。请返回关卡选择并进入同一关卡。";
                            RejectIncomingLevelStart();
                            break;
                        }
                        HostRole = hostRole;
                        LocalRole = HostRole == PlayerRole.DeepSeek ? PlayerRole.Harness : PlayerRole.DeepSeek;
                        _epoch = welcomeEpoch; _hasPeer = true;
                        if (!PrepareSelection(LocalRole))
                        {
                            Status = "Level selection was rejected";
                            RejectIncomingLevelStart();
                            break;
                        }
                        Phase = playing ? SessionPhase.Playing : SessionPhase.Lobby;
                        Time.timeScale = playing ? 1 : 0; Status = playing ? "Online" : "Choose Ready";
                        if (playing) PlayingStarted?.Invoke(); Changed?.Invoke(); break;
                    case Message.Ready when IsAuthority && Phase == SessionPhase.Lobby:
                        _guestReady = r.ReadBoolean(); BroadcastRoom(); TryStart(); break;
                    case Message.Start when !IsAuthority:
                        if (!TryValidateLevelStart()) { RejectIncomingLevelStart(); break; }
                        if (!Selection.IsSelectionComplete)
                        {
                            Status = "Level selection has not completed";
                            RejectIncomingLevelStart();
                            break;
                        }
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
                        bool hostReady = r.ReadBoolean(), guestReady = r.ReadBoolean();
                        var hostControl = (SlotControl)r.ReadByte(); var guestControl = (SlotControl)r.ReadByte();
                        uint roomEpoch = r.ReadUInt32();
                        if (hostControl > SlotControl.DisconnectedAi || guestControl > SlotControl.DisconnectedAi) return;
                        _hostReady = hostReady; _guestReady = guestReady;
                        _hostControl = hostControl; _guestControl = guestControl;
                        _epoch = roomEpoch; Changed?.Invoke(); break;
                }
            }
            catch (EndOfStreamException) { Diagnostics.Rejected(bytes[0], "truncated core packet"); }
            catch (IOException) { Diagnostics.Rejected(bytes[0], "malformed core packet"); }
        }

        private void Send(Message kind, Action<BinaryWriter> write)
        {
            if (_exiting) return;
            // 无对端时也保留原先执行写入回调的语义，但不制造待发送数组。
            byte[] packet = Serialize((byte)kind, write, !IsAuthority || _hasPeer);
            if (packet != null) SendPacket(IsAuthority ? _peer : 0, packet, true);
        }

        public void SendAuthority(byte kind, Action<BinaryWriter> write, bool reliable)
        {
            if (_exiting || !IsAuthority || !_hasPeer || kind < 32) return;
            SendPacket(_peer, Serialize(kind, write, true), reliable);
        }

        public void SendToAuthority(
            byte kind,
            Action<BinaryWriter> write,
            bool reliable)
        {
            if (_exiting || IsAuthority || !_hasPeer || kind < 32) return;
            SendPacket(0, Serialize(kind, write, true), reliable);
        }

        private byte[] Serialize(byte kind, Action<BinaryWriter> write, bool copyPacket)
        {
            if (_sendBufferInUse)
            {
                // 通常通道只同步写字段；仍保留嵌套 Send 的独立流和原有发送顺序。
                using var nestedStream = new MemoryStream(); using var nestedWriter = new NetworkBinaryWriter(nestedStream);
                nestedWriter.Write(kind); write?.Invoke(nestedWriter);
                return copyPacket ? nestedStream.ToArray() : null;
            }
            _sendBufferInUse = true;
            try
            {
                if (_sendBuffer == null)
                { _sendBuffer = new MemoryStream(); _sendWriter = new NetworkBinaryWriter(_sendBuffer); }
                _sendBuffer.SetLength(0); _sendBuffer.Position = 0;
                _sendWriter.Write(kind); write?.Invoke(_sendWriter);
                // Transport 接口拥有独立 byte[]；只复用工作区，绝不将可变内部缓冲交给传输。
                return copyPacket ? _sendBuffer.ToArray() : null;
            }
            finally { _sendBufferInUse = false; }
        }

        private Delegate[] GetInvocationSnapshot(bool authority)
        {
            if (authority)
            {
                Action<byte, BinaryReader> handlers = PeerMessage;
                if (!ReferenceEquals(handlers, _cachedPeerHandlers))
                {
                    _cachedPeerHandlers = handlers;
                    _peerInvocationSnapshot = handlers?.GetInvocationList() ?? Array.Empty<Delegate>();
                }
                return _peerInvocationSnapshot;
            }
            else
            {
                Action<byte, BinaryReader> handlers = AuthorityMessage;
                if (!ReferenceEquals(handlers, _cachedAuthorityHandlers))
                {
                    _cachedAuthorityHandlers = handlers;
                    _authorityInvocationSnapshot = handlers?.GetInvocationList() ?? Array.Empty<Delegate>();
                }
                return _authorityInvocationSnapshot;
            }
        }

        private void SendPacket(ulong recipient, byte[] packet, bool reliable)
        {
            var direction = IsAuthority ? NetworkMessageCatalog.Direction.AuthorityToPeer : NetworkMessageCatalog.Direction.PeerToAuthority;
            if (packet.Length > Config.MaximumMessageBytes ||
                !NetworkMessageCatalog.TryValidatePacket(packet, direction, out _))
            {
                bool first = Diagnostics.RejectedCount(packet[0]) == 0;
                Diagnostics.Rejected(packet[0], "outgoing packet does not match catalog");
                if (first) Debug.LogError("[CoopSessionController] 发送消息与协议目录不匹配：" + packet[0], this);
                return;
            }
            _transport.Send(recipient, packet, reliable);
            Diagnostics.Sent(packet[0]);
        }
    }
}
