using System;
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
        [SerializeField] private ChapterCombatSegmentDefinition[] _segments;
        [SerializeField, Min(0.1f)] private float _singleDownedTimeoutSeconds = 10f;
        [SerializeField, Min(0.1f)] private float _teamDownedTimeoutSeconds = 2f;
        [SerializeField, Min(0.1f)] private float _defeatPresentationSeconds = 2f;
        [SerializeField] private RestNodeRecoveryMode _restNodeRecovery =
            RestNodeRecoveryMode.FullRestore;
        [SerializeField, Range(0.01f, 1f)] private float _reviveOnlyHealthFraction = 0.333f;
        [SerializeField, Min(0.01f)] private float _checkpointInvulnerabilitySeconds = 1.5f;

        public int CombatSegmentCount => _segments?.Length ?? 0;
        public float SingleDownedTimeoutSeconds => _singleDownedTimeoutSeconds;
        public float TeamDownedTimeoutSeconds => _teamDownedTimeoutSeconds;
        public float DefeatPresentationSeconds => _defeatPresentationSeconds;
        public RestNodeRecoveryMode RestNodeRecovery => _restNodeRecovery;
        public float ReviveOnlyHealthFraction => _reviveOnlyHealthFraction;
        public float CheckpointInvulnerabilitySeconds =>
            _checkpointInvulnerabilitySeconds;

        public ChapterCombatSegmentDefinition GetSegment(int oneBasedNumber)
        {
            if (_segments == null || oneBasedNumber < 1 ||
                oneBasedNumber > _segments.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(oneBasedNumber));
            }
            return _segments[oneBasedNumber - 1];
        }

        public bool TryValidate(out string reason)
        {
            if (_segments == null || _segments.Length == 0 ||
                _singleDownedTimeoutSeconds <= 0f ||
                _teamDownedTimeoutSeconds <= 0f ||
                _defeatPresentationSeconds <= 0f ||
                _reviveOnlyHealthFraction <= 0f ||
                _checkpointInvulnerabilitySeconds <= 0f)
            {
                reason = "章节时长、目标、倒地限制和恢复参数必须为正数。";
                return false;
            }

            for (int index = 0; index < _segments.Length; index++)
            {
                if (_segments[index] == null)
                {
                    reason = $"第 {index + 1} 段配置为空。";
                    return false;
                }
                if (!_segments[index].TryValidate(out reason))
                {
                    reason = $"第 {index + 1} 段配置无效：{reason}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
