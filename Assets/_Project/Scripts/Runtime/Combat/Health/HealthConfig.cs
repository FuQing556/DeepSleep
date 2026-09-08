using UnityEngine;

namespace DeepSleep.Runtime.Combat.Health
{
    /// <summary>
    /// 通用生命上限配置。敌人定义、玩家定义和难度快照
    /// 后续可以引用或覆盖它，但运行时不修改该资产。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_Health_",
        menuName = "DeepSleep/Combat/Health Config")]
    public sealed class HealthConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _maximumHealth;

        public float MaximumHealth => _maximumHealth;

        public bool TryValidate(out string reason)
        {
            if (_maximumHealth <= 0f)
            {
                reason = "生命上限必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
