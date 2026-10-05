using System;

namespace DeepSleep.Editor.Setup
{
    /// <summary>旧程序几何/全局重排入口已停用，不得覆盖已确认的生成美术和用户布局。</summary>
    public static class UiThemeInstaller
    {
        public static string Install() => throw new InvalidOperationException(
            "UiThemeInstaller 原型装配已停用。请显式运行 UiLayeredArtworkInstaller.Install()，仅替换生成美术层，不重排控件。");
    }
}
