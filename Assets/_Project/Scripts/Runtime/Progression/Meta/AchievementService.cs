using System;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    [DefaultExecutionOrder(-250)]
    public sealed class AchievementService : MonoBehaviour
    {
        [SerializeField] private LocalPlayerProfileStore _profile;
        [SerializeField] private AchievementDefinition[] _definitions;

        public event Action<AchievementDefinition> Unlocked;
        public AchievementDefinition[] Definitions => _definitions;

        public bool IsUnlocked(AchievementDefinition definition) =>
            definition != null &&
            _profile.IsAchievementUnlocked(definition.AchievementId);

        public int GetProgress(AchievementDefinition definition) =>
            definition == null
                ? 0
                : _profile.GetAchievementProgress(definition.TriggerId);

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError("[Achievements] " + reason, this);
                enabled = false;
            }
        }

        public void Report(string triggerId, int amount = 1)
        {
            if (!_profile.TryRecordAchievementEvent(
                    triggerId,
                    amount,
                    _definitions,
                    out AchievementDefinition[] newlyUnlocked,
                    out string message))
            {
                Debug.LogError("[Achievements] " + message, this);
                return;
            }

            for (int index = 0; index < newlyUnlocked.Length; index++)
                Unlocked?.Invoke(newlyUnlocked[index]);
        }

        public void ReportLevelCleared(MetaLevelDefinition level)
        {
            foreach (var definition in _definitions)
                if (definition.RequiredLevel == level && level != null)
                    Report(definition.TriggerId);
        }

        public void ReportChallengeCleared()
        {
            foreach (var definition in _definitions)
            {
                var required = definition.RequiredChallenges;
                if (required == null || required.Length == 0) continue;
                int completed = 0;
                foreach (var entry in required)
                    if (_profile.HasCompletedChallenge(entry.EntryId)) completed++;
                int missing = completed - GetProgress(definition);
                if (missing > 0) Report(definition.TriggerId, missing);
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_profile == null || _definitions == null ||
                _definitions.Length == 0)
            {
                reason = "档案与成就定义必须配置。";
                return false;
            }
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int index = 0; index < _definitions.Length; index++)
            {
                AchievementDefinition definition = _definitions[index];
                if (definition == null)
                {
                    reason = $"第 {index + 1} 个成就定义为空。";
                    return false;
                }
                if (!definition.TryValidate(out reason))
                    return false;
                if (!ids.Add(definition.AchievementId))
                {
                    reason = "成就 ID 重复：" + definition.AchievementId;
                    return false;
                }
                var challenges = definition.RequiredChallenges;
                if (challenges != null && challenges.Length > 0)
                {
                    var unique = new System.Collections.Generic.HashSet<string>();
                    foreach (var entry in challenges)
                        if (entry == null || !unique.Add(entry.EntryId))
                        { reason = "成就挑战条件缺失或重复。"; return false; }
                    if (definition.TargetCount != unique.Count)
                    { reason = "成就目标数须匹配指定的不同挑战数量。"; return false; }
                }
            }
            reason = string.Empty;
            return true;
        }
    }
}
