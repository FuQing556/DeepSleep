using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// 将成功伤害事件转换为命中特效，并管理可扩容对象池。
    /// </summary>
    public sealed class HarnessLaserHitEffectPresenter2D : MonoBehaviour
    {
        [SerializeField]
        private HarnessTerminalLaserDamageExecutor2D _damageExecutor;
        [SerializeField]
        private HarnessTerminalLaserPresentationConfig _config;
        [SerializeField]
        private HarnessLaserHitEffect2D _effectPrefab;
        [SerializeField] private Transform _poolRoot;

        private readonly Stack<HarnessLaserHitEffect2D> _available = new();
        private readonly List<HarnessLaserHitEffect2D> _all = new();
        private System.Random _visualRandom;
        private bool _isInitialized;

        private void Awake()
        {
            _visualRandom = new System.Random();

            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(HarnessLaserHitEffectPresenter2D)}] " +
                    $"命中特效装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            for (int index = 0;
                 index < _config.HitEffectPoolPrewarmCount;
                 index++)
            {
                _available.Push(CreateEffect());
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                _damageExecutor.HitConfirmed += OnHitConfirmed;
            }
        }

        private void OnDisable()
        {
            if (_isInitialized && _damageExecutor != null)
            {
                _damageExecutor.HitConfirmed -= OnHitConfirmed;
            }

            for (int index = 0; index < _all.Count; index++)
            {
                _all[index].PrepareForPool();
            }

            _available.Clear();

            for (int index = 0; index < _all.Count; index++)
            {
                _available.Push(_all[index]);
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_damageExecutor == null)
            {
                reason = "未配置伤害执行器。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置表现参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                reason = $"表现配置无效：{reason}";
                return false;
            }

            if (_effectPrefab == null)
            {
                reason = "未配置命中特效预制体。";
                return false;
            }

            if (!_effectPrefab.TryValidateConfiguration(out reason))
            {
                reason = $"命中特效预制体无效：{reason}";
                return false;
            }

            if (_poolRoot == null)
            {
                reason = "未配置对象池根节点。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void OnHitConfirmed(HarnessTerminalLaserHitConfirmed hit)
        {
            if (!hit.IsValid)
            {
                return;
            }

            HarnessLaserHitEffect2D effect = _available.Count > 0
                ? _available.Pop()
                : CreateEffect();
            float initialRotationOffset = NextVisualFloat(0f, 360f);
            float rotationMagnitude = NextVisualFloat(
                _config.HitEffectMinimumRotationDegrees,
                _config.HitEffectMaximumRotationDegrees);
            float rotationDirection =
                _visualRandom.Next(0, 2) == 0 ? -1f : 1f;
            effect.Play(
                in hit,
                _config,
                initialRotationOffset,
                rotationMagnitude * rotationDirection);
        }

        private HarnessLaserHitEffect2D CreateEffect()
        {
            HarnessLaserHitEffect2D effect = Instantiate(
                _effectPrefab,
                _poolRoot);
            effect.name = _effectPrefab.name;
            effect.Finished += ReturnToPool;
            effect.PrepareForPool();
            _all.Add(effect);
            return effect;
        }

        private void ReturnToPool(HarnessLaserHitEffect2D effect)
        {
            effect.PrepareForPool();
            _available.Push(effect);
        }

        private float NextVisualFloat(float minimum, float maximum)
        {
            return Mathf.Lerp(
                minimum,
                maximum,
                (float)_visualRandom.NextDouble());
        }
    }
}
