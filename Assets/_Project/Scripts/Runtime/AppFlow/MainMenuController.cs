using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>主菜单场景的纯页面路由；不持有任何战斗场景对象。</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [System.Serializable]
        public sealed class LevelCardTextBinding
        {
            public MetaLevelDefinition Level;
            public Text Title;
            public Text Description;
        }

        [SerializeField] private LevelCardTextBinding[] _levelCards;
        [SerializeField] private MetaLevelDefinition _prototypeLevel;
        [SerializeField] private MetaLevelDefinition _world01Level;
        [SerializeField] private GameObject _home;
        [SerializeField] private GameObject _levelSelection;
        [SerializeField] private GameObject _modeSelection;
        [SerializeField] private GameObject _shop;
        [SerializeField] private GameObject _inventory;
        [SerializeField] private GameObject _achievements;
        [SerializeField] private GameObject _bestiary;
        [SerializeField] private Button _bestiaryButton;
        [SerializeField] private Button _startGame;
        [SerializeField] private Button _shopButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _achievementsButton;
        [SerializeField] private Button _prototypeButton;
        [SerializeField] private Button _world01Button;
        [SerializeField] private Button _soloButton;
        [SerializeField] private Button _onlineButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _quitButton;

        private MainMenuPage _page;
        private GameSceneRouter _router;
        private MetaLevelDefinition _selectedLevel;

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
            _selectedLevel = _prototypeLevel;
            _page = GameAppRoot.Instance.LaunchContext.ReturnPage;
        }

        private void OnEnable()
        {
            RefreshLevelCardText();
            _startGame.onClick.AddListener(OpenLevels);
            _shopButton.onClick.AddListener(OpenShop);
            _inventoryButton.onClick.AddListener(OpenInventory);
            _achievementsButton.onClick.AddListener(OpenAchievements);
            if (_bestiaryButton != null) _bestiaryButton.onClick.AddListener(OpenBestiary);
            _prototypeButton.onClick.AddListener(SelectPrototype);
            if (_world01Button != null) _world01Button.onClick.AddListener(SelectWorld01);
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
            if (_bestiaryButton != null) _bestiaryButton.onClick.RemoveListener(OpenBestiary);
            _prototypeButton.onClick.RemoveListener(SelectPrototype);
            if (_world01Button != null) _world01Button.onClick.RemoveListener(SelectWorld01);
            _soloButton.onClick.RemoveListener(StartSolo);
            _onlineButton.onClick.RemoveListener(StartOnline);
            _backButton.onClick.RemoveListener(Back);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(QuitGame);
        }

        private void Start() => Render();

        /// <summary>选关显示直接读取正式定义，避免场景文案与关卡配置各存一份。</summary>
        public void RefreshLevelCardText()
        {
            if (_levelCards == null) return;
            foreach (var card in _levelCards)
            {
                if (card == null || card.Level == null || card.Title == null || card.Description == null)
                {
                    Debug.LogError("[MainMenu] 选关文字绑定不完整。", this);
                    continue;
                }
                card.Title.text = card.Level.DisplayName;
                card.Description.text = card.Level.Description;
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenLevels() => ShowPage(MainMenuPage.LevelSelection, AudioCue.UiOpen);
        public void OpenShop() => ShowPage(MainMenuPage.Shop, AudioCue.UiOpen);
        public void OpenInventory() => ShowPage(MainMenuPage.Inventory, AudioCue.UiOpen);
        public void OpenAchievements() => ShowPage(MainMenuPage.Achievements, AudioCue.UiOpen);
        public void OpenBestiary() => ShowPage(MainMenuPage.Bestiary, AudioCue.UiOpen);
        public void OpenModes() => ShowPage(MainMenuPage.ModeSelection, AudioCue.UiOpen);

        private void ShowPage(MainMenuPage page, AudioCue cue)
        {
            if (_page != page) GameAppRoot.Instance?.Audio?.Play(cue);
            _page = page;
            Render();
        }

        private void SelectPrototype()
        {
            SelectLevel(_prototypeLevel);
        }

        private void SelectWorld01()
        {
            SelectLevel(_world01Level);
        }

        /// <summary>新增关卡按钮显式绑定定义，复用现有单人/联机入口。</summary>
        public void SelectLevel(MetaLevelDefinition level)
        {
            if (level == null || !level.Implemented)
            {
                Debug.LogError("[MainMenu] 选关按钮未绑定可用关卡。", this);
                return;
            }
            _selectedLevel = level;
            OpenModes();
        }

        public void Back()
        {
            ShowPage(_page == MainMenuPage.ModeSelection
                ? MainMenuPage.LevelSelection
                : MainMenuPage.Home, AudioCue.UiCancel);
        }

        private void StartSolo() => _router.StartLevel(
            _selectedLevel, GameLaunchMode.Solo);

        private void StartOnline() => _router.StartLevel(
            _selectedLevel, GameLaunchMode.Online);

        private void Render()
        {
            _home.SetActive(_page == MainMenuPage.Home);
            _levelSelection.SetActive(_page == MainMenuPage.LevelSelection);
            _modeSelection.SetActive(_page == MainMenuPage.ModeSelection);
            _shop.SetActive(_page == MainMenuPage.Shop);
            _inventory.SetActive(_page == MainMenuPage.Inventory);
            _achievements.SetActive(_page == MainMenuPage.Achievements);
            if (_bestiary != null) _bestiary.SetActive(_page == MainMenuPage.Bestiary);
            _backButton.gameObject.SetActive(_page != MainMenuPage.Home);
        }
    }
}
