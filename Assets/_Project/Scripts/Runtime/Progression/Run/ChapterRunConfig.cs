using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    public enum RestNodeRecoveryMode : byte
    {
        FullRestore,
        ReviveOnly,
        Disabled
    }

    [CreateAssetMenu(
        fileName = "CFG_ChapterRun_",
        menuName = "DeepSleep/Progression/Chapter Run Config")]
    public sealed class ChapterRunConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _combatDurationSeconds = 60f;
        [SerializeField, Min(1)] private int _requiredDefeats = 10;
        [SerializeField, Min(1)] private int _combatSegmentCount = 3;
        [SerializeField, Min(0.1f)] private float _singleDownedTimeoutSeconds = 10f;
        [SerializeField, Min(0.1f)] private float _teamDownedTimeoutSeconds = 2f;
        [SerializeField, Min(0.1f)] private float _defeatPresentationSeconds = 2f;
        [SerializeField] private RestNodeRecoveryMode _restNodeRecovery =
            RestNodeRecoveryMode.FullRestore;
        [SerializeField, Range(0.01f, 1f)] private float _reviveOnlyHealthFraction = 0.333f;
        [SerializeField, Min(0.01f)] private float _checkpointInvulnerabilitySeconds = 1.5f;

        public float CombatDurationSeconds => _combatDurationSeconds;
        public int RequiredDefeats => _requiredDefeats;
        public int CombatSegmentCount => _combatSegmentCount;
        public float SingleDownedTimeoutSeconds => _singleDownedTimeoutSeconds;
        public float TeamDownedTimeoutSeconds => _teamDownedTimeoutSeconds;
        public float DefeatPresentationSeconds => _defeatPresentationSeconds;
        public RestNodeRecoveryMode RestNodeRecovery => _restNodeRecovery;
        public float ReviveOnlyHealthFraction => _reviveOnlyHealthFraction;
        public float CheckpointInvulnerabilitySeconds =>
            _checkpointInvulnerabilitySeconds;

        public bool TryValidate(out string reason)
        {
            if (_combatDurationSeconds <= 0f || _requiredDefeats <= 0 ||
                _combatSegmentCount <= 0 ||
                _singleDownedTimeoutSeconds <= 0f ||
                _teamDownedTimeoutSeconds <= 0f ||
                _defeatPresentationSeconds <= 0f ||
                _reviveOnlyHealthFraction <= 0f ||
                _checkpointInvulnerabilitySeconds <= 0f)
            {
                reason = "章节时长、目标、倒地限制和恢复参数必须为正数。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
