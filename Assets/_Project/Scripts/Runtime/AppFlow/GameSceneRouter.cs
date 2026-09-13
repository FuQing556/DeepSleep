using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>唯一允许发起主菜单/玩法场景切换的入口。</summary>
    public sealed class GameSceneRouter : MonoBehaviour
    {
        [SerializeField] private GameLaunchContext _launchContext;
        [SerializeField] private string _bootScene = "Boot";
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _prototypeGameplayScene =
            "Gameplay_Prototype";

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == _bootScene)
            {
                LoadMainMenu(MainMenuPage.Home);
            }
        }

        public void StartPrototype(
            MetaLevelDefinition level,
            GameLaunchMode mode)
        {
            if (level == null || !level.Implemented)
            {
                Debug.LogError("[SceneRouter] 关卡不存在或尚未实现。", this);
                return;
            }

            _launchContext.Prepare(level, mode);
            GameAppRoot.Instance.Achievements.Report(
                AchievementTriggerIds.JourneyStarted);
            SceneManager.LoadScene(_prototypeGameplayScene);
        }

        public void LoadMainMenu(MainMenuPage page)
        {
            Time.timeScale = 1f;
            _launchContext.SetReturnPage(page);
            SceneManager.LoadScene(_mainMenuScene);
        }
    }
}
