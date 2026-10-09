using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    [CreateAssetMenu(
        fileName = "CFG_META_Achievement_",
        menuName = "DeepSleep/Progression/Achievement")]
    public sealed class AchievementDefinition : ScriptableObject
    {
        [SerializeField] private string _achievementId;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private bool _hidden;
        [SerializeField] private string _triggerId;
        [SerializeField, Min(1)] private int _targetCount = 1;
        [SerializeField] private Sprite _icon;
        [SerializeField] private MetaLevelDefinition _requiredLevel;
        [SerializeField] private DeepSleep.Runtime.Progression.Bestiary.BestiaryEntryDefinition[] _requiredChallenges;

        public string AchievementId => _achievementId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public bool Hidden => _hidden;
        public string TriggerId => _triggerId;
        public int TargetCount => _targetCount;
        public Sprite Icon => _icon;
        public MetaLevelDefinition RequiredLevel => _requiredLevel;
        public DeepSleep.Runtime.Progression.Bestiary.BestiaryEntryDefinition[] RequiredChallenges => _requiredChallenges;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_achievementId) ||
                string.IsNullOrWhiteSpace(_displayName) ||
                string.IsNullOrWhiteSpace(_triggerId))
            {
                reason = "成就 ID、名称与触发 ID 不能为空。";
                return false;
            }
            if (_targetCount <= 0)
            {
                reason = $"{_displayName} 的目标次数必须大于零。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    public static class AchievementTriggerIds
    {
        public const string JourneyStarted = "journey_started";
        public const string LevelCleared = "level_cleared";
        public const string TeammateRevived = "teammate_revived";
        public const string ProductPurchased = "product_purchased";
        public const string FlawlessLevelCleared = "flawless_level_cleared";
    }
}
