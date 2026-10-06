using System;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>本机装饰密度，不进入战斗数值、网络状态或关卡存档。</summary>
    public static class CloudPresentationPreferences
    {
        private const string Key = "DeepSleep.Presentation.CloudDensity";
        private static float _density = -1;
        public static event Action<float> Changed;
        public static float Density => _density < 0 ? _density = Mathf.Clamp01(PlayerPrefs.GetFloat(Key, .5f)) : _density;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _density = -1; Changed = null; }

        public static void SetDensity(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(Density, value)) return;
            _density = value;
            Changed?.Invoke(value);
        }

        public static void Save() { PlayerPrefs.SetFloat(Key, Density); PlayerPrefs.Save(); }
    }
}
