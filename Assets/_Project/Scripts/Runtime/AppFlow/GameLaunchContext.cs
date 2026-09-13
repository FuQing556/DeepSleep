using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;

namespace DeepSleep.Runtime.AppFlow
{
    public enum GameLaunchMode
    {
        Solo,
        Online
    }

    public enum MainMenuPage
    {
        Home,
        LevelSelection,
        ModeSelection,
        Shop,
        Inventory,
        Achievements
    }

    /// <summary>跨场景传递一次启动意图，不保存战斗运行时对象。</summary>
    public sealed class GameLaunchContext : MonoBehaviour
    {
        public MetaLevelDefinition SelectedLevel { get; private set; }
        public GameLaunchMode Mode { get; private set; }
        public MainMenuPage ReturnPage { get; private set; } =
            MainMenuPage.Home;

        public void Prepare(
            MetaLevelDefinition level,
            GameLaunchMode mode)
        {
            SelectedLevel = level;
            Mode = mode;
        }

        public void SetReturnPage(MainMenuPage page)
        {
            ReturnPage = page;
        }
    }
}
