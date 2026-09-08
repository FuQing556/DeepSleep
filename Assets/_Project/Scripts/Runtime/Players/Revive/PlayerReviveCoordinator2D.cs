using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive
{
    /// <summary>
    /// 把倒地玩家作为救援目标提供给范围内的有效队友。
    /// 不读取输入，也不累计最终复活进度。
    /// </summary>
    public sealed class PlayerReviveCoordinator2D : MonoBehaviour
    {
        [SerializeField] private PlayerLifeStateController2D _ownerLifeState;
        [SerializeField] private PlayerReviveZone2D _reviveZone;
        [SerializeField] private PlayerReviveChannelConfig _config;

        private readonly Dictionary<PlayerActor, PlayerReviveActionChannel>
            _offeredChannels =
                new Dictionary<PlayerActor, PlayerReviveActionChannel>();

        private bool _isInitialized;
        private PlayerReviveActionChannel _activeChannel;
        private float _elapsedReviveSeconds;

        public event System.Action<float> ProgressChanged;
        public event System.Action<PlayerActor> ReviveCompleted;

        public float Progress01 => _config == null
            ? 0f
            : Mathf.Clamp01(
                _elapsedReviveSeconds / _config.ReviveDurationSeconds);

        public bool IsReviving =>
            _activeChannel != null && _activeChannel.IsChanneling;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerReviveCoordinator2D)}] " +
                    $"复活协调器装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _reviveZone.EligibleRescuerEntered += OnRescuerEntered;
            _reviveZone.EligibleRescuerExited += OnRescuerExited;
        }

        private void OnDisable()
        {
            if (_reviveZone != null)
            {
                _reviveZone.EligibleRescuerEntered -= OnRescuerEntered;
                _reviveZone.EligibleRescuerExited -= OnRescuerExited;
            }

            WithdrawAllOffers();
        }

        private void OnRescuerEntered(PlayerActor rescuer)
        {
            if (rescuer == null ||
                !rescuer.TryGetComponent(
                    out PlayerReviveActionChannel channel) ||
                !channel.TryOfferTarget(_ownerLifeState))
            {
                return;
            }

            _offeredChannels[rescuer] = channel;
            channel.ChannelStarted += OnChannelStarted;
            channel.ChannelCancelled += OnChannelCancelled;
        }

        private void OnRescuerExited(PlayerActor rescuer)
        {
            if (rescuer == null ||
                !_offeredChannels.Remove(
                    rescuer,
                    out PlayerReviveActionChannel channel))
            {
                return;
            }

            channel.WithdrawTarget(_ownerLifeState);
            channel.ChannelStarted -= OnChannelStarted;
            channel.ChannelCancelled -= OnChannelCancelled;
        }

        private void WithdrawAllOffers()
        {
            foreach (PlayerReviveActionChannel channel in
                     _offeredChannels.Values)
            {
                channel.ChannelStarted -= OnChannelStarted;
                channel.ChannelCancelled -= OnChannelCancelled;
                channel.WithdrawTarget(_ownerLifeState);
            }

            _offeredChannels.Clear();
            ResetProgress();
        }

        public void Simulate(float deltaTime)
        {
            if (!_isInitialized || _activeChannel == null ||
                !_activeChannel.IsChanneling ||
                _activeChannel.OfferedTarget != _ownerLifeState ||
                _ownerLifeState.State != PlayerLifeState.Downed ||
                deltaTime <= 0f)
            {
                return;
            }

            _elapsedReviveSeconds = Mathf.Min(
                _config.ReviveDurationSeconds,
                _elapsedReviveSeconds + deltaTime);
            ProgressChanged?.Invoke(Progress01);

            if (_elapsedReviveSeconds + 0.00001f <
                _config.ReviveDurationSeconds)
            {
                return;
            }

            CompleteRevive();
        }

        private void OnChannelStarted()
        {
            foreach (PlayerReviveActionChannel channel in
                     _offeredChannels.Values)
            {
                if (!channel.IsChanneling ||
                    channel.OfferedTarget != _ownerLifeState)
                {
                    continue;
                }

                _activeChannel = channel;
                _elapsedReviveSeconds = 0f;
                ProgressChanged?.Invoke(0f);
                return;
            }
        }

        private void OnChannelCancelled()
        {
            if (_activeChannel != null && !_activeChannel.IsChanneling)
            {
                ResetProgress();
            }
        }

        private void CompleteRevive()
        {
            PlayerReviveActionChannel completedChannel = _activeChannel;
            PlayerActor rescuer = completedChannel.GetComponent<PlayerActor>();
            float revivedHealth = Mathf.Max(
                _config.MinimumRevivedHealth,
                Mathf.Floor(
                    _ownerLifeState.MaximumHealth *
                    _config.RevivedMaximumHealthFraction));

            if (!completedChannel.TryCompleteChannel(_ownerLifeState) ||
                !_ownerLifeState.TryRevive(
                    revivedHealth,
                    _config.PostReviveInvulnerabilitySeconds))
            {
                ResetProgress();
                return;
            }

            _elapsedReviveSeconds = _config.ReviveDurationSeconds;
            ProgressChanged?.Invoke(1f);
            ReviveCompleted?.Invoke(rescuer);
            _activeChannel = null;
        }

        private void ResetProgress()
        {
            bool hadProgress = _elapsedReviveSeconds > 0f ||
                _activeChannel != null;
            _activeChannel = null;
            _elapsedReviveSeconds = 0f;

            if (hadProgress)
            {
                ProgressChanged?.Invoke(0f);
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_ownerLifeState == null || _reviveZone == null ||
                _config == null)
            {
                reason = "目标生命状态、复活范围和复活参数必须全部配置。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                return false;
            }

            if (_ownerLifeState.gameObject != gameObject ||
                !_reviveZone.transform.IsChildOf(transform))
            {
                reason = "协调器必须位于目标根节点，复活范围必须属于该目标。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
