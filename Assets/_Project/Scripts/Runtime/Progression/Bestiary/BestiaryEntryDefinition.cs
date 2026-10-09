using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Bestiary
{
    public enum BestiaryChallengeKind { Kimi, Doubao, EnemyChannel, Claude }
    /// <summary>图鉴展示及挑战入口数据；复用正式关卡，挑战奖励独立于正式关卡首通记录。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/Progression/Bestiary Entry")]
    public sealed class BestiaryEntryDefinition : ScriptableObject
    {
        [System.Serializable]
        public sealed class ChallengePoolCapacity
        {
            public DeepSleep.Runtime.Combat.Enemies.EnemySpawnChannelDefinition Channel;
            [Min(1)] public int Capacity;
        }
        public ChallengePoolCapacity[] PoolCapacities = System.Array.Empty<ChallengePoolCapacity>();
        public string EntryId, DisplayName, Subtitle;
        [TextArea(3, 8)] public string Description;
        [TextArea(5, 14)] public string SkillNotes;
        public Sprite Portrait, PreparationBackdrop;
        public RestNodePreparationLayout PreparationLayout;
        public MetaLevelDefinition Level;
        public int CombatSegment, StartingTokensPerRole;
        public BestiaryChallengeKind ChallengeKind;
        public bool IsBossChallenge => ChallengeKind == BestiaryChallengeKind.Kimi || ChallengeKind == BestiaryChallengeKind.Claude;
        public DeepSleep.Runtime.Combat.Enemies.EnemySpawnChannelDefinition EnemyChannel;
        [Min(1)] public int EnemyCount;
        [Tooltip("0沿用原刷怪方式；大于0时按批同时生成，批间按游戏秒间隔，不等待清场。")]
        [Min(0)] public int EnemyBatchSize;
        [Min(0)] public float EnemyBatchIntervalSeconds;
        public int InitialEnemyBatchCount => EnemyBatchSize > 0 ? Mathf.Min(EnemyBatchSize, EnemyCount) : EnemyCount;
        public bool SpawnAllAtStart;
        [Min(.01f)] public float SpawnIntervalSeconds;
        [Min(1)] public int MaximumAlive;
        [Min(1)] public float ChallengeSeconds;
        [Min(0)] public int CompletionVoucherReward;
        public float PreludeSeconds;
        [TextArea] public string PreparationPrompt;

        public string ChallengeSummary
        {
            get
            {
                string formation = ChallengeKind == BestiaryChallengeKind.EnemyChannel
                    ? EnemyBatchSize > 0 && EnemyCount > EnemyBatchSize
                        ? $"共{EnemyCount}只，每批{EnemyBatchSize}只；每隔{EnemyBatchIntervalSeconds:0.#}游戏秒刷下一批，不等待清场。"
                        : $"独立挑战：共{EnemyCount}只敌人。"
                    : "单体挑战：击败目标后结束。";
                string completion = ChallengeKind == BestiaryChallengeKind.EnemyChannel
                    ? "所有敌人及其分裂后代离场后结束。" : "击败目标后结束。";
                return formation + (ChallengeKind == BestiaryChallengeKind.EnemyChannel ? "\n" + completion : string.Empty) + $"\n初始配装资金：每名角色{StartingTokensPerRole} Token。\n" +
                    (CompletionVoucherReward > 0 ? $"通关奖励：{CompletionVoucherReward}鲸元券。" : "不产出鲸元券。");
            }
        }

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(EntryId) || string.IsNullOrWhiteSpace(DisplayName) ||
                Portrait == null || PreparationBackdrop == null || PreparationLayout?.Hotspots == null ||
                PreparationLayout.BackgroundScale.x <= 0 || PreparationLayout.BackgroundScale.y <= 0 ||
                Level == null || StartingTokensPerRole < 0 || CompletionVoucherReward < 0 ||
                !float.IsFinite(PreludeSeconds) || PreludeSeconds <= 0 ||
                !Level.TryValidateGameplayDefinition(out reason))
            { reason = "图鉴条目身份、素材、正式关卡或挑战配装参数无效。"; return false; }
            if (CombatSegment < 1 || CombatSegment > Level.ChapterRunConfig.CombatSegmentCount)
            { reason = "图鉴挑战战斗段不属于正式关卡。"; return false; }
            if (!System.Enum.IsDefined(typeof(BestiaryChallengeKind), ChallengeKind))
            { reason = "未知图鉴挑战类型。"; return false; }
            if (PoolCapacities == null) { reason = "挑战池容量未配置。"; return false; }
            var channels = new System.Collections.Generic.HashSet<DeepSleep.Runtime.Combat.Enemies.EnemySpawnChannelDefinition>();
            foreach (var pool in PoolCapacities)
                if (pool == null || pool.Channel == null || pool.Capacity < 1 ||
                    !Level.ContentManifest.ContainsChannel(pool.Channel) || !channels.Add(pool.Channel))
                { reason = "挑战池容量或频道无效/重复。"; return false; }
            if (!IsBossChallenge && (!float.IsFinite(ChallengeSeconds) || ChallengeSeconds <= 0 ||
                (ChallengeKind == BestiaryChallengeKind.EnemyChannel && (EnemyChannel == null || EnemyCount < 1 || MaximumAlive < 1 || EnemyBatchSize < 0 ||
                !float.IsFinite(SpawnIntervalSeconds) || SpawnIntervalSeconds <= 0 ||
                !float.IsFinite(EnemyBatchIntervalSeconds) || EnemyBatchIntervalSeconds < 0 ||
                (SpawnAllAtStart && MaximumAlive < InitialEnemyBatchCount) || !Level.ContentManifest.ContainsChannel(EnemyChannel)))))
            { reason = "图鉴单怪挑战频道、数量、间隔或时长无效。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
