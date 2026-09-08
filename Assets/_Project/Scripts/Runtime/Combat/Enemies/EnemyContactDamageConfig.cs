using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 敌人接触攻击的数据。伤害与目标层都由资产配置，
    /// 接触组件不认识任何具体玩家类型。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_EnemyContactDamage_",
        menuName = "DeepSleep/配置/敌人/接触伤害")]
    public sealed class EnemyContactDamageConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _damageAmount = 1f;
        [SerializeField] private LayerMask _targetLayers;

        public float DamageAmount => _damageAmount;
        public LayerMask TargetLayers => _targetLayers;

        public bool ContainsLayer(int layer)
        {
            return (_targetLayers.value & (1 << layer)) != 0;
        }

        public bool TryValidate(out string reason)
        {
            if (_damageAmount <= 0f)
            {
                reason = "接触伤害必须大于 0。";
                return false;
            }

            if (_targetLayers.value == 0)
            {
                reason = "至少需要配置一个目标物理图层。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
