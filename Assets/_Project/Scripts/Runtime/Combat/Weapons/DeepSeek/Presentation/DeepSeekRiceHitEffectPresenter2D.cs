using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Presentation
{
    /// <summary>
    /// 将饭团成功伤害事实转换为一次性命中特效。
    /// </summary>
    public sealed class DeepSeekRiceHitEffectPresenter2D : MonoBehaviour
    {
        [SerializeField] private RiceProjectilePool _projectilePool;
        [SerializeField] private OneShotSpriteEffectPool2D _effectPool;

        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DeepSeekRiceHitEffectPresenter2D)}] " +
                    $"命中特效装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                _projectilePool.HitConfirmed += OnHitConfirmed;
            }
        }

        private void OnDisable()
        {
            if (_isInitialized && _projectilePool != null)
            {
                _projectilePool.HitConfirmed -= OnHitConfirmed;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_projectilePool == null)
            {
                reason = "未配置饭团弹体池。";
                return false;
            }

            if (_effectPool == null)
            {
                reason = "未配置一次性命中特效池。";
                return false;
            }

            return _effectPool.TryValidateConfiguration(out reason);
        }

        private void OnHitConfirmed(RiceProjectileHitConfirmed hit)
        {
            if (!hit.IsValid)
            {
                return;
            }

            _effectPool.TryPlay(
                hit.HitPoint,
                WorldSpriteGeometry2D.DirectionToAngle(hit.Direction));
        }
    }
}
