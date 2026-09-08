using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.Presentation
{
    /// <summary>
    /// 将敌人离场原因路由到对应表现；正常飞出屏幕不播放特效。
    /// </summary>
    public sealed class EnemyDespawnEffectPresenter2D : MonoBehaviour
    {
        [SerializeField] private EnemyActorPool2D _enemyPool;
        [SerializeField] private OneShotSpriteEffectPool2D _deathEffectPool;
        [SerializeField]
        private OneShotSpriteEffectPool2D _contactImpactEffectPool;

        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyDespawnEffectPresenter2D)}] " +
                    $"敌人离场表现装配无效：{reason}",
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
                _enemyPool.ActorDespawned += OnActorDespawned;
            }
        }

        private void OnDisable()
        {
            if (_enemyPool != null)
            {
                _enemyPool.ActorDespawned -= OnActorDespawned;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_enemyPool == null)
            {
                reason = "未配置敌人对象池。";
                return false;
            }

            if (_deathEffectPool == null && _contactImpactEffectPool == null)
            {
                reason = "击杀特效池与撞击特效池不能同时为空。";
                return false;
            }

            if (_deathEffectPool != null &&
                !_deathEffectPool.TryValidateConfiguration(out reason))
            {
                reason = $"击杀特效池无效：{reason}";
                return false;
            }

            if (_contactImpactEffectPool != null &&
                !_contactImpactEffectPool.TryValidateConfiguration(out reason))
            {
                reason = $"撞击特效池无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void OnActorDespawned(EnemyDespawnRequest2D request)
        {
            switch (request.Reason)
            {
                case EnemyDespawnReason.Defeated:
                    if (_deathEffectPool != null)
                    {
                        _deathEffectPool.TryPlay(
                            request.EffectPosition,
                            request.WorldRotationDegrees);
                    }
                    break;

                case EnemyDespawnReason.ContactImpact:
                    float directionAngle = Mathf.Atan2(
                        request.TravelDirection.y,
                        request.TravelDirection.x) * Mathf.Rad2Deg;
                    if (_contactImpactEffectPool != null)
                    {
                        _contactImpactEffectPool.TryPlay(
                            request.EffectPosition,
                            directionAngle);
                    }
                    break;
            }
        }
    }
}
