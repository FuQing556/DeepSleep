using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>不要把编辑器内的横向 GameView 误当作手机锁横屏。</summary>
    public sealed class MobileOrientationBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft ||
                PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
                throw new BuildFailedException("中国AI会飞必须固定横屏：LandscapeLeft，且关闭两个竖屏选项。拒绝生成方向配置错误的 APK。");
        }
    }
}
