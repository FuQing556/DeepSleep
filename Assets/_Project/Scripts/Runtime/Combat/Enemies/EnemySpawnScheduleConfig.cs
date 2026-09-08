using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 单种敌人的原型刷怪节奏与出生区域。
    /// 未来关卡波次系统可以选择、暂停或替换这份日程。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_EnemySpawnSchedule_",
        menuName = "DeepSleep/配置/敌人/刷怪日程")]
    public sealed class EnemySpawnScheduleConfig : ScriptableObject
    {
        [Header("节奏")]
        [SerializeField, Min(0f)] private float _initialDelaySeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float _minimumIntervalSeconds = 1.1f;
        [SerializeField, Min(0.01f)] private float _maximumIntervalSeconds = 1.8f;
        [SerializeField, Min(1)] private int _maximumAliveCount = 6;

        [Header("右侧出生区域")]
        [SerializeField, Min(0f)] private float _horizontalSpawnMargin = 1f;
        [SerializeField, Min(0f)] private float _verticalPadding = 0.8f;

        [Header("可复现随机")]
        [SerializeField] private int _randomSeed = 404;

        public float InitialDelaySeconds => _initialDelaySeconds;
        public int MaximumAliveCount => _maximumAliveCount;
        public float HorizontalSpawnMargin => _horizontalSpawnMargin;
        public float VerticalPadding => _verticalPadding;
        public int RandomSeed => _randomSeed;

        public float SampleInterval(System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return Mathf.Lerp(
                _minimumIntervalSeconds,
                _maximumIntervalSeconds,
                (float)random.NextDouble());
        }

        public bool TryValidate(out string reason)
        {
            if (_initialDelaySeconds < 0f)
            {
                reason = "初始等待不能小于 0。";
                return false;
            }

            if (_minimumIntervalSeconds <= 0f ||
                _maximumIntervalSeconds < _minimumIntervalSeconds)
            {
                reason = "刷怪间隔范围无效。";
                return false;
            }

            if (_maximumAliveCount <= 0)
            {
                reason = "场上数量上限必须大于 0。";
                return false;
            }

            if (_horizontalSpawnMargin < 0f || _verticalPadding < 0f)
            {
                reason = "出生边距不能小于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
