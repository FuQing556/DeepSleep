using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>主菜单场景的纯页面路由；不持有任何战斗场景对象。</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MetaLevelDefinition _prototypeLevel;
        [SerializeField] private GameObject _home;
        [SerializeField] private GameObject _levelSelection;
        [SerializeField] private GameObject _modeSelection;
        [SerializeField] private GameObject _shop;
        [SerializeField] private GameObject _inventory;
        [SerializeField] private GameObject _achievements;
        [SerializeField] private Button _startGame;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _achievementsButton;
        [SerializeField] private Button _prototypeButton;
        [SerializeField] private Button _soloButton;
        [SerializeField] private Button _onlineButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _quitButton;

        private MainMenuPage _page;
        private GameSceneRouter _router;

        public MainMenuPage CurrentPage => _page;

        private void Awake()
        {
            if (GameAppRoot.Instance == null)
            {
                Debug.LogError("[MainMenu] 必须从 Boot 场景启动。", this);
                enabled = false;
                return;
            }
            _router = GameAppRoot.Instance.SceneRouter;
            _page = GameAppRoot.Instance.LaunchContext.ReturnPage;
        }

        private void OnEnable()
        {
            _startGame.onClick.AddListener(OpenLevels);
            _shopButton.onClick.AddListener(OpenShop);
            _inventoryButton.onClick.AddListener(OpenInventory);
            _achievementsButton.onClick.AddListener(OpenAchievements);
            _prototypeButton.onClick.AddListener(OpenModes);
            _soloButton.onClick.AddListener(StartSolo);
            _onlineButton.onClick.AddListener(StartOnline);
            _backButton.onClick.AddListener(Back);
            if (_quitButton != null) _quitButton.onClick.AddListener(QuitGame);
        }

        private void OnDisable()
        {
            _startGame.onClick.RemoveListener(OpenLevels);
            _shopButton.onClick.RemoveListener(OpenShop);
            _inventoryButton.onClick.RemoveListener(OpenInventory);
            _achievementsButton.onClick.RemoveListener(OpenAchievements);
            _prototypeButton.onClick.RemoveListener(OpenModes);
            _soloButton.onClick.RemoveListener(StartSolo);
            _onlineButton.onClick.RemoveListener(StartOnline);
            _backButton.onClick.RemoveListener(Back);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(QuitGame);
        }

        private void Start() => Render();

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenLevels()
        {
            _page = MainMenuPage.LevelSelection;
            Render();
        }

        public void OpenShop()
        {
            _page = MainMenuPage.Shop;
            Render();
        }

        public void OpenInventory()
        {
            _page = MainMenuPage.Inventory;
            Render();
        }

        public void OpenAchievements()
        {
            _page = MainMenuPage.Achievements;
            Render();
        }

        public void OpenModes()
        {
            _page = MainMenuPage.ModeSelection;
            Render();
        }

        public void Back()
        {
            _page = _page == MainMenuPage.ModeSelection
                ? MainMenuPage.LevelSelection
                : MainMenuPage.Home;
            Render();
        }

        private void StartSolo() => _router.StartPrototype(
            _prototypeLevel, GameLaunchMode.Solo);

        private void StartOnline() => _router.StartPrototype(
            _prototypeLevel, GameLaunchMode.Online);

        private void Render()
        {
            _home.SetActive(_page == MainMenuPage.Home);
            _levelSelection.SetActive(_page == MainMenuPage.LevelSelection);
            _modeSelection.SetActive(_page == MainMenuPage.ModeSelection);
            _shop.SetActive(_page == MainMenuPage.Shop);
            _inventory.SetActive(_page == MainMenuPage.Inventory);
            _achievements.SetActive(_page == MainMenuPage.Achievements);
            _backButton.gameObject.SetActive(_page != MainMenuPage.Home);
        }
    }
}
