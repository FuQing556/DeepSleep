using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Input.Touch;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.CharacterSelection
{
    /// <summary>主菜单、单人选角与房间的显示流程；战斗由原选角门和会话服务启动。</summary>
    public sealed class OpeningFrontEnd : MonoBehaviour
    {
        public CoopSessionController Session;
        public OpeningCharacterSelectionController Selection;
        public CoopSessionMenu NetworkMenu;
        public Canvas SelectionCanvas, CombatHud;
        public GameObject Backdrop, MainPanel;
        public Button Solo, Online, Back;
        public TouchCommandSource TouchInput;
        private bool _solo, _online;

        private void OnEnable()
        {
            Solo.onClick.AddListener(ChooseSolo);
            Online.onClick.AddListener(ChooseOnline);
            Back.onClick.AddListener(ReturnHome);
            Session.Changed += Refresh;
            Selection.SelectionConfirmed += OnSelected;
        }
        private void OnDisable()
        {
            Solo.onClick.RemoveListener(ChooseSolo);
            Online.onClick.RemoveListener(ChooseOnline);
            Back.onClick.RemoveListener(ReturnHome);
            if (Session != null) Session.Changed -= Refresh;
            if (Selection != null) Selection.SelectionConfirmed -= OnSelected;
        }
        private void Start() => Refresh();
        private void OnSelected(DeepSleep.Runtime.Players.Identity.PlayerRole role) => Refresh();
        public void ChooseSolo() { _solo = true; _online = false; Refresh(); }
        public void ChooseOnline() { _online = true; _solo = false; Refresh(); }
        public void ReturnHome() { _solo = _online = false; Refresh(); }
        private void Refresh()
        {
            bool playing = Session.Phase == SessionPhase.Playing ||
                (Session.Phase == SessionPhase.Offline && Selection.IsSelectionComplete && !_online);
            bool room = !playing && (_online || Session.Phase != SessionPhase.Offline);
            bool selecting = !playing && !room && _solo;
            Backdrop.SetActive(!playing);
            MainPanel.SetActive(!playing && !room && !selecting);
            SelectionCanvas.enabled = selecting;
            CombatHud.enabled = playing;
            Back.gameObject.SetActive(!playing && Session.Phase == SessionPhase.Offline && (_solo || _online));
            NetworkMenu.SetFrontEndState(room, playing);
            TouchInput.TouchCanvas.SetActive(playing && TouchInput.TouchEnabled);
        }
    }
}
