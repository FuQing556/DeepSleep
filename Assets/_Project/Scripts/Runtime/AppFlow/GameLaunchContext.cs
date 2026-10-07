using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Bestiary;
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
        Achievements,
        Bestiary
    }

    /// <summary>跨场景传递一次启动意图，不保存战斗运行时对象。</summary>
    public sealed class GameLaunchContext : MonoBehaviour
    {
        public MetaLevelDefinition SelectedLevel { get; private set; }
        public GameLaunchMode Mode { get; private set; }
        public BestiaryEntryDefinition Challenge { get; private set; }
        public MainMenuPage ReturnPage { get; private set; } =
            MainMenuPage.Home;

        public void Prepare(
            MetaLevelDefinition level,
            GameLaunchMode mode)
        {
            SelectedLevel = level;
            Mode = mode;
            Challenge = null;
        }

        public void PrepareChallenge(BestiaryEntryDefinition entry)
        {
            Prepare(entry.Level, GameLaunchMode.Solo);
            Challenge = entry;
            ReturnPage = MainMenuPage.Bestiary;
        }

        public void SetReturnPage(MainMenuPage page)
        {
            ReturnPage = page;
            Challenge = null;
        }
    }
}
