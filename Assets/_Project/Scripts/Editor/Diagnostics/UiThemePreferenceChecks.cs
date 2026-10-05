using System;
using System.Reflection;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>隔离本机主题 key 的小型自检；不加载场景、不改经济存档。</summary>
    public static class UiThemePreferenceChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("请退出 Play Mode 后检查主题偏好，避免影响当前界面。");

            string key = (string)typeof(UiThemePreferences).GetField("PreferenceKey",
                BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
            bool existed = PlayerPrefs.HasKey(key);
            int original = PlayerPrefs.GetInt(key);
            int notifications = 0;
            PlayerRole notifiedRole = PlayerRole.DeepSeek;
            Action<PlayerRole> changed = role => { notifications++; notifiedRole = role; };
            UiThemePreferences.Changed += changed;
            try
            {
                PlayerPrefs.DeleteKey(key);
                Require(UiThemePreferences.Current == PlayerRole.DeepSeek && !PlayerPrefs.HasKey(key),
                    "Missing preference must read DS without writing.");
                UiThemePreferences.SetFromLocalSelection(PlayerRole.Harness);
                Require(UiThemePreferences.Current == PlayerRole.Harness && PlayerPrefs.GetInt(key) == 1 &&
                    notifications == 1 && notifiedRole == PlayerRole.Harness, "HS choice was not saved/notified.");
                UiThemePreferences.SetFromLocalSelection(PlayerRole.Harness);
                Require(notifications == 1, "Repeated HS choice must not notify again.");
                UiThemePreferences.SetFromLocalSelection((PlayerRole)255);
                Require(UiThemePreferences.Current == PlayerRole.Harness && notifications == 1,
                    "Invalid selection must not overwrite a valid preference.");
                UiThemePreferences.SetFromLocalSelection(PlayerRole.DeepSeek);
                Require(UiThemePreferences.Current == PlayerRole.DeepSeek && PlayerPrefs.GetInt(key) == 0 &&
                    notifications == 2 && notifiedRole == PlayerRole.DeepSeek, "DS choice was not saved/notified.");
                foreach (int invalid in new[] { -1, 2, 257 })
                {
                    PlayerPrefs.SetInt(key, invalid);
                    Require(UiThemePreferences.Current == PlayerRole.DeepSeek && PlayerPrefs.GetInt(key) == invalid,
                        "Invalid stored value must read DS without silently rewriting storage.");
                }
                UiThemePreferences.SetFromLocalSelection(PlayerRole.DeepSeek);
                Require(PlayerPrefs.GetInt(key) == 0 && notifications == 2,
                    "Explicit DS choice must repair an invalid value without a false visual change.");
                PlayerPrefs.SetInt(key, 1);
                Require(UiThemePreferences.Current == PlayerRole.Harness,
                    "Current must read persisted preference, not a scene-local cached role.");
            }
            finally
            {
                UiThemePreferences.Changed -= changed;
                if (existed) PlayerPrefs.SetInt(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            Require(PlayerPrefs.HasKey(key) == existed && (!existed || PlayerPrefs.GetInt(key) == original),
                "Original local preference was not restored.");
            return "11 theme preference checks passed; original PlayerPrefs key restored. No scene/network simulation.";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
