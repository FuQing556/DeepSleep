using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.CharacterSelection
{
    public enum OpeningFrontEndPage
    {
        Home,
        LevelSelection,
        ModeSelection,
        CharacterSelection,
        OnlineRoom,
        Shop,
        Inventory
    }

    /// <summary>
    /// 开局界面的页面路由。它只决定显示哪一页，选角和联机会话仍由
    /// 各自控制器负责。
    /// </summary>
    public sealed class OpeningFrontEnd : MonoBehaviour
    {
        private static bool _returnToLevelSelection;

        public CoopSessionController Session;
        public OpeningCharacterSelectionController Selection;
        public CoopSessionMenu NetworkMenu;
        public Canvas SelectionCanvas, CombatHud;
        public GameObject Backdrop, MainPanel, LevelSelectionPanel,
            ModeSelectionPanel, ShopPanel, InventoryPanel;
        public Button StartGame, Shop, Inventory, PrototypeLevel,
            Solo, Online, Back;
        public TouchCommandSource TouchInput;
        private OpeningFrontEndPage _page = OpeningFrontEndPage.Home;

        public OpeningFrontEndPage CurrentPage => _page;

        public static void ReturnToLevelSelectionAfterReload()
        {
            _returnToLevelSelection = true;
        }

        private void OnEnable()
        {
            StartGame.onClick.AddListener(OpenLevelSelection);
            Shop.onClick.AddListener(OpenShop);
            Inventory.onClick.AddListener(OpenInventory);
            PrototypeLevel.onClick.AddListener(OpenModeSelection);
            Solo.onClick.AddListener(ChooseSolo);
            Online.onClick.AddListener(ChooseOnline);
            Back.onClick.AddListener(GoBack);
            Session.Changed += Refresh;
            Selection.SelectionConfirmed += OnSelected;
        }

        private void OnDisable()
        {
            StartGame.onClick.RemoveListener(OpenLevelSelection);
            Shop.onClick.RemoveListener(OpenShop);
            Inventory.onClick.RemoveListener(OpenInventory);
            PrototypeLevel.onClick.RemoveListener(OpenModeSelection);
            Solo.onClick.RemoveListener(ChooseSolo);
            Online.onClick.RemoveListener(ChooseOnline);
            Back.onClick.RemoveListener(GoBack);
            if (Session != null) Session.Changed -= Refresh;
            if (Selection != null) Selection.SelectionConfirmed -= OnSelected;
        }

        private void Start()
        {
            if (_returnToLevelSelection)
            {
                _returnToLevelSelection = false;
                _page = OpeningFrontEndPage.LevelSelection;
            }
            Refresh();
        }

        private void OnSelected(DeepSleep.Runtime.Players.Identity.PlayerRole role) => Refresh();

        public void OpenLevelSelection()
        {
            _page = OpeningFrontEndPage.LevelSelection;
            Refresh();
        }

        public void OpenShop()
        {
            _page = OpeningFrontEndPage.Shop;
            Refresh();
        }

        public void OpenInventory()
        {
            _page = OpeningFrontEndPage.Inventory;
            Refresh();
        }

        public void OpenModeSelection()
        {
            _page = OpeningFrontEndPage.ModeSelection;
            Refresh();
        }

        public void ChooseSolo()
        {
            _page = OpeningFrontEndPage.CharacterSelection;
            Refresh();
        }

        public void ChooseOnline()
        {
            _page = OpeningFrontEndPage.OnlineRoom;
            Refresh();
        }

        public void GoBack()
        {
            if (Session.Phase != SessionPhase.Offline) return;
            _page = _page switch
            {
                OpeningFrontEndPage.LevelSelection => OpeningFrontEndPage.Home,
                OpeningFrontEndPage.Shop => OpeningFrontEndPage.Home,
                OpeningFrontEndPage.Inventory => OpeningFrontEndPage.Home,
                OpeningFrontEndPage.ModeSelection =>
                    OpeningFrontEndPage.LevelSelection,
                OpeningFrontEndPage.CharacterSelection =>
                    OpeningFrontEndPage.ModeSelection,
                OpeningFrontEndPage.OnlineRoom =>
                    OpeningFrontEndPage.ModeSelection,
                _ => OpeningFrontEndPage.Home
            };
            Refresh();
        }

        private void Refresh()
        {
            bool playing = Session.Phase == SessionPhase.Playing ||
                (Session.Phase == SessionPhase.Offline &&
                 Selection.IsSelectionComplete &&
                 _page == OpeningFrontEndPage.CharacterSelection);
            bool room = !playing &&
                (_page == OpeningFrontEndPage.OnlineRoom ||
                 Session.Phase != SessionPhase.Offline);
            Backdrop.SetActive(!playing);
            MainPanel.SetActive(!playing && !room &&
                _page == OpeningFrontEndPage.Home);
            LevelSelectionPanel.SetActive(!playing && !room &&
                _page == OpeningFrontEndPage.LevelSelection);
            ModeSelectionPanel.SetActive(!playing && !room &&
                _page == OpeningFrontEndPage.ModeSelection);
            ShopPanel.SetActive(!playing && !room &&
                _page == OpeningFrontEndPage.Shop);
            InventoryPanel.SetActive(!playing && !room &&
                _page == OpeningFrontEndPage.Inventory);
            SelectionCanvas.enabled = !playing && !room &&
                _page == OpeningFrontEndPage.CharacterSelection;
            CombatHud.enabled = playing;
            Back.gameObject.SetActive(!playing &&
                Session.Phase == SessionPhase.Offline &&
                _page != OpeningFrontEndPage.Home);
            NetworkMenu.SetFrontEndState(room, playing);
            TouchInput.TouchCanvas.SetActive(
                playing && TouchInput.TouchEnabled);
        }
    }
}
