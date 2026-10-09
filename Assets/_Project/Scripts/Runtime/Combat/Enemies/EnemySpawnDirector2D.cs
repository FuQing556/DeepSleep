using System;
using DeepSleep.Runtime.World.Playfield;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 单敌种刷怪器：在配置指定的战斗区侧边随机高度生成，
    /// 将随机运动快照交给对象池中的敌人。
    /// </summary>
    public sealed class EnemySpawnDirector2D : MonoBehaviour, IFixedSimulationStep
    {
        [SerializeField] private EnemyActorPool2D _pool;
        [SerializeField] private CombatPlayfieldConfig _playfield;
        [SerializeField] private EnemySpawnMotionConfig _motionConfig;
        [SerializeField] private EnemySpawnScheduleConfig _schedule;
        [SerializeField] private EnemySpawnChannelDefinition _channel;
        [SerializeField] private bool _autoStart = true;
        [Tooltip("可选的生成容量约束；不配置时保持原刷怪规则。")]
        [SerializeField] private MonoBehaviour _spawnAdmission;
        private IEnemySpawnAdmission2D _admission;
        [Tooltip("可选的定点增援阵型；未配置时保留战区侧边刷怪。偏移使用世界单位。")]
        [SerializeField] private Transform _spawnAnchor;
        [SerializeField] private Vector2[] _spawnOffsets;
        private int _formationIndex;

        private System.Random _random;
        private float _remainingSeconds;
        private bool _isRunning;
        private bool _runtimeEnabled = true;
        private float _runtimeInitialDelaySeconds;
        private float _runtimeIntervalMultiplier = 1f;
        private int _runtimeMaximumAliveCount;
        private float _runtimeHealthMultiplier = 1f;
        private int _encounterMaximumAliveCount;
        private int _runtimeSpawnLimit, _spawnedCount;
        private float _runtimeSpawnInterval;
        private bool _runtimeSpawnAllAtOnce;
        private int _runtimeBatchSize, _runtimeBatchQuota;
        private float _runtimeBatchInterval;

        public int EffectiveMaximumAliveCount => _encounterMaximumAliveCount > 0
            ? Mathf.Min(_runtimeMaximumAliveCount, _encounterMaximumAliveCount)
            : _runtimeMaximumAliveCount;

        // 独立于章节调参；0 解除限制，不清除场上已生成的敌人。
        public void SetEncounterMaximumAliveCount(int count) =>
            _encounterMaximumAliveCount = Mathf.Max(0, count);

        public bool IsRunning => _isRunning;
        public EnemySpawnChannelDefinition Channel => _channel;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemySpawnDirector2D)}] 刷怪器装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _random = new System.Random(_schedule.RandomSeed);
            _admission = _spawnAdmission as IEnemySpawnAdmission2D;
            ResetRuntimeTuning();
        }

        private void Start()
        {
            if (_autoStart)
            {
                Begin();
            }
        }

        public void Simulate(float deltaTime)
        {
            if (!_isRunning || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds -= deltaTime;

            if (_remainingSeconds > 0f ||
                _pool.ActiveCount >= EffectiveMaximumAliveCount)
            {
                return;
            }

            if (_runtimeSpawnAllAtOnce)
            {
                // 有限批次在同一模拟刻生成；池不足时停止本刻尝试，不能无限循环。
                while (_isRunning && TrySpawnNow()) { }
                if (_isRunning && _spawnedCount >= _runtimeBatchQuota && _runtimeBatchQuota < _runtimeSpawnLimit)
                {
                    _runtimeBatchQuota = Mathf.Min(_runtimeSpawnLimit, _runtimeBatchQuota + _runtimeBatchSize);
                    _remainingSeconds = _runtimeBatchInterval;
                }
                return;
            }
            if (TrySpawnNow())
            {
                _remainingSeconds = _runtimeSpawnInterval > 0 ? _runtimeSpawnInterval :
                    _schedule.SampleInterval(_random) * _runtimeIntervalMultiplier;
            }
        }

        public void Begin()
        {
            if (_random == null)
            {
                return;
            }

            _isRunning = _runtimeEnabled;
            _remainingSeconds = _runtimeInitialDelaySeconds;
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void ApplyRuntimeTuning(
            bool enabledForSegment,
            float initialDelaySeconds,
            float intervalMultiplier,
            int maximumAliveCount, float healthMultiplier = 1f, int spawnLimit = 0, float spawnIntervalSeconds = 0,
            bool spawnAllAtOnce = false, int spawnBatchSize = 0, float spawnBatchIntervalSeconds = 0)
        {
            _runtimeEnabled = enabledForSegment;
            _runtimeSpawnLimit = Mathf.Max(0, spawnLimit); _spawnedCount = 0;
            _runtimeSpawnInterval = Mathf.Max(0, spawnIntervalSeconds);
            _runtimeSpawnAllAtOnce = spawnAllAtOnce && _runtimeSpawnLimit > 0;
            _runtimeBatchSize = _runtimeSpawnAllAtOnce ? Mathf.Max(0, spawnBatchSize) : 0;
            _runtimeBatchQuota = _runtimeBatchSize > 0 ? Mathf.Min(_runtimeBatchSize, _runtimeSpawnLimit) : _runtimeSpawnLimit;
            _runtimeBatchInterval = Mathf.Max(0, spawnBatchIntervalSeconds);
            _formationIndex = 0;
            _runtimeHealthMultiplier = Mathf.Max(1f, healthMultiplier);
            _runtimeInitialDelaySeconds = Mathf.Max(0f, initialDelaySeconds);
            _runtimeIntervalMultiplier = Mathf.Max(0.05f, intervalMultiplier);
            _runtimeMaximumAliveCount = Mathf.Max(1, maximumAliveCount);
            if (_runtimeEnabled)
            {
                _isRunning = true;
                _remainingSeconds = _runtimeInitialDelaySeconds;
            }
            else
            {
                Stop();
            }
        }

        private void ResetRuntimeTuning()
        {
            _runtimeEnabled = true;
            _runtimeInitialDelaySeconds = _schedule.InitialDelaySeconds;
            _runtimeIntervalMultiplier = 1f;
            _runtimeHealthMultiplier = 1f;
            _runtimeMaximumAliveCount = _schedule.MaximumAliveCount;
        }

        public bool TrySpawnNow()
        {
            if (_random == null || (_runtimeSpawnLimit > 0 && _spawnedCount >= _runtimeSpawnLimit) ||
                (_runtimeSpawnAllAtOnce && (_spawnedCount >= _runtimeBatchQuota || (_runtimeBatchSize > 0 && _remainingSeconds > 0))) ||
                _pool.ActiveCount >= EffectiveMaximumAliveCount || (_admission != null && !_admission.CanSpawn))
            {
                return false;
            }

            Rect bounds = _playfield.WorldBounds;
            float minimumY = bounds.yMin + _schedule.VerticalPadding;
            float maximumY = bounds.yMax - _schedule.VerticalPadding;
            float spawnY = Mathf.Lerp(
                minimumY,
                maximumY,
                (float)_random.NextDouble());
            Vector2 spawnPosition = new Vector2(
                bounds.xMax + _schedule.HorizontalSpawnMargin,
                spawnY);
            // 未开启的旧日程不额外抽随机数，保留其他怪物原有随机序列。
            bool fromLeft = _schedule.SpawnFromBothSides && _random.NextDouble() < .5;
            if (fromLeft) spawnPosition.x = bounds.xMin - _schedule.HorizontalSpawnMargin;
            if (_spawnAnchor != null)
                spawnPosition = (Vector2)_spawnAnchor.position + _spawnOffsets[_formationIndex % _spawnOffsets.Length];
            EnemySpawnVariation2D variation =
                _motionConfig.SampleVariation(_random, fromLeft ? Vector2.right : Vector2.left);

            bool spawned = _pool.TryRent(
                spawnPosition,
                in variation,
                out var actor);
            if (spawned)
            {
                _spawnedCount++;
                if (_runtimeSpawnLimit > 0 && _spawnedCount >= _runtimeSpawnLimit) Stop();
                if (_spawnAnchor != null) _formationIndex = (_formationIndex + 1) % _spawnOffsets.Length;
                // 每次从基础生命计算，不能把池化实体上一段的倍率叠乘进来。
                actor.Health.SetMaximumHealthBonus(0f);
                float baseHealth = actor.Health.MaximumHealth;
                actor.Health.SetMaximumHealthBonus(
                    baseHealth * _runtimeHealthMultiplier - baseHealth);
                actor.Health.ResetToMaximum();
            }
            return spawned;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_spawnAdmission != null && _spawnAdmission is not IEnemySpawnAdmission2D)
            { reason = "生成容量约束必须实现 IEnemySpawnAdmission2D。"; return false; }
            if (_spawnAnchor != null)
            {
                if (_spawnOffsets == null || _spawnOffsets.Length == 0)
                { reason = "定点增援缺少出生偏移。"; return false; }
                foreach (var offset in _spawnOffsets)
                    if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y))
                    { reason = "定点增援偏移必须有限。"; return false; }
            }
            if (_pool == null)
            {
                reason = "未配置敌人池。";
                return false;
            }

            if (!_pool.TryValidateConfiguration(out reason))
            {
                reason = $"敌人池无效：{reason}";
                return false;
            }

            if (_playfield == null)
            {
                reason = "未配置逻辑战斗区域。";
                return false;
            }

            if (!_playfield.TryValidate(out reason))
            {
                reason = $"逻辑战斗区域无效：{reason}";
                return false;
            }

            if (_motionConfig == null)
            {
                reason = "未配置敌人运动参数。";
                return false;
            }

            if (!_motionConfig.TryValidate(out reason))
            {
                reason = $"运动配置无效：{reason}";
                return false;
            }

            if (_schedule == null)
            {
                reason = "未配置刷怪日程。";
                return false;
            }

            if (_channel == null)
            {
                reason = "未配置刷怪频道身份。";
                return false;
            }

            if (!_schedule.TryValidate(out reason))
            {
                reason = $"刷怪日程无效：{reason}";
                return false;
            }

            if (_schedule.VerticalPadding * 2f >=
                _playfield.WorldBounds.height)
            {
                reason = "上下出生留白占满了整个战斗区域。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
