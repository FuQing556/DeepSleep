using DeepSleep.Runtime.AppFlow;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>每场景的一次开局快照；主机握手覆盖客机，重试不重新读取局外设置。</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class RunDifficultyContext : MonoBehaviour
    {
        public RunDifficultyConfig Config;
        public bool IsEasy { get; private set; }
        public float DurabilityMultiplier => IsEasy ? Config.EasyDurabilityMultiplier : 1f;
        private void Awake()
        {
            if (Config == null || !float.IsFinite(Config.EasyDurabilityMultiplier) || Config.EasyDurabilityMultiplier <= 0 || Config.EasyDurabilityMultiplier > 1)
            { Debug.LogError("[RunDifficulty] 难度配置缺失或越界。", this); enabled = false; return; }
            IsEasy = GameAppRoot.Instance != null && GameAppRoot.Instance.LaunchContext.EasyMode;
        }
        public void ApplyAuthority(bool easy) => IsEasy = easy;
        public float Health(float standard) => standard * DurabilityMultiplier;
        public int Hits(int standard) => Mathf.Max(1, Mathf.CeilToInt(standard * DurabilityMultiplier));
    }
    public static class DifficultyPreferences
    {
        private const string Key = "DeepSleep.Difficulty.Easy";
        public static bool EasyMode => PlayerPrefs.GetInt(Key, 0) != 0;
        public static void SetEasyMode(bool value) { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
