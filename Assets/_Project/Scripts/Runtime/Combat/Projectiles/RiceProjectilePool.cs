using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Projectiles
{
    /// <summary>
    /// 预热并复用饭团弹体，严格限制实例总数。
    /// 池耗尽时跳过本次发射并只记录一次告警。
    /// </summary>
    public sealed class RiceProjectilePool : MonoBehaviour
    {
        [SerializeField] private RiceProjectile _projectilePrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private DeepSeekRiceWeaponConfig _config;

        private readonly Stack<RiceProjectile> _available = new();
        private readonly List<RiceProjectile> _allProjectiles = new();
        private bool _isInitialized;
        private bool _hasReportedExhaustion;

        public event Action<RiceProjectileHitConfirmed> HitConfirmed;

        public int TotalCount => _allProjectiles.Count;
        public int AvailableCount => _available.Count;
        public IReadOnlyList<RiceProjectile> Instances => _allProjectiles;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(RiceProjectilePool)}] " +
                    $"饭团对象池装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            for (int index = 0;
                 index < _config.InitialPoolSize;
                 index++)
            {
                _available.Push(CreateProjectile());
            }

            _isInitialized = true;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _allProjectiles.Count; index++)
            {
                RiceProjectile projectile = _allProjectiles[index];

                if (projectile != null &&
                    projectile.transform.parent != _poolRoot)
                {
                    Destroy(projectile.gameObject);
                }
            }
        }

        public bool TryRent(
            Vector2 position,
            Vector2 direction,
            GameObject damageSource,
            out RiceProjectile projectile)
        {
            projectile = null;

            if (!_isInitialized || direction.sqrMagnitude <= 0f)
            {
                return false;
            }

            if (_available.Count > 0)
            {
                projectile = _available.Pop();
            }
            else if (_allProjectiles.Count < _config.MaximumPoolSize)
            {
                projectile = CreateProjectile();
            }
            else
            {
                ReportExhaustionOnce();
                return false;
            }

            projectile.transform.SetParent(null, true);
            projectile.OnRent(
                this,
                position,
                direction.normalized,
                _config.ProjectileSpeed,
                _config.ProjectileLifetimeSeconds,
                _config.ProjectileCollisionLayers,
                _config.DamagePerProjectile,
                damageSource);

            return true;
        }

        internal void Return(RiceProjectile projectile)
        {
            if (projectile == null || !projectile.IsRented)
            {
                return;
            }

            projectile.OnReturn();
            projectile.transform.SetParent(_poolRoot, false);
            _available.Push(projectile);
            _hasReportedExhaustion = false;
        }

        internal void NotifyHitConfirmed(RiceProjectileHitConfirmed hit)
        {
            if (hit.IsValid)
            {
                HitConfirmed?.Invoke(hit);
            }
        }

        private RiceProjectile CreateProjectile()
        {
            RiceProjectile projectile = Instantiate(
                _projectilePrefab,
                _poolRoot);

            projectile.name =
                $"{_projectilePrefab.name}_Pooled_{_allProjectiles.Count:00}";
            projectile.PrepareForPool();
            _allProjectiles.Add(projectile);
            return projectile;
        }

        private void ReportExhaustionOnce()
        {
            if (_hasReportedExhaustion)
            {
                return;
            }

            Debug.LogWarning(
                $"[{nameof(RiceProjectilePool)}] " +
                $"饭团池已达到上限 {_config.MaximumPoolSize}，" +
                "本次发射已跳过。",
                this);
            _hasReportedExhaustion = true;
        }

        private bool TryValidateConfiguration(out string reason)
        {
            if (_projectilePrefab == null)
            {
                reason = "未配置饭团弹体 Prefab。";
                return false;
            }

            if (_poolRoot == null)
            {
                reason = "未配置对象池根节点。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置饭团武器参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }
    }
}
