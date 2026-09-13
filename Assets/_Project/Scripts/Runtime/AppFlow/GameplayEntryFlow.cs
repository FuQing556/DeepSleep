using System.Collections;
using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>玩法场景只处理选角/房间到战斗的入口，不再承担主菜单。</summary>
    public sealed class GameplayEntryFlow : MonoBehaviour
    {
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private OpeningCharacterSelectionController _selection;
        [SerializeField] private CoopSessionMenu _networkMenu;
        [SerializeField] private Canvas _selectionCanvas;
        [SerializeField] private Canvas _combatHud;
        [SerializeField] private TouchCommandSource _touchInput;
        [SerializeField] private Button _backButton;

        private GameLaunchMode _mode;

        private void Awake()
        {
            if (GameAppRoot.Instance == null)
            {
                Debug.LogError("[GameplayEntry] 必须从 Boot/MainMenu 启动。", this);
                enabled = false;
                return;
            }
            _mode = GameAppRoot.Instance.LaunchContext.Mode;
        }

        private void OnEnable()
        {
            _session.Changed += Refresh;
            _selection.SelectionConfirmed += OnSelected;
            _backButton.onClick.AddListener(ReturnToModes);
        }

        private void OnDisable()
        {
            if (_session != null) _session.Changed -= Refresh;
            if (_selection != null)
                _selection.SelectionConfirmed -= OnSelected;
            if (_backButton != null)
                _backButton.onClick.RemoveListener(ReturnToModes);
        }

        private void Start() => Refresh();
        private void OnSelected(
            DeepSleep.Runtime.Players.Identity.PlayerRole role) => Refresh();

        private void Refresh()
        {
            bool playing = _session.Phase == SessionPhase.Playing ||
                (_mode == GameLaunchMode.Solo &&
                 _selection.IsSelectionComplete);
            bool room = !playing && _mode == GameLaunchMode.Online;
            _selectionCanvas.enabled = !playing && !room;
            _combatHud.enabled = playing;
            _networkMenu.SetFrontEndState(room, playing);
            _backButton.gameObject.SetActive(
                !playing && !room && _session.Phase == SessionPhase.Offline);
            _touchInput.TouchCanvas.SetActive(
                playing && _touchInput.TouchEnabled);
        }

        private void ReturnToModes()
        {
            StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            if (_session.Phase != SessionPhase.Offline)
            {
                _session.Leave();
            }
            yield return null;
            GameAppRoot.Instance.SceneRouter.LoadMainMenu(
                MainMenuPage.ModeSelection);
        }
    }
}
