using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Doubao
{
    [Serializable]
    public struct WordWallSlotDefinition
    {
        [SerializeField] private Vector2 _offset;
        [SerializeField, Min(0.01f)] private float _width;

        public Vector2 Offset => _offset;
        public float Width => _width;
        public bool IsValid => _width > 0f;
    }

    [Serializable]
    public sealed class WordWallPatternDefinition
    {
        [SerializeField] private string _displayName;
        [SerializeField] private WordWallSlotDefinition[] _slots;

        public string DisplayName => _displayName;
        public int SlotCount => _slots?.Length ?? 0;
        public WordWallSlotDefinition GetSlot(int index) => _slots[index];

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_displayName) || _slots == null || _slots.Length == 0)
            {
                reason = "形状名称和词块列表不能为空。";
                return false;
            }
            for (int index = 0; index < _slots.Length; index++)
            {
                if (!_slots[index].IsValid)
                {
                    reason = $"第 {index + 1} 个词块宽度无效。";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>豆包词墙的跨关卡共享规则；关卡只决定何时启用和摆在哪里。</summary>
    [CreateAssetMenu(
        fileName = "CFG_DB_WordWall_",
        menuName = "DeepSleep/配置/遭遇/豆包词墙")]
    public sealed class DoubaoWordWallConfig : ScriptableObject
    {
        [Header("时序")]
        [SerializeField, Min(0f)] private float _initialDelaySeconds;
        [SerializeField, Min(0.01f)] private float _groupIntervalSeconds;
        [SerializeField, Min(0.01f)] private float _bossRevealSeconds;
        [SerializeField, Min(0.01f)] private float _fallSpeed;
        [Header("连续迷宫（开启后替代散组间隔）")]
        [SerializeField] private bool _useMaze;
        [SerializeField, Range(3, 15)] private int _mazeColumns = 7;
        [SerializeField, Min(.1f)] private float _mazeRowPitch = 2.36f;
        [SerializeField, Min(0f)] private float _mazeSentenceGap = .12f;
        [SerializeField, Min(1)] private int _mazeHoldRows = 3;
        [SerializeField, Min(.1f)] private float _mazeRouteRadius = .75f;
        public bool UseMaze => _useMaze;
        public int MazeColumns => _mazeColumns;
        public float MazeRowPitch => _mazeRowPitch;
        public float MazeSentenceGap => _mazeSentenceGap;
        public int MazeHoldRows => _mazeHoldRows;
        public float MazeRouteRadius => _mazeRouteRadius;
        [Header("全屏小词块分布")]
        [SerializeField, Min(1)] private int _groupsPerWave = 4;
        [SerializeField, Min(0.1f)] private float _coverageWidth = 17.2f;
        [SerializeField, Min(0f)] private float _horizontalJitter = 0.45f;
        [SerializeField, Min(0f)] private float _verticalJitter = 0.6f;
        [Header("组间净通道（以 HS 常态宽高为标尺）")]
        [SerializeField] private Vector2 _referenceCharacterSize = new Vector2(2.45f, 2.45f);
        [SerializeField] private Vector2 _gapCharacterRange = new Vector2(1f, 1.5f);
        public Vector2 ReferenceCharacterSize => _referenceCharacterSize;
        public Vector2 GapCharacterRange => _gapCharacterRange;
        public Vector2 MaximumGroupSize
        {
            get
            {
                Vector2 maximum = Vector2.zero;
                foreach (var pattern in _patterns)
                {
                    Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                    for (int i = 0; i < pattern.SlotCount; i++)
                    {
                        var slot = pattern.GetSlot(i);
                        // 现役圆泡的视觉和碰撞直径都由 Width 决定。
                        Vector2 half = Vector2.one * slot.Width * 0.5f;
                        min = Vector2.Min(min, slot.Offset - half);
                        max = Vector2.Max(max, slot.Offset + half);
                    }
                    maximum = Vector2.Max(maximum, max - min);
                }
                return maximum;
            }
        }

        [Header("词块")]
        [SerializeField, Min(1f)] private float _blockMaximumHealth;
        [SerializeField, Min(1f)] private float _contactDamage;
        [SerializeField, Min(0.01f)] private float _blockHeight;
        [SerializeField, Min(0f)] private float _spawnTopPadding;
        [SerializeField, Min(0f)] private float _despawnBottomPadding;
        [SerializeField, Min(1)] private int _poolCapacity;
        [SerializeField] private string[] _phrases;
        [SerializeField] private WordWallPatternDefinition[] _patterns;

        [Header("本体")]
        [SerializeField, Min(1f)] private float _bossMaximumHealth;

        public float InitialDelaySeconds => _initialDelaySeconds;
        public float GroupIntervalSeconds => _groupIntervalSeconds;
        public float BossRevealSeconds => _bossRevealSeconds;
        public float FallSpeed => _fallSpeed;
        public int GroupsPerWave => _groupsPerWave;
        public float CoverageWidth => _coverageWidth;
        public float HorizontalJitter => _horizontalJitter;
        public float VerticalJitter => _verticalJitter;
        public float BlockMaximumHealth => _blockMaximumHealth;
        public float ContactDamage => _contactDamage;
        public float BlockHeight => _blockHeight;
        public float SpawnTopPadding => _spawnTopPadding;
        public float DespawnBottomPadding => _despawnBottomPadding;
        public int PoolCapacity => _poolCapacity;
        public float BossMaximumHealth => _bossMaximumHealth;
        public int PhraseCount => _phrases?.Length ?? 0;
        public int PatternCount => _patterns?.Length ?? 0;
        public string GetPhrase(int index) => _phrases[index];
        public WordWallPatternDefinition GetPattern(int index) => _patterns[index];

        public bool TryValidate(out string reason)
        {
            reason = string.Empty;
            if (_useMaze && (_mazeColumns < 3 || _mazeColumns > 15 || _mazeRowPitch <= 0f ||
                _mazeHoldRows < 1 || _mazeRouteRadius <= 0f || _poolCapacity > byte.MaxValue))
            {
                reason = "迷宫参数无效，且现有网络快照的气泡上限为 255。";
                return false;
            }
            if (_groupIntervalSeconds <= 0f || _bossRevealSeconds <= 0f || _fallSpeed <= 0f ||
                _blockMaximumHealth < 1f || _contactDamage < 1f || _blockHeight <= 0f ||
                _poolCapacity < 1 || _bossMaximumHealth < 1f ||
                _groupsPerWave < 1 || _coverageWidth <= 0f ||
                _horizontalJitter < 0f || _verticalJitter < 0f ||
                _referenceCharacterSize.x <= 0f || _referenceCharacterSize.y <= 0f ||
                _gapCharacterRange.x < 1f || _gapCharacterRange.y < _gapCharacterRange.x)
            {
                reason = "时序、速度、生命、接触伤害、词块高度、池容量或本体生命无效。";
                return false;
            }
            if (_phrases == null || _phrases.Length == 0 || _patterns == null || _patterns.Length == 0)
            {
                reason = "短语库和形状库不能为空。";
                return false;
            }
            for (int index = 0; index < _phrases.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(_phrases[index]))
                {
                    reason = $"第 {index + 1} 条短语为空。";
                    return false;
                }
            }
            for (int index = 0; index < _patterns.Length; index++)
            {
                if (_patterns[index] == null)
                {
                    reason = $"第 {index + 1} 个词墙形状为空。";
                    return false;
                }
                if (!_patterns[index].TryValidate(out reason))
                {
                    reason = $"第 {index + 1} 个词墙形状无效：{reason}";
                    return false;
                }
            }
            if (_useMaze && (_mazeSentenceGap < 0f ||
                _mazeRowPitch + .0001f < MaximumGroupSize.y + _mazeSentenceGap ||
                _coverageWidth / _mazeColumns + .0001f < MaximumGroupSize.x + _mazeSentenceGap))
            {
                reason = "迷宫行/列间距必须容纳完整句子，并保留句间细缝。";
                return false;
            }
            return true;
        }
    }
}
