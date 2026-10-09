using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>章节实际倒计时是唯一状态时钟。统一战斗时间倍率，保留暂停所有权，不另加敌人倍率。</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class ChapterSceneEffectsController : MonoBehaviour
    {
        public ChapterRunController Chapter;
        public ChapterSceneEffectsConfig Config;
        [Tooltip("Boss独立节奏；为空时沿用普通配置。前三波始终只读Config。")]
        public ChapterSceneEffectsConfig BossConfig;
        public ChapterSceneEffectsConfig ActiveBossConfig => BossConfig != null ? BossConfig : Config;
        private bool _valid;
        private float _appliedScale = 1;
        private int _scheduleSeed, _scheduleWave;
        private ChapterSceneEffectsConfig _scheduleConfig;
        private SceneEffectPulse[] _schedule = System.Array.Empty<SceneEffectPulse>();
        private bool _bossClock, _bossStates;
        private float _bossElapsed, _bossScheduleDuration;
        private int _bossSeed;
        public bool BossClockActive => _bossClock;
        public bool BossStatesActive => _bossStates;
        public int BossClockSeed => _bossSeed;
        public float BossClockElapsed => _bossElapsed;

        public void ApplyBossClockReplica(bool active, bool states, int seed, float elapsed)
        {
            if (!active) { if (_bossClock) EndBossClock(); return; }
            if (!_bossClock || _bossStates != states || _bossSeed != seed) BeginBossClock(seed, states);
            _bossElapsed = elapsed;
            // 晚加入可以跳过多个时间表块，必须一次覆盖到当前时刻。
            float duration = Mathf.Max(60, (Mathf.Floor((_bossElapsed + ActiveBossConfig.StopBeforeEndSeconds) / 60) + 1) * 60);
            if (states && duration > _bossScheduleDuration)
            { _bossScheduleDuration = duration; _schedule = ActiveBossConfig.BuildSchedule(seed, 3, duration); }
            ApplyCurrentState();
        }

        public void BeginBossClock(int seed, bool statesActive)
        {
            _bossClock=true;_bossStates=statesActive;_bossSeed=seed;_bossElapsed=0;_bossScheduleDuration=60;
            _schedule=statesActive?ActiveBossConfig.BuildSchedule(seed,3,_bossScheduleDuration):System.Array.Empty<SceneEffectPulse>();
            ApplyCurrentState();
        }
        public void EndBossClock()
        {
            _bossClock=_bossStates=false;_bossElapsed=0;_scheduleConfig=null;_schedule=System.Array.Empty<SceneEffectPulse>();
            if(Time.timeScale>0 && Mathf.Approximately(Time.timeScale,_appliedScale))Time.timeScale=1;
            _appliedScale=1;
        }

        public bool IsBattleActive => _valid && isActiveAndEnabled && Chapter.IsInitialized &&
            (!Chapter.IsChallenge || (_bossClock && Chapter.Challenge.ChallengeKind == DeepSleep.Runtime.Progression.Bestiary.BestiaryChallengeKind.Claude)) &&
            !Chapter.LevelBindings.Session.IsExiting && Chapter.Phase == ChapterRunPhase.Combat && Chapter.RemainingCombatSeconds > 0;
        public float ElapsedSeconds => IsBattleActive ? (_bossClock?_bossElapsed:Chapter.LevelBindings.Level.ChapterRunConfig.GetSegment(Chapter.SegmentNumber).DurationSeconds - Chapter.RemainingCombatSeconds) : 0;
        public int Count(SceneBattleEffect effect, bool warning = false)
        {
            if(!IsBattleActive)return 0;
            if(_bossClock)
            {
                if(!_bossStates)return 0;
                if(_bossElapsed>=_bossScheduleDuration-ActiveBossConfig.StopBeforeEndSeconds)
                {
                    _bossScheduleDuration+=60;
                    _schedule=ActiveBossConfig.BuildSchedule(_bossSeed,3,_bossScheduleDuration);
                }
                return ChapterSceneEffectsConfig.CountAt(_schedule,_bossElapsed,effect,warning);
            }
            if(_scheduleConfig!=Config||_scheduleSeed!=Chapter.SceneEffectSeed||_scheduleWave!=Chapter.SegmentNumber)
            {
                _scheduleConfig=Config;_scheduleSeed=Chapter.SceneEffectSeed;_scheduleWave=Chapter.SegmentNumber;
                _schedule=Config.BuildSchedule(_scheduleSeed,_scheduleWave,Chapter.LevelBindings.Level.ChapterRunConfig.GetSegment(_scheduleWave).DurationSeconds);
            }
            return ChapterSceneEffectsConfig.CountAt(_schedule,ElapsedSeconds,effect,warning);
        }
        public float TimeMultiplier => Mathf.Pow(2, Count(SceneBattleEffect.DoubleSpeed) - Count(SceneBattleEffect.HalfSpeed));
        public void GetVisualTiming(SceneBattleEffect effect,out float warningProgress,out float remaining)
        {
            warningProgress=0;remaining=float.PositiveInfinity;
            if(!IsBattleActive)return;
            Count(effect); // 确保本波时间表已缓存。
            float elapsed=ElapsedSeconds;
            foreach(var p in _schedule)
            {
                if(p.Effect!=effect)continue;
                float start=p.FirstWarningSeconds+p.WarningSeconds;
                if(elapsed>=p.FirstWarningSeconds&&elapsed<start)
                    warningProgress=Mathf.Max(warningProgress,(elapsed-p.FirstWarningSeconds)/p.WarningSeconds);
                if(elapsed>=start&&elapsed<start+p.DurationSeconds)
                    remaining=Mathf.Min(remaining,start+p.DurationSeconds-elapsed);
            }
        }
        public bool InvertsMovement => (Count(SceneBattleEffect.InvertedMovement) & 1) != 0;
        public Vector2 TransformMovement(Vector2 move) => InvertsMovement ? -move : move;

        private void Awake()
        {
            _valid = TryValidateConfiguration(out string reason);
            if (!_valid) { Debug.LogError("[ChapterSceneEffects] " + reason, this); enabled = false; }
        }

        private void Update()
        {
            if(_bossClock && _bossStates && IsBattleActive && Time.timeScale>0)_bossElapsed+=Time.unscaledDeltaTime;
            ApplyCurrentState();
        }

        public void ApplyCurrentState()
        {
            // 0由菜单、选角或Router拥有；状态层绝不自行解除暂停。
            if (!_valid || Time.timeScale <= 0) return;
            _appliedScale = TimeMultiplier;
            Time.timeScale = _appliedScale;
        }

        private void OnDisable()
        {
            EndBossClock();
            if (Time.timeScale > 0 && Mathf.Approximately(Time.timeScale, _appliedScale)) Time.timeScale = 1;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Chapter == null || Chapter.LevelBindings == null || Chapter.LevelBindings.Level == null ||
                Chapter.LevelBindings.Session == null || Config == null)
            { reason = "缺少章节、关卡绑定、会话或状态配置。"; return false; }
            if (!Config.TryValidate(out reason)) return false;
            if (BossConfig != null && !BossConfig.TryValidate(out reason)) return false;
            foreach (var pulse in Config.Waves)
                if (pulse.Wave > Chapter.LevelBindings.Level.ChapterRunConfig.CombatSegmentCount)
                { reason = "状态时间表引用不存在的波次。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
