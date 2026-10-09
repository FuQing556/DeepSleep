using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Run;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    [CreateAssetMenu(
        fileName = "CFG_META_Level_",
        menuName = "DeepSleep/Progression/Meta Level")]
    public sealed class MetaLevelDefinition : ScriptableObject
    {
        [SerializeField] private string _levelId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _sceneName = "Gameplay_Prototype";
        [SerializeField, TextArea] private string _description;
        [SerializeField, Min(0)] private int _firstClearVoucherReward = 10;
        [SerializeField, Min(0)] private int _repeatClearVoucherReward = 10;
        [SerializeField] private bool _implemented = true;
        [SerializeField] private ChapterRunConfig _chapterRunConfig;
        [SerializeField] private LevelContentManifest _contentManifest;

        public string LevelId => _levelId;
        public string DisplayName => _displayName;
        public string SceneName => _sceneName;
        public string Description => _description;
        public int FirstClearVoucherReward => _firstClearVoucherReward;
        public int RepeatClearVoucherReward => _repeatClearVoucherReward;
        public bool Implemented => _implemented;
        public ChapterRunConfig ChapterRunConfig => _chapterRunConfig;
        public LevelContentManifest ContentManifest => _contentManifest;

        /// <summary>验证可开战的完整关卡定义；菜单与永久档案仍只验证元数据。</summary>
        public bool TryValidateGameplayDefinition(out string reason)
        {
            if (!TryValidate(out reason)) return false;
            if (_chapterRunConfig == null || _contentManifest == null)
            {
                reason = $"{_levelId} 缺少 _chapterRunConfig 或 _contentManifest，请显式装配关卡定义。";
                return false;
            }
            if (!_chapterRunConfig.TryValidate(out reason) ||
                !_contentManifest.TryValidate(out reason))
            {
                reason = $"{_levelId} 的关内配置无效：{reason}";
                return false;
            }
            for (int segment = 1; segment <= _chapterRunConfig.CombatSegmentCount; segment++)
            {
                foreach (SegmentSpawnRule rule in _chapterRunConfig.GetSegment(segment).SpawnRules)
                {
                    if (rule.Enabled && !_contentManifest.ContainsChannel(rule.Channel))
                    {
                        reason = $"{_levelId} 第 {segment} 段启用的频道 {rule.Channel.DisplayName} 未登记到 _contentManifest。";
                        return false;
                    }
                }
            }
            reason = string.Empty;
            return true;
        }

        public bool TryValidate(out string reason)
        {
            if (!LevelIdentityValidation.TryValidateLevelId(_levelId, out reason)) return false;
            if (string.IsNullOrWhiteSpace(_displayName) ||
                string.IsNullOrWhiteSpace(_sceneName))
            {
                reason = "关卡 ID、名称与场景名不能为空。";
                return false;
            }
            if (_firstClearVoucherReward < 0 ||
                _repeatClearVoucherReward < 0)
            {
                reason = $"{_displayName} 的鲸元券奖励不能为负数。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
