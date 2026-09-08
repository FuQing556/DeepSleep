using System.Collections.Generic;
using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>
    /// 场景级敌方弹体池。多条同类敌人共享，不在敌人预制体内重复创建。
    /// </summary>
    public sealed class EnemyProjectilePool2D : MonoBehaviour
    {
        [SerializeField] private EnemyProjectile2D _projectilePrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private EnemyProjectileConfig _config;
        [SerializeField] private OneShotSpriteEffectPool2D _impactEffectPool;

        private readonly Stack<EnemyProjectile2D> _available = new();
        private readonly List<EnemyProjectile2D> _all = new();
        private bool _isInitialized;
        private bool _reportedExhaustion;

        public int TotalCount => _all.Count;
        public int ActiveCount => _all.Count - _available.Count;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyProjectilePool2D)}] 弹体池装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            for (int index = 0; index < _config.InitialPoolSize; index++)
            {
                _available.Push(CreateProjectile());
            }

            _isInitialized = true;
        }

        public bool TryRent(
            Vector2 position,
            Vector2 direction,
            GameObject damageSource,
            out EnemyProjectile2D projectile)
        {
            projectile = null;
            if (!_isInitialized || direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (_available.Count > 0)
            {
                projectile = _available.Pop();
            }
            else if (_all.Count < _config.MaximumPoolSize)
            {
                projectile = CreateProjectile();
            }
            else
            {
                ReportExhaustionOnce();
                return false;
            }

            projectile.Rent(this, position, direction, damageSource, _config);
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_projectilePrefab == null)
            {
                reason = "未配置弹体预制体。";
                return false;
            }

            if (!_projectilePrefab.TryValidateConfiguration(out reason))
            {
                reason = $"弹体预制体无效：{reason}";
                return false;
            }

            if (_poolRoot == null)
            {
                reason = "未配置对象池根节点。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置弹体参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                reason = $"弹体参数无效：{reason}";
                return false;
            }

            if (_impactEffectPool == null)
            {
                reason = "未配置命中特效池。";
                return false;
            }

            if (!_impactEffectPool.TryValidateConfiguration(out reason))
            {
                reason = $"命中特效池无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 立即回收全部活动敌方弹体。用于波次切换、房间重置和调试清场。
        /// </summary>
        public int ReturnAllActive()
        {
            int returnedCount = 0;

            for (int index = 0; index < _all.Count; index++)
            {
                EnemyProjectile2D projectile = _all[index];

                if (projectile != null && projectile.IsRented)
                {
                    Return(projectile);
                    returnedCount++;
                }
            }

            return returnedCount;
        }

        internal void Return(EnemyProjectile2D projectile)
        {
            if (projectile == null || !projectile.IsRented)
            {
                return;
            }

            projectile.Return();
            projectile.transform.SetParent(_poolRoot, false);
            _available.Push(projectile);
            _reportedExhaustion = false;
        }

        internal void NotifyImpact(Vector2 position, float angle)
        {
            _impactEffectPool.TryPlay(position, angle);
        }

        private EnemyProjectile2D CreateProjectile()
        {
            EnemyProjectile2D projectile = Instantiate(
                _projectilePrefab,
                _poolRoot);
            projectile.name =
                $"{_projectilePrefab.name}_Pooled_{_all.Count:00}";
            projectile.PrepareForPool();
            _all.Add(projectile);
            return projectile;
        }

        private void ReportExhaustionOnce()
        {
            if (_reportedExhaustion)
            {
                return;
            }

            Debug.LogWarning(
                $"[{nameof(EnemyProjectilePool2D)}] " +
                $"弹体池达到上限 {_config.MaximumPoolSize}，跳过本次发射。",
                this);
            _reportedExhaustion = true;
        }
    }
}
