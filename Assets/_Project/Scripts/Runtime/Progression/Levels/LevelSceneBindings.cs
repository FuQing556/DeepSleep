using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Levels
{
    /// <summary>内容条目与已装配实例的映射；不决定是否生成敌人。</summary>
    [Serializable]
    public sealed class LevelEnemySceneBinding
    {
        [SerializeField] private string _entryId;
        [SerializeField] private Transform _root;
        [SerializeField] private EnemySpawnDirector2D _director;
        [SerializeField] private EnemyActorPool2D _pool;
        [SerializeField] private EnemyProjectilePool2D[] _projectilePools = Array.Empty<EnemyProjectilePool2D>();

        public string EntryId => _entryId;
        public Transform Root => _root;
        public EnemySpawnDirector2D Director => _director;
        public EnemyActorPool2D Pool => _pool;
        public IReadOnlyList<EnemyProjectilePool2D> ProjectilePools =>
            _projectilePools ?? Array.Empty<EnemyProjectilePool2D>();

        public LevelEnemySceneBinding() { }

        public LevelEnemySceneBinding(string entryId, Transform root, EnemySpawnDirector2D director,
            EnemyActorPool2D pool, EnemyProjectilePool2D[] projectilePools)
        {
            _entryId = entryId;
            _root = root;
            _director = director;
            _pool = pool;
            _projectilePools = projectilePools == null
                ? Array.Empty<EnemyProjectilePool2D>()
                : (EnemyProjectilePool2D[])projectilePools.Clone();
        }
    }

    /// <summary>
    /// 用户显式装配在关卡根上的绑定表。引用所有场景服务及敌人实例，不要求同物体挂载组件。
    /// 不驱动阶段、模拟或生命周期；Editor 由此派生消费者清单，运行时只检查而不补线。
    /// </summary>
    public sealed class LevelSceneBindings : MonoBehaviour
    {
        // Unity 的内建持久场景标识；只核对显式 Session 根的生命周期，不用于查找对象。
        private const string PERSISTENT_SCENE_NAME = "DontDestroyOnLoad";
        [SerializeField] private MetaLevelDefinition _level;
        [SerializeField] private ChapterRunController _chapterRun;
        [SerializeField] private RestNodePrototypeController2D _restNode;
        [SerializeField] private FixedSimulationLoop _simulationLoop;
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private NetworkAuthorityGate _authorityGate;
        [SerializeField] private CombatPerceptionRegistry2D _perceptionRegistry;
        [SerializeField] private EnemyTokenRewardController _tokenRewards;
        [SerializeField] private NetworkWorldSnapshotChannel _worldSnapshot;
        [SerializeField] private LevelEnemySceneBinding[] _enemies = Array.Empty<LevelEnemySceneBinding>();

        public MetaLevelDefinition Level => _level;
        public ChapterRunController ChapterRun => _chapterRun;
        public RestNodePrototypeController2D RestNode => _restNode;
        public FixedSimulationLoop SimulationLoop => _simulationLoop;
        public CoopSessionController Session => _session;
        public NetworkAuthorityGate AuthorityGate => _authorityGate;
        public CombatPerceptionRegistry2D PerceptionRegistry => _perceptionRegistry;
        public EnemyTokenRewardController TokenRewards => _tokenRewards;
        public NetworkWorldSnapshotChannel WorldSnapshot => _worldSnapshot;
        public IReadOnlyList<LevelEnemySceneBinding> Enemies => _enemies ?? Array.Empty<LevelEnemySceneBinding>();

        public bool TryGetEnemy(string entryId, out LevelEnemySceneBinding binding)
        {
            for (int index = 0; index < Enemies.Count; index++)
            {
                LevelEnemySceneBinding candidate = Enemies[index];
                if (candidate != null && string.Equals(candidate.EntryId, entryId, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }
            binding = null;
            return false;
        }

        /// <summary>验证绑定真源；消费者派生数组由 Editor 审计比对，避免装配校验递归。</summary>
        public bool TryValidateConfiguration(out string reason, bool validateDerivedChannels = true)
        {
            if (!LevelIdentityValidation.TryValidateContext(_level, gameObject.scene.name, null, false, out reason) ||
                !_level.TryValidateGameplayDefinition(out reason)) return false;
            if (!TryValidateService(_chapterRun, nameof(_chapterRun), out reason) ||
                !TryValidateService(_restNode, nameof(_restNode), out reason) ||
                !TryValidateService(_simulationLoop, nameof(_simulationLoop), out reason) ||
                !TryValidateService(_perceptionRegistry, nameof(_perceptionRegistry), out reason) ||
                !TryValidateService(_tokenRewards, nameof(_tokenRewards), out reason) ||
                !TryValidateNetworkOwnership(out reason, validateDerivedChannels)) return false;
            LevelContentManifest manifest = _level.ContentManifest;
            if (Enemies.Count != manifest.Enemies.Count)
            {
                reason = $"{_level.LevelId} 的场景敌人绑定数与内容清单不同，请显式更新装配。";
                return false;
            }
            for (int index = 0; index < Enemies.Count; index++)
            {
                LevelEnemySceneBinding binding = Enemies[index];
                if (binding == null || string.IsNullOrWhiteSpace(binding.EntryId) ||
                    !manifest.TryGetEnemy(binding.EntryId, out LevelEnemyContentEntry entry))
                {
                    reason = $"{_level.LevelId} 的 _enemies[{index}] 为空或 EntryId 未登记。";
                    return false;
                }
                if (!TryValidateService(binding.Root, binding.EntryId + "._root", out reason) ||
                    !TryValidateService(binding.Director, binding.EntryId + "._director", out reason) ||
                    !TryValidateService(binding.Pool, binding.EntryId + "._pool", out reason)) return false;
                if (!binding.Director.transform.IsChildOf(binding.Root) ||
                    !binding.Pool.transform.IsChildOf(binding.Root) ||
                    (validateDerivedChannels && binding.Director.Channel != entry.Channel))
                {
                    reason = $"{_level.LevelId}/{binding.EntryId} 的 Director/Pool 不属于模块根，或 Director.Channel 与定义不同。";
                    return false;
                }
                for (int previous = 0; previous < index; previous++)
                {
                    LevelEnemySceneBinding other = Enemies[previous];
                    if (string.Equals(other.EntryId, binding.EntryId, StringComparison.Ordinal) ||
                        other.Root == binding.Root || other.Director == binding.Director || other.Pool == binding.Pool)
                    {
                        reason = $"{_level.LevelId}/{binding.EntryId} 重复使用 EntryId、根、Director 或 Pool；当前必须一池一频道。";
                        return false;
                    }
                }
                for (int projectileIndex = 0; projectileIndex < binding.ProjectilePools.Count; projectileIndex++)
                {
                    EnemyProjectilePool2D pool = binding.ProjectilePools[projectileIndex];
                    if (!TryValidateService(pool, binding.EntryId + "._projectilePools[" + projectileIndex + "]", out reason))
                        return false;
                    for (int previous = 0; previous < projectileIndex; previous++)
                    {
                        if (binding.ProjectilePools[previous] == pool)
                        {
                            reason = $"{_level.LevelId}/{binding.EntryId} 重复登记同一敌弹池。";
                            return false;
                        }
                    }
                }
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>允许本关已登记的 NGO 根在 Play 时进入 Unity 持久场景，仍严格验证所属选角与根层级。</summary>
        public bool TryValidateNetworkOwnership(out string reason, bool validateSessionBinding = true)
        {
            if (validateSessionBinding && (_session == null || _session.LevelBindings != this))
            {
                reason = "_session.LevelBindings 必须引用当前关卡的绑定表。";
                return false;
            }
            if (_session == null || _chapterRun == null || _chapterRun.gameObject.scene != gameObject.scene ||
                _session.Selection == null ||
                _session.Selection != _chapterRun.Selection ||
                _session.Selection.gameObject.scene != gameObject.scene)
            {
                reason = "_session.Selection 必须属于本关 Gameplay 场景，且与 ChapterRun.Selection 是同一个选角门。";
                return false;
            }
            GameObject root = _session.SceneExitRoot;
            if (root == null || _session.gameObject.scene != root.scene ||
                !_session.transform.IsChildOf(root.transform))
            {
                reason = "_session 不属于其显式 SceneExitRoot。";
                return false;
            }
            bool sceneLocal = root.scene == gameObject.scene;
            bool ownedPersistentRoot = Application.isPlaying && root.scene.IsValid() &&
                root.scene.buildIndex == -1 && root.scene.name == PERSISTENT_SCENE_NAME;
            if (!sceneLocal && !ownedPersistentRoot)
            {
                reason = "_session.SceneExitRoot 必须在本关场景，或在 Play 时由现有 NGO 生命周期移入 Unity 持久场景。";
                return false;
            }
            if (_authorityGate == null || _worldSnapshot == null ||
                _authorityGate.Session != _session || _worldSnapshot.Session != _session ||
                _authorityGate.gameObject.scene != root.scene || _worldSnapshot.gameObject.scene != root.scene ||
                !_authorityGate.transform.IsChildOf(root.transform) ||
                !_worldSnapshot.transform.IsChildOf(root.transform))
            {
                reason = "AuthorityGate/WorldSnapshot 必须引用同一 _session，并属于其 SceneExitRoot 层级。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private bool TryValidateService(Component service, string field, out string reason)
        {
            if (service == null || service.gameObject.scene != gameObject.scene)
            {
                reason = $"{_level.LevelId}/{name} 的 {field} 为空或不属于当前关卡场景。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
