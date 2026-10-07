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
        [SerializeField] private OneShotSpriteEffectPool2D _splashEffectPool;

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
                _projectilePool.SplashConfirmed += OnSplashConfirmed;
            }
        }

        private void OnDisable()
        {
            if (_isInitialized && _projectilePool != null)
            {
                _projectilePool.HitConfirmed -= OnHitConfirmed;
                _projectilePool.SplashConfirmed -= OnSplashConfirmed;
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

            if (!_effectPool.TryValidateConfiguration(out reason)) return false;
            if (_splashEffectPool == null)
            {
                reason = "未配置饭团溅射特效池。";
                return false;
            }
            return _splashEffectPool.TryValidateConfiguration(out reason);
        }

        private void OnSplashConfirmed(Vector2 point, Vector2 direction)
        {
            _splashEffectPool?.TryPlay(point, WorldSpriteGeometry2D.DirectionToAngle(direction));
        }

        private void OnHitConfirmed(RiceProjectileHitConfirmed hit)
        {
            if (!hit.IsValid || hit.Hitbox.UseReceiverHitFeedback)
            {
                return;
            }

            _effectPool.TryPlay(
                hit.HitPoint,
                WorldSpriteGeometry2D.DirectionToAngle(hit.Direction));
        }
    }
}
