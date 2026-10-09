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
        [SerializeField] private bool _despawnOnImpact = true;
        [SerializeField, Min(.01f)] private float _repeatIntervalSeconds = 1f;
        [SerializeField, Min(0f)] private float _knockbackDistance;
        [SerializeField, Min(.01f)] private float _knockbackSeconds = .22f;
        public bool DespawnOnImpact => _despawnOnImpact;
        public float RepeatIntervalSeconds => _repeatIntervalSeconds;
        public float KnockbackDistance => _knockbackDistance;
        public float KnockbackSeconds => _knockbackSeconds;

        public float DamageAmount => _damageAmount;
        public LayerMask TargetLayers => _targetLayers;

        public bool ContainsLayer(int layer)
        {
            return (_targetLayers.value & (1 << layer)) != 0;
        }

        public bool TryValidate(out string reason)
        {
            if (!float.IsFinite(_repeatIntervalSeconds) || _repeatIntervalSeconds <= 0f ||
                !float.IsFinite(_knockbackDistance) || _knockbackDistance < 0f ||
                !float.IsFinite(_knockbackSeconds) || _knockbackSeconds <= 0f)
            { reason = "接触冷却/击退距离/时长无效。"; return false; }
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
