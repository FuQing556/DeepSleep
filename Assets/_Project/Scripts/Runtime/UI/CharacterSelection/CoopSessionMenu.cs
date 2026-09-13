using System.Collections;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.CharacterSelection
{
    public sealed class CoopSessionMenu : MonoBehaviour
    {
        public CoopSessionController Session;
        public LanRoomDiscovery Discovery;
        public GameObject Panel;
        public Button TogglePanel, HostDs, HostHs, JoinButton, ReadyButton, AiButton, LeaveButton;
        public InputField Address;
        public Text StatusLabel, AiLabel, ReadyLabel;
        public Button TransportMode;
        public Text TransportLabel;
        public InputField RelayEndpoint;
        public Button CopyRoom;
        public Text AddressHint;
        public string ReadyText, CancelReadyText, AiText, ControlText, LanText, RelayText, LanHint, RoomHint;
        public string LobbyTitle = "联机大厅", HostText = "房主", GuestText = "队友", WaitingText = "等待加入", NotReadyText = "未准备";
        public GameObject ConnectGroup, ManualGroup, LobbyGroup, PlayGroup, ConfirmGroup;
        public Button CloseButton, ScanButton, ManualButton, ConfirmLeave, CancelLeave, QuickAi;
        public Text TitleLabel, DiscoveryLabel, PlayHint, QuickAiLabel;
        public Button[] RoomButtons;
        public Text[] RoomLabels;
        private readonly string[] _roomAddresses = new string[32];
        private bool _wasPlaying, _relay, _manual, _ownsPause, _leaving;
        private float _previousTimeScale;
        private IRelaySelection Relay => Session.TransportComponent as IRelaySelection;
        private TouchCommandSource Touch => Session.LocalInput as TouchCommandSource;
        private bool InGame => Session.CanControlLocally;
        private void OnEnable()
        {
            TogglePanel.onClick.AddListener(Toggle);
            HostDs.onClick.AddListener(CreateDs); HostHs.onClick.AddListener(CreateHs);
            JoinButton.onClick.AddListener(Join); ReadyButton.onClick.AddListener(Ready);
            AiButton.onClick.AddListener(Ai); LeaveButton.onClick.AddListener(RequestLeave);
            TransportMode.onClick.AddListener(SwitchTransport); CopyRoom.onClick.AddListener(CopyRoomCode);
            CloseButton.onClick.AddListener(ClosePanel); ScanButton.onClick.AddListener(Scan);
            ManualButton.onClick.AddListener(ToggleManual); ConfirmLeave.onClick.AddListener(Leave);
            CancelLeave.onClick.AddListener(CancelExit); QuickAi.onClick.AddListener(Ai);
            for (int i = 0; i < RoomButtons.Length; i++)
            {
                int index = i;
                RoomButtons[i].onClick.AddListener(() => JoinDiscovered(index));
            }
            Session.Changed += Refresh; Discovery.Changed += RefreshRooms;
            _relay = Relay != null && Relay.UseRelay;
            Refresh();
        }
        private void OnDisable()
        {
            RestorePause(); Touch?.SetUiBlocked(false);
            TogglePanel.onClick.RemoveListener(Toggle);
            HostDs.onClick.RemoveListener(CreateDs); HostHs.onClick.RemoveListener(CreateHs);
            JoinButton.onClick.RemoveListener(Join); ReadyButton.onClick.RemoveListener(Ready);
            AiButton.onClick.RemoveListener(Ai); LeaveButton.onClick.RemoveListener(RequestLeave);
            TransportMode.onClick.RemoveListener(SwitchTransport); CopyRoom.onClick.RemoveListener(CopyRoomCode);
            CloseButton.onClick.RemoveListener(ClosePanel); ScanButton.onClick.RemoveListener(Scan);
            ManualButton.onClick.RemoveListener(ToggleManual); ConfirmLeave.onClick.RemoveListener(Leave);
            CancelLeave.onClick.RemoveListener(CancelExit); QuickAi.onClick.RemoveListener(Ai);
            foreach (var button in RoomButtons) button.onClick.RemoveAllListeners();
            Session.Changed -= Refresh; Discovery.Changed -= RefreshRooms;
            Discovery.SetBrowsing(false);
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && InGame)
            {
                if (ConfirmGroup.activeSelf) CancelExit(); else Toggle();
            }
        }
        private void LateUpdate()
        {
            bool blocked = Panel.activeSelf && InGame;
            Touch?.SetUiBlocked(blocked);
            if (Touch != null && InGame)
                Touch.TouchCanvas.SetActive(Touch.TouchEnabled && !blocked && !Session.LocalAi);
        }
        public void SetFrontEndState(bool room, bool playing)
        {
            TogglePanel.gameObject.SetActive(playing);
            QuickAi.gameObject.SetActive(playing);
            if (room) SetPanel(true);
            else if (!playing || !_wasPlaying) SetPanel(false);
            _wasPlaying = playing;
            Refresh();
        }
        public void Toggle() => SetPanel(!Panel.activeSelf);
        public void ClosePanel() => SetPanel(false);
        private void SetPanel(bool open)
        {
            Panel.SetActive(open);
            Touch?.SetUiBlocked(open && InGame);
            if (!open) ConfirmGroup.SetActive(false);
            if (open && Session.IsSoloPlaying && !_ownsPause)
            {
                _previousTimeScale = Time.timeScale; _ownsPause = true; Time.timeScale = 0;
            }
            else if (!open) RestorePause();
            Refresh();
        }
        private void RestorePause()
        {
            if (!_ownsPause) return;
            _ownsPause = false; Time.timeScale = _previousTimeScale;
        }
        private void Configure() => Relay?.SelectRelay(_relay, RelayEndpoint.text.Trim(), Address.text.Trim());
        private void CreateDs() { Configure(); Session.Create(PlayerRole.DeepSeek); }
        private void CreateHs() { Configure(); Session.Create(PlayerRole.Harness); }
        private void Join()
        {
            if (string.IsNullOrWhiteSpace(Address.text)) { AddressHint.text = _relay ? "请输入房间码" : "请输入房主的局域网 IPv4 地址"; return; }
            Configure(); Session.Join(Address.text.Trim());
        }
        private void JoinDiscovered(int index)
        {
            if (index >= _roomAddresses.Length || string.IsNullOrEmpty(_roomAddresses[index])) return;
            _relay = false; Address.text = _roomAddresses[index]; Join();
        }
        private void SwitchTransport()
        {
            if (Session.Phase != SessionPhase.Offline) return;
            _relay = !_relay; Address.text = ""; Refresh();
        }
        private void Scan() => Discovery.Rescan();
        private void ToggleManual() { _manual = !_manual; Refresh(); }
        private void Ready() => Session.SetReady(!Session.LocalReady);
        public void ToggleAi() => Ai();
        private void Ai()
        {
            Session.SetLocalAi(!Session.LocalAi);
            if (Panel.activeSelf && InGame) ClosePanel();
        }
        private void CopyRoomCode() { if (Relay != null) GUIUtility.systemCopyBuffer = Relay.RoomCode; }
        private void RequestLeave()
        {
            if (InGame || Session.Phase == SessionPhase.Disconnected) ConfirmGroup.SetActive(true);
            else Leave();
        }
        private void CancelExit() => ConfirmGroup.SetActive(false);
        private void Leave()
        {
            if (_leaving) return;
            _leaving = true; RestorePause(); Touch?.SetUiBlocked(false);
            Discovery.SetBrowsing(false); Session.Leave();
            StartCoroutine(ReturnToMenu());
        }
        private IEnumerator ReturnToMenu()
        {
            yield return null;
            Destroy(Session.gameObject);
            yield return null;
            GameAppRoot.Instance.SceneRouter.LoadMainMenu(MainMenuPage.ModeSelection);
        }
        private void Refresh()
        {
            if (ConnectGroup == null) return;
            bool game = InGame;
            bool offline = Session.Phase == SessionPhase.Offline;
            bool disconnected = Session.Phase == SessionPhase.Disconnected;
            bool canJoin = !game && (offline || disconnected);
            ConnectGroup.SetActive(!game && (offline || disconnected || Session.Phase == SessionPhase.Connecting));
            LobbyGroup.SetActive(!game && Session.Phase == SessionPhase.Lobby);
            PlayGroup.SetActive(game);
            TitleLabel.text = game ? "游戏菜单" : disconnected ? "连接已断开" : Session.Phase == SessionPhase.Lobby ? "房间准备" : "联机大厅";
            HostDs.interactable = HostHs.interactable = offline && !game;
            JoinButton.interactable = Address.interactable = canJoin;
            TransportMode.interactable = offline && !game;
            ReadyButton.interactable = Session.Phase == SessionPhase.Lobby;
            AiButton.interactable = game;
            CloseButton.gameObject.SetActive(game);
            RelayEndpoint.gameObject.SetActive(_relay);
            RelayEndpoint.interactable = canJoin;
            ManualGroup.SetActive(_manual || _relay);
            ScanButton.gameObject.SetActive(!_relay);
            ScanButton.interactable = canJoin;
            ManualButton.gameObject.SetActive(!_relay);
            TransportLabel.text = _relay ? "公网中继  ·  点击切换局域网" : "局域网  ·  点击切换公网中继";
            AddressHint.text = _relay ? "中继地址与房间码（两端使用同一个中继）" : "备用：输入房主 IPv4；自动发现无需填写。";
            if (Address.placeholder is Text placeholder) placeholder.text = _relay ? "输入房间码" : "输入房主 IPv4 地址";
            CopyRoom.gameObject.SetActive(_relay && Session.IsAuthority && !offline);
            ReadyLabel.text = Session.LocalReady ? "取消准备" : "准备开始";
            AiLabel.text = Session.LocalAi ? "收回控制" : "开启 AI 托管";
            QuickAiLabel.text = Session.LocalAi ? "收回控制" : "AI 托管";
            PlayHint.text = Session.IsSoloPlaying
                ? "单人游戏已暂停\nAI 可自动战斗、使用技能和救援；节点选择仍可手动处理。"
                : "联机进行中，菜单不会暂停队友\n需要离开一会儿时，可开启 AI 托管。";
            if (game) StatusLabel.text = Session.IsSoloPlaying ? "单人游戏  ·  " + (Session.LocalAi ? "AI 托管中" : "手动控制")
                : "双人联机  ·  " + (Session.IsAuthority ? "房主" : "队友") + "  ·  " + (Session.LocalAi ? "AI 托管中" : "手动控制");
            else if (Session.Phase == SessionPhase.Lobby)
                StatusLabel.text = "房主 " + (Session.HostRole == PlayerRole.DeepSeek ? "DS" : "HS") + "：" + (Session.HostReady ? "已准备" : "未准备") +
                    "    队友：" + (!Session.HasPeer ? "等待加入" : Session.GuestReady ? "已准备" : "未准备") +
                    (_relay && Relay != null ? "\n房间码：" + Relay.RoomCode : "\n让队友进入局域网大厅，点击你的房间即可加入。") +
                    "\n双方准备后开始。";
            else if (Session.Phase == SessionPhase.Connecting) StatusLabel.text = "正在连接房主…";
            else if (disconnected) StatusLabel.text = "连接中断，可重新加入原房间；房主仍在线时由 AI 临时接管。\n" + Session.Status;
            else StatusLabel.text = (_relay ? "先填写中继地址，再创建房间或输入房间码加入。\n" : "一人创建房间，另一人在下方点击加入。\n") +
                (Session.Status == "Offline" ? (_relay ? "两端使用相同中继服务；不要求同一 Wi-Fi。" : "两台设备连接同一 Wi-Fi 或手机热点。") : Session.Status);
            Discovery.SetBrowsing(canJoin && !_relay && Panel.activeSelf && !_leaving);
            RefreshRooms();
        }
        private void RefreshRooms()
        {
            DiscoveryLabel.text = _relay ? "公网模式通过房间码加入，无需在同一局域网。" : Discovery.Status;
            for (int i = 0; i < RoomButtons.Length; i++)
            {
                bool show = !_relay && i < Discovery.Rooms.Count;
                RoomButtons[i].gameObject.SetActive(show);
                _roomAddresses[i] = null;
                if (!show) continue;
                var room = Discovery.Rooms[i];
                RoomLabels[i].text = room.Name + "   ·   " + room.Address + (room.Compatible ? "    加入 →" : "    版本或端口不同");
                RoomButtons[i].interactable = room.Compatible && !InGame &&
                    (Session.Phase == SessionPhase.Offline || Session.Phase == SessionPhase.Disconnected);
                if (room.Compatible) _roomAddresses[i] = room.Address;
            }
        }
    }
}
