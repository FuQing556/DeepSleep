using System;
using DeepSleep.Runtime.Combat.Enemies;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    public enum ChapterObjectiveMode { AllEnemiesAndEncounters, EnemyChannel, EncountersOnly }

    [Serializable]
    public sealed class SegmentSpawnRule
    {
        [SerializeField] private EnemySpawnChannelDefinition _channel;
        [SerializeField] private bool _enabled = true;
        [SerializeField, Min(0f)] private float _initialDelaySeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float _intervalMultiplier = 1f;
        [SerializeField, Min(1)] private int _maximumAliveCount = 6;
        [SerializeField, Min(0f), Tooltip("0沿用本波生命倍率；大于等于1时覆盖本频道倍率，不与波次倍率叠乘。")]
        private float _healthMultiplierOverride;

        public EnemySpawnChannelDefinition Channel => _channel;
        public bool Enabled => _enabled;
        public float InitialDelaySeconds => _initialDelaySeconds;
        public float IntervalMultiplier => _intervalMultiplier;
        public int MaximumAliveCount => _maximumAliveCount;
        public float ResolveHealthMultiplier(float segmentMultiplier) =>
            _healthMultiplierOverride > 0f ? _healthMultiplierOverride : segmentMultiplier;

        public bool TryValidate(out string reason)
        {
            if (!float.IsFinite(_healthMultiplierOverride) ||
                (_healthMultiplierOverride != 0f && _healthMultiplierOverride < 1f))
            {
                reason = "频道生命倍率覆盖必须为0（继承）或不小于1的有限数。";
                return false;
            }
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
        [SerializeField] private ChapterObjectiveMode _objectiveMode;
        [SerializeField] private EnemySpawnChannelDefinition _objectiveChannel;
        [SerializeField] private string _objectiveLabel;
        [SerializeField, Min(1f), Tooltip("相对敌人基础生命的倍率；生命沿用整数向下取整。")]
        private float _enemyHealthMultiplier = 1f;
        [SerializeField] private SegmentSpawnRule[] _spawnRules;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
            ? "未命名战斗段"
            : _displayName;
        public float DurationSeconds => _durationSeconds;
        public int RequiredDefeats => _requiredDefeats;
        public ChapterObjectiveMode ObjectiveMode => _objectiveMode;
        public string ObjectiveLabel => string.IsNullOrWhiteSpace(_objectiveLabel) ? "击败" : _objectiveLabel;
        public bool CountsEnemy(EnemySpawnChannelDefinition channel) =>
            _objectiveMode == ChapterObjectiveMode.AllEnemiesAndEncounters ||
            (_objectiveMode == ChapterObjectiveMode.EnemyChannel && channel == _objectiveChannel);
        public float EnemyHealthMultiplier => _enemyHealthMultiplier;
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
            if (_objectiveMode == ChapterObjectiveMode.EnemyChannel &&
                (_objectiveChannel == null || !TryGetRule(_objectiveChannel, out var targetRule) || !targetRule.Enabled))
            {
                reason = $"{DisplayName} 的任务频道必须配置且启用刷怪。";
                return false;
            }
            if (_objectiveMode == ChapterObjectiveMode.EncountersOnly && _requiredDefeats != 1)
            {
                reason = $"{DisplayName} 的遭遇任务目标须为 1。";
                return false;
            }
            if (_durationSeconds <= 0f || _requiredDefeats <= 0)
            {
                reason = $"{DisplayName} 的时长与目标必须为正数。";
                return false;
            }
            if (_enemyHealthMultiplier < 1f || float.IsNaN(_enemyHealthMultiplier) ||
                float.IsInfinity(_enemyHealthMultiplier))
            {
                reason = $"{DisplayName} 的敌人生命倍率必须为不小于 1 的有限数。";
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
