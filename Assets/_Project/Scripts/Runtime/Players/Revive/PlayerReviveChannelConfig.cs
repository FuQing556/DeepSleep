using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive
{
    [CreateAssetMenu(
        fileName = "CFG_PlayerReviveChannel",
        menuName = "DeepSleep/Players/Revive Channel Config")]
    public sealed class PlayerReviveChannelConfig : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)]
        private float _movementCancelDeadZone = 0.1f;

        [SerializeField, Min(0f)]
        private float _automaticStartDelaySeconds = 0.2f;

        [SerializeField, Min(0.01f)]
        private float _reviveDurationSeconds = 2f;

        [SerializeField, Range(0.01f, 1f)]
        private float _revivedMaximumHealthFraction = 1f / 3f;

        [SerializeField, Min(0.01f)]
        private float _minimumRevivedHealth = 1f;

        [SerializeField, Min(0.01f)]
        private float _postReviveInvulnerabilitySeconds = 1.5f;

        public float MovementCancelDeadZone => _movementCancelDeadZone;

        public float AutomaticStartDelaySeconds =>
            _automaticStartDelaySeconds;

        public float ReviveDurationSeconds => _reviveDurationSeconds;

        public float RevivedMaximumHealthFraction =>
            _revivedMaximumHealthFraction;

        public float MinimumRevivedHealth => _minimumRevivedHealth;

        public float PostReviveInvulnerabilitySeconds =>
            _postReviveInvulnerabilitySeconds;

        public bool TryValidate(out string reason)
        {
            if (_movementCancelDeadZone < 0f ||
                _movementCancelDeadZone > 1f)
            {
                reason = "移动中断死区必须在 0 到 1 之间。";
                return false;
            }

            if (_automaticStartDelaySeconds < 0f)
            {
                reason = "自动开始等待时间不能小于零。";
                return false;
            }

            if (_reviveDurationSeconds <= 0f ||
                _revivedMaximumHealthFraction <= 0f ||
                _revivedMaximumHealthFraction > 1f ||
                _minimumRevivedHealth <= 0f ||
                _postReviveInvulnerabilitySeconds <= 0f)
            {
                reason = "复活时间、生命比例、最低生命和无敌时间必须为有效正数。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
