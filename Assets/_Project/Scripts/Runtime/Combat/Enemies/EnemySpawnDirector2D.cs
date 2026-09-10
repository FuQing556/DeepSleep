using System;
using DeepSleep.Runtime.World.Playfield;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 第一版单敌种刷怪器：在固定逻辑战斗区右侧随机高度生成，
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

        private System.Random _random;
        private float _remainingSeconds;
        private bool _isRunning;
        private bool _runtimeEnabled = true;
        private float _runtimeInitialDelaySeconds;
        private float _runtimeIntervalMultiplier = 1f;
        private int _runtimeMaximumAliveCount;

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
                _pool.ActiveCount >= _runtimeMaximumAliveCount)
            {
                return;
            }

            if (TrySpawnNow())
            {
                _remainingSeconds = _schedule.SampleInterval(_random) *
                    _runtimeIntervalMultiplier;
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
            int maximumAliveCount)
        {
            _runtimeEnabled = enabledForSegment;
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
            _runtimeMaximumAliveCount = _schedule.MaximumAliveCount;
        }

        public bool TrySpawnNow()
        {
            if (_random == null ||
                _pool.ActiveCount >= _runtimeMaximumAliveCount)
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
            EnemySpawnVariation2D variation =
                _motionConfig.SampleVariation(_random, Vector2.left);

            return _pool.TryRent(
                spawnPosition,
                in variation,
                out _);
        }

        public bool TryValidateConfiguration(out string reason)
        {
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
