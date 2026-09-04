using System;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// Unity 编辑器与 C# 开发环境的最小连通性检查。
    /// 仅参与编辑器编译，不会进入玩家构建，也不会改动场景或运行时数据。
    /// </summary>
    internal static class DevelopmentEnvironmentProbe
    {
        private const string MENU_PATH = "DeepSleep/诊断/验证开发环境";

        [MenuItem(MENU_PATH, priority = 0)]
        private static void VerifyDevelopmentEnvironment()
        {
            Debug.Log(
                "[DeepSleep] 开发环境验证通过。\n" +
                $"Unity: {Application.unityVersion}\n" +
                $"活动构建目标: {EditorUserBuildSettings.activeBuildTarget}\n" +
                $"C# 运行时: {Environment.Version}");
        }
    }
}
