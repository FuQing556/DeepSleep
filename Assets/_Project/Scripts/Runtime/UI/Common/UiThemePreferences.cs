using System;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>仅记录本机真人成功选角后的界面偏好，与网络角色和局内控制权独立。</summary>
    public static class UiThemePreferences
    {
        private const string PreferenceKey = "DeepSleep.UI.ThemeRole";

        /// <summary>读取本机已保存主题；首次启动或未知存储值使用 DeepSeek。</summary>
        public static PlayerRole Current =>
            PlayerPrefs.GetInt(PreferenceKey, (int)PlayerRole.DeepSeek) == (int)PlayerRole.Harness
                ? PlayerRole.Harness
                : PlayerRole.DeepSeek;

        /// <summary>成功保存且显示主题实际变化时通知当前界面。</summary>
        public static event Action<PlayerRole> Changed;

        /// <summary>由真人选角成功入口调用；重复选择不重复存盘，非法角色不覆盖已有偏好。</summary>
        public static void SetFromLocalSelection(PlayerRole role)
        {
            if (role != PlayerRole.DeepSeek && role != PlayerRole.Harness) return;
            if (PlayerPrefs.HasKey(PreferenceKey) && PlayerPrefs.GetInt(PreferenceKey) == (int)role) return;

            PlayerRole before = Current;
            PlayerPrefs.SetInt(PreferenceKey, (int)role);
            PlayerPrefs.Save();
            if (before != role) Changed?.Invoke(role);
        }
    }
}
