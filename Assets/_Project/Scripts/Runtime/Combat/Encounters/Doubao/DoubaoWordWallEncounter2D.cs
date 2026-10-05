using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Doubao
{
    public enum DoubaoEncounterState : byte
    {
        Idle,
        Prelude,
        Active,
        Complete
    }

    /// <summary>
    /// 可跨关卡复用的豆包遭遇：固定刻生成气泡组、按逻辑时间显形本体并在击败时当刻清场。
    /// </summary>
    public sealed class DoubaoWordWallEncounter2D :
        MonoBehaviour,
        IFixedSimulationStep
    {
        [SerializeField] private DoubaoWordWallConfig _config;
        [SerializeField] private CombatPlayfieldConfig _playfield;
        [SerializeField] private DoubaoWordWallBlock2D _blockPrefab;
        [SerializeField] private Transform _blockPoolRoot;
        [SerializeField] private DoubaoBoss2D _boss;
        [SerializeField] private Transform _bossAnchor;
        [SerializeField] private Transform _groupCenterAnchor;
        [SerializeField] private bool _beginOnEnable;
        [SerializeField] private int _deterministicSeed;
        [SerializeField] private DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D _impactEffects;
        [SerializeField] private DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D _breakEffects;
        [SerializeField] private CompanionObstacleRegistry2D _obstacleRegistry;

        private readonly Queue<DoubaoWordWallBlock2D> _available = new();
        private readonly List<DoubaoWordWallBlock2D> _active = new();
        private System.Random _random;
        private float _elapsed;
        private float _nextGroupSeconds;
        private bool _poolWarningIssued;
        private bool _isInitialized;
        private uint _nextReplicationId;
        private float _firstWaveArrivalSeconds;
        private DoubaoMazeRoute _mazeRoute;
        private int _mazeRow;

        public event Action<DoubaoWordWallEncounter2D> Completed;
        public DoubaoEncounterState State { get; private set; } =
            DoubaoEncounterState.Idle;
        public IReadOnlyList<DoubaoWordWallBlock2D> ActiveBlocks => _active;
        public DoubaoBoss2D Boss => _boss;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError($"[{nameof(DoubaoWordWallEncounter2D)}] {reason}", this);
                enabled = false;
                return;
            }

            for (int index = 0; index < _config.PoolCapacity; index++)
            {
                DoubaoWordWallBlock2D block = Instantiate(
                    _blockPrefab,
                    _blockPoolRoot);
                block.name = $"DB_WordBubble_{index + 1:00}";
                block.ReturnToPool();
                block.Popped += OnBlockPopped;
                _available.Enqueue(block);
            }
            _boss.ResetEncounter();
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                _boss.Defeated += OnBossDefeated;
                if (_beginOnEnable) BeginEncounter();
            }
        }

        private void OnDisable()
        {
            if (_boss != null) _boss.Defeated -= OnBossDefeated;
            if (_isInitialized) ResetEncounter();
        }

        public void BeginEncounter()
        {
            if (!_isInitialized || State != DoubaoEncounterState.Idle) return;
            _random = new System.Random(_deterministicSeed);
            _elapsed = 0f;
            _nextGroupSeconds = _config.InitialDelaySeconds;
            _poolWarningIssued = false;
            _nextReplicationId = 0;
            _firstWaveArrivalSeconds = float.PositiveInfinity;
            _mazeRow = 0;
            _mazeRoute = _config.UseMaze ? new DoubaoMazeRoute(_deterministicSeed,
                _config.MazeColumns, _config.MazeHoldRows,
                Mathf.Min(_config.CoverageWidth, _playfield.WorldBounds.width), _config.MazeRowPitch) : null;
            State = DoubaoEncounterState.Prelude;
        }

        public void Simulate(float deltaTime)
        {
            if (!_isInitialized || State == DoubaoEncounterState.Idle ||
                State == DoubaoEncounterState.Complete || deltaTime <= 0f)
            {
                return;
            }

            _elapsed += deltaTime;
            if (State == DoubaoEncounterState.Prelude &&
                _elapsed >= Mathf.Max(_config.BossRevealSeconds, _firstWaveArrivalSeconds))
            {
                _boss.Activate(_bossAnchor.position, _config.BossMaximumHealth);
                State = DoubaoEncounterState.Active;
            }

            // 本体与堤岸平台共用显式场景锚点，不跟屏幕宽高比改变玩法位置。
            if (State == DoubaoEncounterState.Active && _boss.IsActive)
                _boss.PlaceAt(_bossAnchor.position);

            // 先推进已有气泡。新排按预定出生时刻补偿本步超出的时间，
            // 避免固定步取整（或同一步补发多排）把句间细缝挤掉。
            for (int index = _active.Count - 1; index >= 0; index--)
            {
                DoubaoWordWallBlock2D block = _active[index];
                if (block != null && block.IsActive) block.Simulate(deltaTime);
            }

            while (_elapsed >= _nextGroupSeconds)
            {
                SpawnWave(_elapsed - _nextGroupSeconds);
                if (_config.UseMaze)
                {
                    _nextGroupSeconds += _config.MazeRowPitch / _config.FallSpeed;
                    continue;
                }
                float rowGap = Mathf.Lerp(_config.GapCharacterRange.x, _config.GapCharacterRange.y,
                    (float)_random.NextDouble()) * _config.ReferenceCharacterSize.y;
                // 同速下降的整排之间先留净通道，不能被发射间隔或随机Y偏移挤没。
                _nextGroupSeconds += Mathf.Max(_config.GroupIntervalSeconds,
                    (_config.MaximumGroupSize.y + rowGap) / _config.FallSpeed);
            }

        }

        public void ResetEncounter()
        {
            for (int index = _active.Count - 1; index >= 0; index--)
            {
                ReturnBlock(_active[index]);
            }
            _active.Clear();
            _boss.ResetEncounter();
            _elapsed = 0f;
            _nextGroupSeconds = 0f;
            State = DoubaoEncounterState.Idle;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            reason = string.Empty;
            if (_config == null)
            {
                reason = "未配置豆包词墙参数。";
                return false;
            }
            if (!_config.TryValidate(out reason))
            {
                reason = "词墙配置无效：" + reason;
                return false;
            }
            if (_playfield == null)
            {
                reason = "未配置玩法区域。";
                return false;
            }
            if (!_playfield.TryValidate(out reason))
            {
                reason = "玩法区域配置无效：" + reason;
                return false;
            }
            if (_config.UseMaze && Mathf.Min(_config.CoverageWidth, _playfield.WorldBounds.width) /
                _config.MazeColumns + .0001f < _config.MaximumGroupSize.x + _config.MazeSentenceGap)
            {
                reason = "玩法区域过窄，无法在迷宫各列之间保留句间细缝。";
                return false;
            }
            if (_blockPrefab == null || _blockPoolRoot == null || _boss == null ||
                _bossAnchor == null || _groupCenterAnchor == null || _impactEffects == null || _breakEffects == null ||
                _obstacleRegistry == null)
            {
                reason = "气泡 Prefab、对象池根、本体、场景锚点、特效池与 AI 障碍登记表必须完整配置。";
                return false;
            }
            return true;
        }

        private void SpawnWave(float ageSeconds)
        {
            if (_config.UseMaze) { SpawnMazeWave(ageSeconds); return; }
            float width = Mathf.Min(_config.CoverageWidth, _playfield.WorldBounds.width);
            float groupWidth = _config.MaximumGroupSize.x;
            float minGap = _config.ReferenceCharacterSize.x * _config.GapCharacterRange.x;
            int count = Mathf.Min(_config.GroupsPerWave, Mathf.FloorToInt((width + minGap) / (groupWidth + minGap)));
            if (count < 1) return;
            float maxGap = count > 1 ? Mathf.Min(_config.ReferenceCharacterSize.x * _config.GapCharacterRange.y,
                (width - count * groupWidth) / (count - 1)) : minGap;
            float gap = Mathf.Lerp(minGap, maxGap, (float)_random.NextDouble());
            float occupiedWidth = count * groupWidth + (count - 1) * gap;
            float center = Mathf.Clamp(_groupCenterAnchor.position.x,
                _playfield.WorldBounds.xMin + width * 0.5f,
                _playfield.WorldBounds.xMax - width * 0.5f);
            bool firstWave = float.IsPositiveInfinity(_firstWaveArrivalSeconds);
            // 每波整体横移半个分区以内，避免分区之间形成永远不落词块的固定安全直线。
            float waveShift = ((float)_random.NextDouble() - 0.5f) * (width - occupiedWidth);
            for (int group = 0; group < count; group++)
            {
                float x = center - occupiedWidth * 0.5f + groupWidth * 0.5f + group * (groupWidth + gap) + waveShift;
                SpawnNextGroup(x, -ageSeconds * _config.FallSpeed, 1f, firstWave);
            }
        }

        private void SpawnMazeWave(float ageSeconds)
        {
            float width = Mathf.Min(_config.CoverageWidth, _playfield.WorldBounds.width);
            float originX = _playfield.WorldBounds.center.x;
            bool first = float.IsPositiveInfinity(_firstWaveArrivalSeconds);
            for (int column = 0; column < _config.MazeColumns; column++)
            {
                int patternIndex = _random.Next(_config.PatternCount);
                var pattern = _config.GetPattern(patternIndex);
                float x = -width * .5f + (column + .5f) * width / _config.MazeColumns;
                bool blocked = false;
                for (int i = 0; i < pattern.SlotCount; i++)
                {
                    var slot = pattern.GetSlot(i);
                    Vector2 position = new Vector2(x + slot.Offset.x,
                        _mazeRow * _config.MazeRowPitch + slot.Offset.y);
                    if (_mazeRoute.IntersectsRoute(position,
                        _config.MazeRouteRadius + slot.Width * .5f, _mazeRow))
                    { blocked = true; break; }
                }
                // 完整保留单字短语组，不把保护路线时删剩的残字当作短语。
                if (!blocked) SpawnNextGroup(originX + x, -ageSeconds * _config.FallSpeed, 1f, first, patternIndex);
            }
            _mazeRow++;
        }

        private void SpawnNextGroup(float centerX, float verticalOffset, float mirror, bool firstWave, int chosenPattern = -1)
        {
            int patternIndex = chosenPattern >= 0 ? chosenPattern : _random.Next(0, _config.PatternCount);
            WordWallPatternDefinition pattern = _config.GetPattern(patternIndex);
            // 一组拼出一句三/四字短语；不再把一整句话塞进每个气泡。
            string groupPhrase = null;
            int phraseStart = _random.Next(_config.PhraseCount);
            for (int i = 0; i < _config.PhraseCount; i++)
            {
                string candidate = _config.GetPhrase((phraseStart + i) % _config.PhraseCount);
                if (candidate.Length == pattern.SlotCount) { groupPhrase = candidate; break; }
            }
            if (groupPhrase == null) return;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            for (int index = 0; index < pattern.SlotCount; index++)
            {
                WordWallSlotDefinition slot = pattern.GetSlot(index);
                left = Mathf.Min(left, slot.Offset.x * mirror - slot.Width * 0.5f);
                right = Mathf.Max(right, slot.Offset.x * mirror + slot.Width * 0.5f);
            }
            centerX = Mathf.Clamp(centerX, _playfield.WorldBounds.xMin - left,
                _playfield.WorldBounds.xMax - right);
            float top = _playfield.WorldBounds.yMax + _config.SpawnTopPadding;
            float bottom = _playfield.WorldBounds.yMin - _config.DespawnBottomPadding;
            for (int index = 0; index < pattern.SlotCount; index++)
            {
                if (_available.Count == 0)
                {
                    if (!_poolWarningIssued)
                    {
                        Debug.LogWarning("[豆包词墙] 对象池已满，本组剩余气泡跳过；请调大配置池容量或降低密度。", this);
                        _poolWarningIssued = true;
                    }
                    return;
                }

                WordWallSlotDefinition slot = pattern.GetSlot(index);
                DoubaoWordWallBlock2D block = _available.Dequeue();
                Vector2 position = new Vector2(
                    Mathf.Clamp(centerX + slot.Offset.x * mirror,
                        _playfield.WorldBounds.xMin + slot.Width * 0.5f,
                        _playfield.WorldBounds.xMax - slot.Width * 0.5f),
                    top + verticalOffset + slot.Offset.y);
                if (firstWave)
                {
                    // 按首波逻辑路程显形，不依赖词块是否已被攻击/碰撞回收。
                    float arrival = _elapsed +
                        (position.y - _playfield.WorldBounds.yMin) / _config.FallSpeed;
                    _firstWaveArrivalSeconds = Mathf.Min(_firstWaveArrivalSeconds, arrival);
                }
                string phrase = groupPhrase[index].ToString();
                block.transform.SetParent(null, true);
                block.Activate(
                    position,
                    ++_nextReplicationId,
                    slot.Width,
                    _config.BlockHeight,
                    phrase,
                    _config.BlockMaximumHealth,
                    _config.FallSpeed,
                    bottom,
                    _config.ContactDamage,
                    ReturnBlock);
                _active.Add(block);
                _obstacleRegistry.Register(block.GetComponent<CircleCollider2D>(),
                    Vector2.down * _config.FallSpeed, block);
            }
        }

        private void ReturnBlock(DoubaoWordWallBlock2D block)
        {
            if (block == null) return;
            if (_obstacleRegistry != null)
                _obstacleRegistry.Unregister(block.GetComponent<CircleCollider2D>());
            _active.Remove(block);
            block.transform.SetParent(_blockPoolRoot, false);
            block.ReturnToPool();
            _available.Enqueue(block);
        }

        private void OnBossDefeated(DoubaoBoss2D boss)
        {
            State = DoubaoEncounterState.Complete;
            for (int index = _active.Count - 1; index >= 0; index--)
            {
                _active[index].ClearWithEffect();
            }
            _active.Clear();
            Completed?.Invoke(this);
        }

        private void OnBlockPopped(Vector2 point, bool impact)
        {
            (impact ? _impactEffects : _breakEffects).TryPlay(point, 0f);
        }
    }
}
