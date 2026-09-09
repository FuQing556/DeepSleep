using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 预热并复用一种 EnemyActor2D。
    /// 不决定出生时间和位置，也不处理掉落奖励。
    /// </summary>
    public sealed class EnemyActorPool2D : MonoBehaviour, IFixedSimulationStep
    {
        [SerializeField] private EnemyActor2D _enemyPrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private EnemyPoolConfig _config;
        [SerializeField] private EnemyActorRuntimeBinder2D _runtimeBinder;
        [SerializeField] private DeepSleep.Runtime.Combat.Perception.CombatPerceptionRegistry2D _perceptionRegistry;

        private readonly Stack<EnemyActor2D> _available = new();
        private readonly List<EnemyActor2D> _allActors = new();
        private readonly HashSet<EnemyActor2D> _rented = new();
        private readonly List<EnemyActor2D> _despawnBuffer = new();
        private bool _isInitialized;
        private bool _hasReportedExhaustion;

        public event Action<EnemyDespawnRequest2D> ActorDespawned;

        public int TotalCount => _allActors.Count;
        public int ActiveCount => _rented.Count;
        public int AvailableCount => _available.Count;
        /// <summary>只读观测口，供网络权威适配器缓存实体；不暴露租借/回收权限。</summary>
        public IReadOnlyList<EnemyActor2D> Instances => _allActors;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyActorPool2D)}] 敌人池装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            for (int index = 0; index < _config.InitialCapacity; index++)
            {
                _available.Push(CreateActor());
            }

            _isInitialized = true;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _allActors.Count; index++)
            {
                EnemyActor2D actor = _allActors[index];

                if (actor != null)
                {
                    actor.DespawnRequested -= OnDespawnRequested;
                }
            }
        }

        public bool TryRent(
            Vector2 spawnPosition,
            in EnemySpawnVariation2D variation,
            out EnemyActor2D actor)
        {
            actor = null;

            if (!_isInitialized || !variation.IsValid)
            {
                return false;
            }

            if (_available.Count > 0)
            {
                actor = _available.Pop();
            }
            else if (_allActors.Count < _config.MaximumCapacity)
            {
                actor = CreateActor();
            }
            else
            {
                ReportExhaustionOnce();
                return false;
            }

            _rented.Add(actor);
            actor.gameObject.SetActive(true);
            actor.Activate(spawnPosition, in variation);
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_enemyPrefab == null)
            {
                reason = "未配置敌人预制体。";
                return false;
            }

            if (_poolRoot == null)
            {
                reason = "未配置对象池根节点。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置对象池参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                reason = $"对象池配置无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 让全部活动敌人通过正常回收事件离场。
        /// 可用于波次切换、房间重置和编辑器调试，不直接销毁对象。
        /// </summary>
        public int DespawnAll(EnemyDespawnReason reason)
        {
            _despawnBuffer.Clear();

            foreach (EnemyActor2D actor in _rented)
            {
                _despawnBuffer.Add(actor);
            }

            int despawnedCount = 0;

            for (int index = 0;
                 index < _despawnBuffer.Count;
                 index++)
            {
                if (_despawnBuffer[index] != null &&
                    _despawnBuffer[index].TryRequestDespawn(reason))
                {
                    despawnedCount++;
                }
            }

            _despawnBuffer.Clear();
            return despawnedCount;
        }

        /// <summary>稳定遍历池内存储，允许某个敌人在自己的步骤中回收。</summary>
        public void Simulate(float deltaTime)
        {
            if (!_isInitialized || !isActiveAndEnabled || deltaTime <= 0f) return;
            int count = _allActors.Count;
            for (int index = 0; index < count; index++)
            {
                EnemyActor2D actor = _allActors[index];
                if (actor != null && _rented.Contains(actor)) actor.Simulate(deltaTime);
            }
        }

        private EnemyActor2D CreateActor()
        {
            EnemyActor2D actor = Instantiate(_enemyPrefab, _poolRoot);
            actor.name = $"{_enemyPrefab.name}_Pooled_{_allActors.Count:00}";
            // 仅实例创建时缓存已在预制体明确装配的适配器，热路径不查组件。
            if (_perceptionRegistry != null)
            {
                if (actor.TryGetComponent<DeepSleep.Runtime.Combat.Perception.CombatPerceptionBody2D>(out var perception))
                    perception.Register(_perceptionRegistry);
                else Debug.LogError("[EnemyPool] 已启用感知，但预制体缺少 CombatPerceptionBody2D。", this);
            }

            if (_runtimeBinder != null &&
                !_runtimeBinder.TryBind(actor, out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyActorPool2D)}] " +
                    $"无法为 {actor.name} 注入运行时依赖：{reason}",
                    this);
            }

            actor.DespawnRequested += OnDespawnRequested;
            actor.gameObject.SetActive(false);
            _allActors.Add(actor);
            return actor;
        }

        private void OnDespawnRequested(
            EnemyActor2D actor,
            EnemyDespawnRequest2D request)
        {
            if (actor == null || !_rented.Remove(actor))
            {
                return;
            }

            ActorDespawned?.Invoke(request);
            actor.gameObject.SetActive(false);
            actor.transform.SetParent(_poolRoot, false);
            _available.Push(actor);
            _hasReportedExhaustion = false;
        }

        private void ReportExhaustionOnce()
        {
            if (_hasReportedExhaustion)
            {
                return;
            }

            Debug.LogWarning(
                $"[{nameof(EnemyActorPool2D)}] 敌人池已达到上限 " +
                $"{_config.MaximumCapacity}，本次生成已跳过。",
                this);
            _hasReportedExhaustion = true;
        }
    }
}
