using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Debugging
{
    /// <summary>停止播放后清除磁盘档案，防止运行中的档案对象写回旧进度。</summary>
    public static class LocalProfileDebugMenu
    {
        private const string MenuPath = "DeepSleep/调试/存档/清空本地存档（停止播放后）";

        [MenuItem(MenuPath, priority = 70)]
        private static void ClearFromMenu()
        {
            if (!EditorUtility.DisplayDialog("清空 DeepSleep 本地存档",
                    "将重置鲸元券、背包、通关记录与成就。\n旧文件会先归档备份。",
                    "清空", "取消")) return;
            ClearProfile();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanClear() => !EditorApplication.isPlayingOrWillChangePlaymode;

        public static string ClearProfile()
        {
            if (!CanClear())
                throw new InvalidOperationException("请先停止播放，再清空本地存档。");

            string directory = Path.GetFullPath(Application.persistentDataPath);
            // 同时清除自动恢复文件，避免下次启动从旧备份恢复。
            string[] names = { "profile.json", "profile.backup.json", "profile.json.tmp" };
            string archive = Path.Combine(directory, "DebugProfileArchives",
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N"));
            int count = 0;
            foreach (string name in names)
            {
                string source = Path.Combine(directory, name);
                if (!File.Exists(source)) continue;
                Directory.CreateDirectory(archive);
                File.Copy(source, Path.Combine(archive, name), false);
                count++;
            }
            // 所有备份都成功后才删除，任何备份异常都会中止清理。
            foreach (string name in names)
            {
                string source = Path.Combine(directory, name);
                if (File.Exists(source)) File.Delete(source);
            }
            string result = count == 0 ? "本地存档已经为空。" :
                $"已清空 {count} 个存档文件。可恢复备份：{archive}";
            Debug.Log("[DeepSleep 调试] " + result);
            return result;
        }
    }
}
