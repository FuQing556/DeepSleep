using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Enemies;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Levels
{
    /// <summary>一条普通敌人模块的资产依赖；不保存场景实例或当前战斗状态。</summary>
    [Serializable]
    public sealed class LevelEnemyContentEntry
    {
        [SerializeField] private string _entryId;
        [SerializeField] private EnemySpawnChannelDefinition _channel;
        [SerializeField] private GameObject _runtimePrefab;
        [SerializeField] private EnemyActor2D _enemyPrefab;

        public string EntryId => _entryId;
        public EnemySpawnChannelDefinition Channel => _channel;
        public GameObject RuntimePrefab => _runtimePrefab;
        public EnemyActor2D EnemyPrefab => _enemyPrefab;

        public LevelEnemyContentEntry() { }

        /// <summary>供显式装配与验证创建登记项；不会实例化或更改 Prefab。</summary>
        public LevelEnemyContentEntry(string entryId, EnemySpawnChannelDefinition channel,
            GameObject runtimePrefab, EnemyActor2D enemyPrefab)
        {
            _entryId = entryId;
            _channel = channel;
            _runtimePrefab = runtimePrefab;
            _enemyPrefab = enemyPrefab;
        }

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_entryId) || _entryId != _entryId.Trim())
            {
                reason = "敌人登记 _entryId 不能为空或带首尾空格。";
                return false;
            }
            if (_channel == null || _runtimePrefab == null || _enemyPrefab == null)
            {
                reason = $"敌人条目 {_entryId} 缺少 _channel、_runtimePrefab 或 _enemyPrefab。";
                return false;
            }
            if (_runtimePrefab.scene.IsValid() || _enemyPrefab.gameObject.scene.IsValid())
            {
                reason = $"敌人条目 {_entryId} 的 Prefab 依赖不能引用场景实例。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>本关普通敌人内容的唯一登记来源；阶段配置单独决定启用时机。</summary>
    [CreateAssetMenu(fileName = "CFG_LevelContent_", menuName = "DeepSleep/Progression/Level Content Manifest")]
    public sealed class LevelContentManifest : ScriptableObject
    {
        [SerializeField] private LevelEnemyContentEntry[] _enemies = Array.Empty<LevelEnemyContentEntry>();

        public IReadOnlyList<LevelEnemyContentEntry> Enemies => _enemies ?? Array.Empty<LevelEnemyContentEntry>();

        public bool TryGetEnemy(string entryId, out LevelEnemyContentEntry entry)
        {
            for (int index = 0; index < Enemies.Count; index++)
            {
                LevelEnemyContentEntry candidate = Enemies[index];
                if (candidate != null && string.Equals(candidate.EntryId, entryId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = null;
            return false;
        }

        public bool ContainsChannel(EnemySpawnChannelDefinition channel)
        {
            if (channel == null) return false;
            for (int index = 0; index < Enemies.Count; index++)
                if (Enemies[index] != null && Enemies[index].Channel == channel) return true;
            return false;
        }

        public bool TryValidate(out string reason)
        {
            for (int index = 0; index < Enemies.Count; index++)
            {
                LevelEnemyContentEntry entry = Enemies[index];
                if (entry == null)
                {
                    reason = $"_enemies[{index}] 为空。";
                    return false;
                }
                if (!entry.TryValidate(out reason)) return false;
                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(Enemies[previous].EntryId, entry.EntryId, StringComparison.Ordinal) ||
                        Enemies[previous].Channel == entry.Channel)
                    {
                        reason = $"敌人条目 {entry.EntryId} 的 EntryId 或 Channel 重复；当前每条频道只能登记一个敌人模块。";
                        return false;
                    }
                }
            }
            reason = string.Empty;
            return true;
        }
    }
}
