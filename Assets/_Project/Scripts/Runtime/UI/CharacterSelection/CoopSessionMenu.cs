using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.CharacterSelection
{
    /// <summary>临时但真实可用的跨端房间入口；按钮与输入框由场景显式装配。</summary>
    public sealed class CoopSessionMenu : MonoBehaviour
    {
        public CoopSessionController Session;
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
        public string LobbyTitle = "Co-op room", HostText = "Host", GuestText = "Guest", WaitingText = "Waiting", NotReadyText = "Not ready";
        private bool _wasPlaying;
        private IRelaySelection Relay => Session.TransportComponent as IRelaySelection;
        private bool _relay;
        private void OnEnable()
        {
            TogglePanel.onClick.AddListener(Toggle);
            HostDs.onClick.AddListener(CreateDs); HostHs.onClick.AddListener(CreateHs);
            JoinButton.onClick.AddListener(Join); ReadyButton.onClick.AddListener(Ready);
            AiButton.onClick.AddListener(Ai); LeaveButton.onClick.AddListener(Leave);
            Session.Changed += Refresh; Refresh();
            if (TransportMode != null) TransportMode.onClick.AddListener(SwitchTransport);
            if (CopyRoom != null) CopyRoom.onClick.AddListener(CopyRoomCode);
        }
        private void OnDisable()
        {
            TogglePanel.onClick.RemoveListener(Toggle);
            HostDs.onClick.RemoveListener(CreateDs); HostHs.onClick.RemoveListener(CreateHs);
            JoinButton.onClick.RemoveListener(Join); ReadyButton.onClick.RemoveListener(Ready);
            AiButton.onClick.RemoveListener(Ai); LeaveButton.onClick.RemoveListener(Leave);
            Session.Changed -= Refresh;
            if (TransportMode != null) TransportMode.onClick.RemoveListener(SwitchTransport);
            if (CopyRoom != null) CopyRoom.onClick.RemoveListener(CopyRoomCode);
        }
        public void SetFrontEndState(bool room, bool playing)
        {
            TogglePanel.gameObject.SetActive(playing);
            if (room) Panel.SetActive(true);
            else if (!playing || !_wasPlaying) Panel.SetActive(false);
            _wasPlaying = playing;
        }
        private void CopyRoomCode() { if (Relay != null) GUIUtility.systemCopyBuffer = Relay.RoomCode; }
        private void Toggle() => Panel.SetActive(!Panel.activeSelf);
        private void Configure() => Relay?.SelectRelay(_relay, RelayEndpoint.text.Trim(), Address.text.Trim());
        private void CreateDs() { Configure(); Session.Create(PlayerRole.DeepSeek); }
        private void CreateHs() { Configure(); Session.Create(PlayerRole.Harness); }
        private void Join() { Configure(); Session.Join(Address.text.Trim()); }
        private void SwitchTransport()
        {
            if (Session.Phase != SessionPhase.Offline) return;
            _relay = !_relay; Address.text = _relay ? "" : "127.0.0.1"; Refresh();
        }
        private void Ready() => Session.SetReady(!Session.LocalReady);
        private void Ai() => Session.SetLocalAi(!Session.LocalAi);
        private void Leave()
        {
            Session.Leave();
            StartCoroutine(ReturnToMenu());
        }
        private System.Collections.IEnumerator ReturnToMenu()
        {
            // NGO 会把管理器根放入 DontDestroyOnLoad；先结束传输并销毁旧房间，避免重复管理器。
            yield return null;
            Destroy(Session.gameObject);
            yield return null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        private void Refresh()
        {
            bool offline = Session.Phase == SessionPhase.Offline;
            HostDs.interactable = HostHs.interactable = offline;
            JoinButton.interactable = Address.interactable = offline || Session.Phase == SessionPhase.Disconnected;
            ReadyButton.interactable = Session.Phase == SessionPhase.Lobby;
            AiButton.interactable = Session.Phase == SessionPhase.Playing;
            LeaveButton.interactable = true;
            if (TransportMode != null) TransportMode.interactable = offline;
            if (RelayEndpoint != null) { RelayEndpoint.gameObject.SetActive(_relay); RelayEndpoint.interactable = offline; }
            if (TransportLabel != null) TransportLabel.text = _relay ? RelayText : LanText;
            if (AddressHint != null) AddressHint.text = _relay ? RoomHint : LanHint;
            if (CopyRoom != null) CopyRoom.gameObject.SetActive(_relay && Session.IsAuthority && !offline);
            StatusLabel.text = offline ? LobbyTitle : Session.Status + "\n" + Session.LocalRole +
                (Session.IsAuthority ? " | HOST" : " | CLIENT") +
                (_relay && Relay != null ? "\nRoom: " + Relay.RoomCode : " | UDP " + Session.Config.Port);
            if (Session.Phase == SessionPhase.Lobby)
                StatusLabel.text += "\n" + HostText + ": " + (Session.HostReady ? ReadyText : NotReadyText) +
                    " | " + GuestText + ": " + (!Session.HasPeer ? WaitingText : Session.GuestReady ? ReadyText : NotReadyText);
            ReadyButton.gameObject.SetActive(Session.Phase == SessionPhase.Lobby);
            AiButton.gameObject.SetActive(Session.Phase == SessionPhase.Playing);
            ReadyLabel.text = Session.LocalReady ? CancelReadyText : ReadyText;
            AiLabel.text = Session.LocalAi ? ControlText : AiText;
        }
    }
}
