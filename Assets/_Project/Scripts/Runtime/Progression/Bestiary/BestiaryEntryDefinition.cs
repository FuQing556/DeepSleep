using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Bestiary
{
    /// <summary>图鉴展示及挑战入口数据；复用正式关卡，不保存战斗状态或永久奖励。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/Progression/Bestiary Entry")]
    public sealed class BestiaryEntryDefinition : ScriptableObject
    {
        public string EntryId, DisplayName, Subtitle;
        [TextArea(3, 8)] public string Description;
        [TextArea(5, 14)] public string SkillNotes;
        public Sprite Portrait, PreparationBackdrop;
        public RestNodePreparationLayout PreparationLayout;
        public MetaLevelDefinition Level;
        public int CombatSegment, StartingTokensPerRole;
        public float PreludeSeconds;
        [TextArea] public string PreparationPrompt;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(EntryId) || string.IsNullOrWhiteSpace(DisplayName) ||
                Portrait == null || PreparationBackdrop == null || PreparationLayout?.Hotspots == null ||
                PreparationLayout.BackgroundScale.x <= 0 || PreparationLayout.BackgroundScale.y <= 0 ||
                Level == null || StartingTokensPerRole < 0 ||
                !float.IsFinite(PreludeSeconds) || PreludeSeconds <= 0 ||
                !Level.TryValidateGameplayDefinition(out reason))
            { reason = "图鉴条目身份、素材、正式关卡或挑战配装参数无效。"; return false; }
            if (CombatSegment < 1 || CombatSegment > Level.ChapterRunConfig.CombatSegmentCount)
            { reason = "图鉴挑战战斗段不属于正式关卡。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
