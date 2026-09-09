using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Effects
{
    /// <summary>
    /// 管理一种一次性精灵特效的对象池。
    /// </summary>
    public sealed class OneShotSpriteEffectPool2D : MonoBehaviour
    {
        [SerializeField] private OneShotSpriteEffect2D _effectPrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private OneShotSpriteEffectConfig _config;

        private readonly Stack<OneShotSpriteEffect2D> _available = new();
        private readonly List<OneShotSpriteEffect2D> _all = new();
        private System.Random _visualRandom;
        private bool _isInitialized;
        private bool _hasReportedExhaustion;

        public int TotalCount => _all.Count;
        public int ActiveCount => _all.Count - _available.Count;
        /// <summary>表现事件观测口；网络只发送播放事件，不逐粒同步特效。</summary>
        public event System.Action<Vector2, float> Played;

        private void Awake()
        {
            _visualRandom = new System.Random();

            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(OneShotSpriteEffectPool2D)}] " +
                    $"特效池装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            for (int index = 0; index < _config.PrewarmCount; index++)
            {
                _available.Push(CreateEffect());
            }

            _isInitialized = true;
        }

        private void OnDisable()
        {
            _available.Clear();

            for (int index = 0; index < _all.Count; index++)
            {
                _all[index].PrepareForPool();
                _available.Push(_all[index]);
            }
        }

        public bool TryPlay(Vector2 position, float baseRotationDegrees)
        {
            if (!_isInitialized)
            {
                return false;
            }

            OneShotSpriteEffect2D effect;

            if (_available.Count > 0)
            {
                effect = _available.Pop();
            }
            else if (_all.Count < _config.MaximumCount)
            {
                effect = CreateEffect();
            }
            else
            {
                ReportExhaustionOnce();
                return false;
            }

            float rotationMagnitude = NextFloat(
                _config.MinimumRotationDegrees,
                _config.MaximumRotationDegrees);
            float rotationDirection =
                _visualRandom.Next(0, 2) == 0 ? -1f : 1f;
            effect.Play(
                position,
                baseRotationDegrees,
                rotationMagnitude * rotationDirection,
                _config);
            Played?.Invoke(position, baseRotationDegrees);
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_effectPrefab == null)
            {
                reason = "未配置特效预制体。";
                return false;
            }

            if (!_effectPrefab.TryValidateConfiguration(out reason))
            {
                reason = $"特效预制体无效：{reason}";
                return false;
            }

            if (_poolRoot == null)
            {
                reason = "未配置对象池根节点。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置特效参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }

        private OneShotSpriteEffect2D CreateEffect()
        {
            OneShotSpriteEffect2D effect = Instantiate(
                _effectPrefab,
                _poolRoot);
            effect.name = _effectPrefab.name;
            effect.Finished += ReturnToPool;
            effect.PrepareForPool();
            _all.Add(effect);
            return effect;
        }

        private void ReturnToPool(OneShotSpriteEffect2D effect)
        {
            effect.PrepareForPool();
            _available.Push(effect);
            _hasReportedExhaustion = false;
        }

        private float NextFloat(float minimum, float maximum)
        {
            return Mathf.Lerp(
                minimum,
                maximum,
                (float)_visualRandom.NextDouble());
        }

        private void ReportExhaustionOnce()
        {
            if (_hasReportedExhaustion)
            {
                return;
            }

            Debug.LogWarning(
                $"[{nameof(OneShotSpriteEffectPool2D)}] " +
                $"特效池达到上限 {_config.MaximumCount}，跳过本次播放。",
                this);
            _hasReportedExhaustion = true;
        }
    }
}
