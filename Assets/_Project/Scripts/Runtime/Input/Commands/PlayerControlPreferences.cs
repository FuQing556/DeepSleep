using UnityEngine;

namespace DeepSleep.Runtime.Input.Commands
{
    /// <summary>只保存本机操作偏好；输入命令携带选项，由主机执行，不读房主偏好替客机选敌。</summary>
    public static class PlayerControlPreferences
    {
        private const string AssistKey = "DeepSleep.Controls.AimAssist";
        private const string AutoKey = "DeepSleep.Controls.HarnessAutoFire";
        private static bool? _assist, _auto;
        public static bool AimAssist => _assist ??= PlayerPrefs.GetInt(AssistKey, 1) != 0;
        public static bool HarnessAutoFire => _auto ??= PlayerPrefs.GetInt(AutoKey, 0) != 0;
        public static PlayerControlOptions Options => PlayerControlOptions.HumanTargeting |
            (AimAssist ? PlayerControlOptions.AimAssist : 0) | (HarnessAutoFire ? PlayerControlOptions.HarnessAutoFire : 0);
        public static void SetAimAssist(bool value) { _assist = value; PlayerPrefs.SetInt(AssistKey, value ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetHarnessAutoFire(bool value) { _auto = value; PlayerPrefs.SetInt(AutoKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
