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
        [SerializeField, TextArea] private string _description;
        [SerializeField, Min(0)] private int _firstClearVoucherReward = 10;
        [SerializeField, Min(0)] private int _repeatClearVoucherReward = 5;
        [SerializeField] private bool _implemented = true;

        public string LevelId => _levelId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public int FirstClearVoucherReward => _firstClearVoucherReward;
        public int RepeatClearVoucherReward => _repeatClearVoucherReward;
        public bool Implemented => _implemented;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_levelId) ||
                string.IsNullOrWhiteSpace(_displayName))
            {
                reason = "关卡 ID 与名称不能为空。";
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
