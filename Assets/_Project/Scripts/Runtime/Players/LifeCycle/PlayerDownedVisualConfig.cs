using UnityEngine;

namespace DeepSleep.Runtime.Players.LifeCycle
{
    /// <summary>
    /// 玩家进入宕机状态时的纯表现参数。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_PlayerDownedVisual_",
        menuName = "DeepSleep/配置/玩家/宕机表现")]
    public sealed class PlayerDownedVisualConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _ghostFadeSeconds;
        [SerializeField, Range(0.01f, 1f)] private float _ghostStartAlpha;

        public float GhostFadeSeconds => _ghostFadeSeconds;
        public float GhostStartAlpha => _ghostStartAlpha;

        public bool TryValidate(out string reason)
        {
            if (_ghostFadeSeconds <= 0f)
            {
                reason = "旧姿态淡出时间必须大于 0 秒。";
                return false;
            }

            if (_ghostStartAlpha <= 0f || _ghostStartAlpha > 1f)
            {
                reason = "旧姿态初始透明度必须处于 (0, 1]。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
