using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek
{
    /// <summary>
    /// DeepSeek 基础饭团火力的静态配置。
    /// 局内强化应生成运行时数值快照，不得修改该资产。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_DS_RiceWeapon_",
        menuName = "DeepSleep/Combat/DeepSeek Rice Weapon Config")]
    public sealed class DeepSeekRiceWeaponConfig : ScriptableObject
    {
        [Header("发射")]
        [SerializeField, Min(0.01f)] private float _shotsPerSecond;
        [SerializeField, Min(0.01f)] private float _projectileSpeed;
        [SerializeField, Min(0.01f)] private float _projectileLifetimeSeconds;
        [SerializeField, Min(0.01f)] private float _damagePerProjectile;

        [Header("索敌")]
        [SerializeField, Min(0.01f)] private float _targetSearchRadius;
        [SerializeField, Min(1)] private int _maximumTargetCandidates;
        [SerializeField, Range(-1f, 1f)] private float _minimumForwardDot;
        [SerializeField] private LayerMask _targetLayers;
        [SerializeField] private LayerMask _obstacleLayers;

        [Header("弹体碰撞")]
        [SerializeField] private LayerMask _projectileCollisionLayers;

        [Header("对象池")]
        [SerializeField, Min(1)] private int _initialPoolSize;
        [SerializeField, Min(1)] private int _maximumPoolSize;

        public float ShotIntervalSeconds => 1f / _shotsPerSecond;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLifetimeSeconds => _projectileLifetimeSeconds;
        public float DamagePerProjectile => _damagePerProjectile;
        public float TargetSearchRadius => _targetSearchRadius;
        public int MaximumTargetCandidates => _maximumTargetCandidates;
        public float MinimumForwardDot => _minimumForwardDot;
        public LayerMask TargetLayers => _targetLayers;
        public LayerMask ObstacleLayers => _obstacleLayers;
        public LayerMask ProjectileCollisionLayers =>
            _projectileCollisionLayers;
        public int InitialPoolSize => _initialPoolSize;
        public int MaximumPoolSize => _maximumPoolSize;

        public bool TryValidate(out string reason)
        {
            if (_shotsPerSecond <= 0f)
            {
                reason = "每秒发射数必须大于 0。";
                return false;
            }

            if (_projectileSpeed <= 0f ||
                _projectileLifetimeSeconds <= 0f ||
                _damagePerProjectile <= 0f)
            {
                reason = "弹速、弹体寿命和单发伤害必须大于 0。";
                return false;
            }

            if (_targetSearchRadius <= 0f ||
                _maximumTargetCandidates <= 0)
            {
                reason = "索敌半径和候选数量必须大于 0。";
                return false;
            }

            if (_minimumForwardDot < -1f || _minimumForwardDot > 1f)
            {
                reason = "前方索敌点积阈值必须位于 -1 到 1 之间。";
                return false;
            }

            if (_targetLayers.value == 0)
            {
                reason = "目标图层不能为空。";
                return false;
            }

            if (_projectileCollisionLayers.value == 0)
            {
                reason = "弹体碰撞图层不能为空。";
                return false;
            }

            if (_initialPoolSize <= 0 ||
                _maximumPoolSize < _initialPoolSize)
            {
                reason = "对象池上限不得小于预热数量。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
