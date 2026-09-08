using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DeepSleep.Runtime.Presentation.Effects
{
    /// <summary>
    /// 战斗表现的可访问性倍率。玩家产生与敌人产生的表现分别保存。
    /// 不改变碰撞体、伤害范围或复活判定范围。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_CombatEffectScale_",
        menuName = "DeepSleep/配置/表现/战斗特效尺寸")]
    public sealed class CombatEffectScaleSettings : ScriptableObject
    {
        [FormerlySerializedAs("_defaultEnemyHitScale")]
        [SerializeField, Range(0.5f, 5f)]
        private float _defaultPlayerEffectScale = 1f;
        [FormerlySerializedAs("_defaultPlayerHitScale")]
        [SerializeField, Range(0.5f, 5f)]
        private float _defaultEnemyEffectScale = 1f;

        [NonSerialized] private float _playerEffectScale;
        [NonSerialized] private float _enemyEffectScale;
        [NonSerialized] private bool _isInitialized;

        public event Action<CombatEffectSource, float> ScaleChanged;

        public float PlayerEffectScale
        {
            get
            {
                EnsureInitialized();
                return _playerEffectScale;
            }
        }

        public float EnemyEffectScale
        {
            get
            {
                EnsureInitialized();
                return _enemyEffectScale;
            }
        }

        private void OnEnable()
        {
            ResetRuntimeValues();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ResetRuntimeValues();
            }
        }

        public float GetScale(CombatEffectSource source)
        {
            return source switch
            {
                CombatEffectSource.Player => PlayerEffectScale,
                CombatEffectSource.Enemy => EnemyEffectScale,
                _ => 1f
            };
        }

        public void SetPlayerEffectScalePercent(float percent)
        {
            SetScale(CombatEffectSource.Player, percent * 0.01f);
        }

        public void SetEnemyEffectScalePercent(float percent)
        {
            SetScale(CombatEffectSource.Enemy, percent * 0.01f);
        }

        public void SetScale(CombatEffectSource source, float scale)
        {
            EnsureInitialized();
            float clamped = Mathf.Clamp(scale, 0.5f, 5f);

            if (source == CombatEffectSource.Player)
            {
                if (Mathf.Approximately(_playerEffectScale, clamped))
                {
                    return;
                }

                _playerEffectScale = clamped;
            }
            else if (source == CombatEffectSource.Enemy)
            {
                if (Mathf.Approximately(_enemyEffectScale, clamped))
                {
                    return;
                }

                _enemyEffectScale = clamped;
            }
            else
            {
                return;
            }

            ScaleChanged?.Invoke(source, clamped);
        }

        public bool TryValidate(out string reason)
        {
            if (_defaultPlayerEffectScale < 0.5f ||
                _defaultPlayerEffectScale > 5f ||
                _defaultEnemyEffectScale < 0.5f ||
                _defaultEnemyEffectScale > 5f)
            {
                reason = "两类默认倍率都必须位于 50% 到 500% 之间。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                ResetRuntimeValues();
            }
        }

        private void ResetRuntimeValues()
        {
            _playerEffectScale = Mathf.Clamp(
                _defaultPlayerEffectScale,
                0.5f,
                5f);
            _enemyEffectScale = Mathf.Clamp(
                _defaultEnemyEffectScale,
                0.5f,
                5f);
            _isInitialized = true;
        }
    }
}
