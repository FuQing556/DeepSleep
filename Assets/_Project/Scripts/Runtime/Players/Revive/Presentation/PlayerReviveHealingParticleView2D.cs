using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive.Presentation
{
    /// <summary>
    /// 将复活引导状态映射到纯视觉粒子，不参与进度与复活判定。
    /// </summary>
    public sealed class PlayerReviveHealingParticleView2D : MonoBehaviour
    {
        [SerializeField] private PlayerReviveCoordinator2D _coordinator;
        [SerializeField] private ParticleSystem[] _particleChannels;
        [SerializeField] private ParticleSystem _immediateParticleChannel;
        [SerializeField, Min(1)] private int _immediateParticleCount = 1;
        [SerializeField]
        private CombatEffectScaleSettings _effectScaleSettings;

        private ParticleSystem.MinMaxCurve[] _baseStartSizes;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerReviveHealingParticleView2D)}] " +
                    $"复活治疗粒子装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            CacheBaseParticleSizes();
            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _coordinator.ProgressChanged += OnProgressChanged;
            _effectScaleSettings.ScaleChanged += OnEffectScaleChanged;
            ApplyPlayerEffectScale();
            SynchronizePlayback();
        }

        private void OnDisable()
        {
            if (_coordinator != null)
            {
                _coordinator.ProgressChanged -= OnProgressChanged;
            }

            if (_effectScaleSettings != null)
            {
                _effectScaleSettings.ScaleChanged -= OnEffectScaleChanged;
            }

            StopAndClearAllChannels();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_coordinator == null)
            {
                reason = "未配置复活协调器。";
                return false;
            }

            if (_coordinator.gameObject != gameObject)
            {
                reason = "粒子视图与复活协调器必须位于同一玩家根节点。";
                return false;
            }

            int channelCount = _particleChannels?.Length ?? 0;

            if (channelCount == 0)
            {
                reason = "至少需要配置一个治疗粒子通道。";
                return false;
            }

            if (_effectScaleSettings == null)
            {
                reason = "未配置玩家战斗特效尺寸设置。";
                return false;
            }

            if (!_effectScaleSettings.TryValidate(out reason))
            {
                return false;
            }

            if (_immediateParticleChannel == null ||
                _immediateParticleCount <= 0 ||
                System.Array.IndexOf(
                    _particleChannels,
                    _immediateParticleChannel) < 0)
            {
                reason = "首帧粒子通道必须来自已配置通道，且数量必须大于 0。";
                return false;
            }

            for (int index = 0; index < channelCount; index++)
            {
                ParticleSystem channel = _particleChannels[index];

                if (channel == null ||
                    !channel.transform.IsChildOf(transform))
                {
                    reason = $"第 {index} 个粒子通道为空或不属于该玩家。";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private void OnProgressChanged(float progress01)
        {
            SynchronizePlayback();
        }

        private void OnEffectScaleChanged(
            CombatEffectSource source,
            float scale)
        {
            if (source == CombatEffectSource.Player)
            {
                ApplyPlayerEffectScale();
            }
        }

        private void CacheBaseParticleSizes()
        {
            _baseStartSizes =
                new ParticleSystem.MinMaxCurve[_particleChannels.Length];

            for (int index = 0;
                 index < _particleChannels.Length;
                 index++)
            {
                ParticleSystem.MainModule main =
                    _particleChannels[index].main;
                _baseStartSizes[index] = main.startSize;
            }
        }

        private void ApplyPlayerEffectScale()
        {
            float scale = _effectScaleSettings.PlayerEffectScale;

            for (int index = 0;
                 index < _particleChannels.Length;
                 index++)
            {
                ParticleSystem.MainModule main =
                    _particleChannels[index].main;
                ParticleSystem.MinMaxCurve scaled =
                    _baseStartSizes[index];

                if (scaled.mode == ParticleSystemCurveMode.Constant)
                {
                    scaled.constant =
                        _baseStartSizes[index].constant * scale;
                }
                else if (scaled.mode ==
                         ParticleSystemCurveMode.TwoConstants)
                {
                    scaled.constantMin =
                        _baseStartSizes[index].constantMin * scale;
                    scaled.constantMax =
                        _baseStartSizes[index].constantMax * scale;
                }
                else
                {
                    scaled.curveMultiplier =
                        _baseStartSizes[index].curveMultiplier * scale;
                }

                main.startSize = scaled;
            }
        }

        private void SynchronizePlayback()
        {
            bool shouldPlay = _coordinator.IsReviving;
            bool startedPlayback = false;

            for (int index = 0; index < _particleChannels.Length; index++)
            {
                ParticleSystem channel = _particleChannels[index];

                if (shouldPlay)
                {
                    if (!channel.isPlaying)
                    {
                        channel.Play(true);
                        startedPlayback = true;
                    }
                }
                else
                {
                    channel.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (shouldPlay && startedPlayback)
            {
                _immediateParticleChannel.Emit(_immediateParticleCount);
            }
        }

        private void StopAndClearAllChannels()
        {
            int channelCount = _particleChannels?.Length ?? 0;

            for (int index = 0; index < channelCount; index++)
            {
                if (_particleChannels[index] != null)
                {
                    _particleChannels[index].Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
