using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>把可复用豆包遭遇接到指定章节战斗段，不把关卡规则写进豆包本体。</summary>
    public sealed class DoubaoChapterEncounterDriver2D :
        MonoBehaviour,
        IFixedSimulationStep,
        IChapterCombatObjective,
        IChapterCombatLifecycle
    {
        [SerializeField] private ChapterRunController _chapterRun;
        [SerializeField] private DoubaoWordWallEncounter2D _encounter;
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private int[] _segmentNumbers = System.Array.Empty<int>();
        [SerializeField] private EnemyTokenRewardController _rewards;
        [SerializeField, Min(0)] private int _tokenReward;
        [SerializeField] private EnemySpawnDirector2D[] _spawnDirectors = System.Array.Empty<EnemySpawnDirector2D>();
        [SerializeField, Min(1)] private int _bossMaximumAlivePerChannel = 3;

        private bool _combatStopped;

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;
        private bool CanAuthor => !IsOnline || _session.IsAuthority;

        public bool IsComplete =>
            _encounter != null &&
            _encounter.State == DoubaoEncounterState.Complete;

        private void Awake()
        {
            if (_chapterRun == null || _encounter == null || _rewards == null ||
                _segmentNumbers == null || _segmentNumbers.Length == 0 || _tokenReward < 0 ||
                System.Array.Exists(_segmentNumbers, number => number < 1))
            {
                Debug.LogError($"[{nameof(DoubaoChapterEncounterDriver2D)}] 必须配置章节、遭遇、奖励入口和有效战斗段。", this);
                enabled = false;
            }
        }

        public bool IsRequiredForSegment(int segmentNumber) =>
            !_chapterRun.IsChallenge && _segmentNumbers != null && System.Array.IndexOf(_segmentNumbers, segmentNumber) >= 0;

        private void OnEnable()
        {
            if (_encounter != null) _encounter.Completed += OnEncounterCompleted;
        }

        private void OnEncounterCompleted(DoubaoWordWallEncounter2D encounter)
        {
            if (CanAuthor && !_combatStopped && _chapterRun.Phase == ChapterRunPhase.Combat &&
                IsRequiredForSegment(_chapterRun.SegmentNumber))
                _rewards.RecordEncounterReward(_tokenReward);
        }

        public void ResetForSegment(int segmentNumber)
        {
            _combatStopped = false;
            SetSpawnCap(false);
            _encounter.ResetEncounter();
        }

        public void StopCombat(ChapterCombatStopReason reason)
        {
            // 停止调用的当刻清掉碰撞/气泡/刷怪上限，不等待下一次 FixedUpdate。
            _combatStopped = true;
            SetSpawnCap(false);
            if (_encounter != null) _encounter.ResetEncounter();
        }

        public void Simulate(float deltaTime)
        {
            // 豆包遭遇目前由主机负责真实生成、移动和伤害；客户端不能各自运行一套碰撞逻辑。
            if (!CanAuthor) { SetSpawnCap(false); return; }

            if (_combatStopped || _chapterRun.Phase != ChapterRunPhase.Combat ||
                !IsRequiredForSegment(_chapterRun.SegmentNumber))
            {
                SetSpawnCap(false);
                return;
            }

            if (_encounter.State == DoubaoEncounterState.Idle)
                _encounter.BeginEncounter(_chapterRun.CurrentEnemyHealthMultiplier);
            _encounter.Simulate(deltaTime);
            SetSpawnCap(_encounter.State == DoubaoEncounterState.Active);
        }

        private void OnDisable()
        {
            if (_encounter != null) _encounter.Completed -= OnEncounterCompleted;
            SetSpawnCap(false);
        }

        private void SetSpawnCap(bool active)
        {
            foreach (var director in _spawnDirectors)
                if (director != null)
                    director.SetEncounterMaximumAliveCount(active ? _bossMaximumAlivePerChannel : 0);
        }
    }
}
