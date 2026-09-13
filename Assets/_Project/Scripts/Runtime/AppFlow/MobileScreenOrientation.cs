using UnityEngine;

namespace DeepSleep.Runtime.AppFlow
{
    internal static class MobileScreenOrientation
    {
        // 首个场景加载前生效，主菜单、联机大厅和战斗使用同一方向策略。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }
}
