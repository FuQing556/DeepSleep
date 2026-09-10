using System;
using DeepSleep.Runtime.Combat.Enemies;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    [Serializable]
    public sealed class SegmentSpawnRule
    {
        [SerializeField] private EnemySpawnChannelDefinition _channel;
        [SerializeField] private bool _enabled = true;
        [SerializeField, Min(0f)] private float _initialDelaySeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float _intervalMultiplier = 1f;
        [SerializeField, Min(1)] private int _maximumAliveCount = 6;

        public EnemySpawnChannelDefinition Channel => _channel;
        public bool Enabled => _enabled;
        public float InitialDelaySeconds => _initialDelaySeconds;
        public float IntervalMultiplier => _intervalMultiplier;
        public int MaximumAliveCount => _maximumAliveCount;

        public bool TryValidate(out string reason)
        {
            if (_channel == null)
            {
                reason = "未配置刷怪频道。";
                return false;
            }
            if (_initialDelaySeconds < 0f || _intervalMultiplier <= 0f ||
                _maximumAliveCount <= 0)
            {
                reason = $"{_channel.DisplayName} 的延迟、间隔倍率或上限无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class ChapterCombatSegmentDefinition
    {
        [SerializeField] private string _displayName = "未命名战斗段";
        [SerializeField, Min(1f)] private float _durationSeconds = 60f;
        [SerializeField, Min(1)] private int _requiredDefeats = 10;
        [SerializeField] private SegmentSpawnRule[] _spawnRules;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
            ? "未命名战斗段"
            : _displayName;
        public float DurationSeconds => _durationSeconds;
        public int RequiredDefeats => _requiredDefeats;
        public SegmentSpawnRule[] SpawnRules => _spawnRules;

        public bool TryGetRule(
            EnemySpawnChannelDefinition channel,
            out SegmentSpawnRule rule)
        {
            if (_spawnRules != null)
            {
                for (int index = 0; index < _spawnRules.Length; index++)
                {
                    SegmentSpawnRule candidate = _spawnRules[index];
                    if (candidate != null && candidate.Channel == channel)
                    {
                        rule = candidate;
                        return true;
                    }
                }
            }

            rule = null;
            return false;
        }

        public bool TryValidate(out string reason)
        {
            if (_durationSeconds <= 0f || _requiredDefeats <= 0)
            {
                reason = $"{DisplayName} 的时长与目标必须为正数。";
                return false;
            }
            if (_spawnRules == null || _spawnRules.Length == 0)
            {
                reason = $"{DisplayName} 没有刷怪规则。";
                return false;
            }

            bool hasEnabledRule = false;
            for (int index = 0; index < _spawnRules.Length; index++)
            {
                SegmentSpawnRule rule = _spawnRules[index];
                if (rule == null)
                {
                    reason = $"{DisplayName} 第 {index + 1} 条规则为空。";
                    return false;
                }
                if (!rule.TryValidate(out reason))
                {
                    reason = $"{DisplayName} 第 {index + 1} 条规则无效：{reason}";
                    return false;
                }
                for (int other = 0; other < index; other++)
                {
                    if (_spawnRules[other].Channel == rule.Channel)
                    {
                        reason = $"{DisplayName} 重复配置了频道 {rule.Channel.DisplayName}。";
                        return false;
                    }
                }
                hasEnabledRule |= rule.Enabled;
            }

            if (!hasEnabledRule)
            {
                reason = $"{DisplayName} 至少要启用一个刷怪频道。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
