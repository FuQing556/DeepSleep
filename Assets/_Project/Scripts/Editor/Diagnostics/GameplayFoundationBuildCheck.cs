using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只读检查已启用的构建场景；装配错误阻断构建，警告只报告，不运行安装器或保存场景。</summary>
    public sealed class GameplayFoundationBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 110;

        public void OnPreprocessBuild(BuildReport report) => Debug.Log(CheckEnabledBuildScenes());

        /// <summary>与构建回调共用的只读入口，允许不出包时验证门禁。</summary>
        public static string CheckEnabledBuildScenes()
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var summary = new StringBuilder("Gameplay foundation build audit (enabled scenes only):\n");
            var failures = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled || !visited.Add(scene.path)) continue;
                try
                {
                    GameplayFoundationAudit.AuditResult result = GameplayFoundationAudit.ThrowIfInvalidScenePath(scene.path);
                    summary.AppendLine(result.Report);
                    if (result.WarningCount > 0) Debug.LogWarning(result.Report);
                }
                catch (Exception exception)
                {
                    failures.Add(scene.path + "\n" + exception.Message);
                }
            }
            if (failures.Count > 0)
                throw new BuildFailedException("Gameplay foundation audit blocked build:\n" + string.Join("\n\n", failures));
            summary.Append("Audited ").Append(visited.Count).Append(" enabled scenes; no assembly errors. No assets or scenes modified.");
            return summary.ToString();
        }
    }
}
