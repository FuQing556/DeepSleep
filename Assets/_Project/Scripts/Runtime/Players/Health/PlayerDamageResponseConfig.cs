using UnityEngine;

namespace DeepSleep.Runtime.Players.Health
{
    /// <summary>
    /// 玩家成功受伤后的保护规则。生命上限仍由通用 HealthConfig 管理，
    /// 避免同一个数值同时存在于两份配置中。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_PlayerDamageResponse_",
        menuName = "DeepSleep/配置/玩家/受伤响应")]
    public sealed class PlayerDamageResponseConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)]
        private float _invulnerabilityDurationSeconds;

        public float InvulnerabilityDurationSeconds =>
            _invulnerabilityDurationSeconds;

        public bool TryValidate(out string reason)
        {
            if (_invulnerabilityDurationSeconds <= 0f)
            {
                reason = "受伤无敌时间必须大于 0 秒。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
