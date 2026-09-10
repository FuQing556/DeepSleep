using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Targeting;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek
{
    /// <summary>
    /// DeepSeek 的基础自动饭团火力。
    /// 只有存在有效目标时才发射；没有目标时保留立即开火能力。
    /// </summary>
    public sealed class DeepSeekRiceAutoShooter :
        MonoBehaviour,
        IPlayerActionCommandConsumer
    {
        [SerializeField] private Transform _muzzle;
        [SerializeField] private RiceProjectilePool _projectilePool;
        [SerializeField] private DeepSeekRiceWeaponConfig _config;
        [SerializeField] private PlayerFacingController2D _facingController;
        [SerializeField]
        private DeepSeekManualTargetController _manualTargetController;
        [SerializeField] private PlayerUpgradeRuntimeState _upgradeState;

        private NearestVisibleTargetFinder2D _targetFinder;
        private float _remainingShotCooldown;
        private bool _isInitialized;

        public PlayerActionBlock ActionCategory =>
            PlayerActionBlock.AutomaticCombat;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DeepSeekRiceAutoShooter)}] " +
                    $"饭团武器装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _targetFinder = new NearestVisibleTargetFinder2D(_config);
            _isInitialized = true;
        }

        private void OnDisable()
        {
            _remainingShotCooldown = 0f;
        }

        public void ConsumeCommand(
            in PlayerCommand command,
            float deltaTime)
        {
            if (!_isInitialized ||
                !isActiveAndEnabled ||
                deltaTime <= 0f)
            {
                return;
            }

            _remainingShotCooldown = Mathf.Max(
                0f,
                _remainingShotCooldown - deltaTime);

            if (_remainingShotCooldown > 0f)
            {
                return;
            }

            Vector2 origin = _muzzle.position;
            Vector2 direction = _facingController.Forward;

            bool foundTarget =
                _manualTargetController.TryGetLockedTargetPosition(
                    out Vector2 targetPosition) ||
                _targetFinder.TryFind(
                    origin,
                    direction,
                    out targetPosition);

            if (!foundTarget)
            {
                return;
            }

            Vector2 targetDirection = targetPosition - origin;

            if (targetDirection.sqrMagnitude > 0f)
            {
                direction = targetDirection.normalized;
            }

            _projectilePool.TryRent(
                origin,
                direction,
                gameObject,
                GetUpgradeMultiplier(UpgradeEffectKind.WeaponDamage),
                out _);
            _remainingShotCooldown = _config.ShotIntervalSeconds /
                GetUpgradeMultiplier(UpgradeEffectKind.AttackRate);
        }

        private float GetUpgradeMultiplier(UpgradeEffectKind effect)
        {
            return _upgradeState != null
                ? _upgradeState.GetMultiplier(PlayerRole.DeepSeek, effect)
                : 1f;
        }

        private bool TryValidateConfiguration(out string reason)
        {
            if (_muzzle == null)
            {
                reason = "未配置发射点。";
                return false;
            }

            if (_projectilePool == null)
            {
                reason = "未配置饭团对象池。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置饭团武器参数。";
                return false;
            }

            if (_facingController == null)
            {
                reason = "未配置角色朝向控制器。";
                return false;
            }

            if (!_facingController.TryValidateConfiguration(out reason))
            {
                return false;
            }

            if (_manualTargetController == null)
            {
                reason = "未配置手动目标控制器。";
                return false;
            }

            if (!_manualTargetController.TryValidateConfiguration(out reason))
            {
                return false;
            }

            return _config.TryValidate(out reason);
        }
    }
}
