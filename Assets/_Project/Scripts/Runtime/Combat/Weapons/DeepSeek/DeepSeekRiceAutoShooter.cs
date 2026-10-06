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
        private readonly System.Collections.Generic.List<Collider2D> _fanTargets = new(32);
        private readonly System.Collections.Generic.HashSet<DeepSleep.Runtime.Combat.Damage.IDamageReceiver> _assignedTargets = new();

        /// <summary>一轮至少成功租出一颗饭团后发布一次；扇阵不是多次齐射。</summary>
        public event System.Action<Vector2> VolleyFired;

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

            int count = 1 + Mathf.FloorToInt(GetBonus(UpgradeEffectKind.ProjectileCount));
            bool correct = GetBonus(UpgradeEffectKind.TargetCorrection) > 0f;
            _assignedTargets.Clear();
            if (correct)
                Physics2D.OverlapCircle(origin, _config.TargetSearchRadius,
                    new ContactFilter2D { useLayerMask = true, layerMask = _config.TargetLayers, useTriggers = true }, _fanTargets);
            float damageMultiplier = (_config.DamagePerProjectile + GetBonus(UpgradeEffectKind.WeaponDamage)) / _config.DamagePerProjectile;
            bool fired = false;
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : _config.FanDegrees * ((i + .5f) / count - .5f);
                Vector2 lane = Quaternion.Euler(0f, 0f, angle) * direction;
                if (correct) lane = CorrectLane(origin, lane);
                fired |= _projectilePool.TryRent(origin, lane, gameObject, damageMultiplier, out _,
                    GetBonus(UpgradeEffectKind.RiceSplash) > 0f);
            }
            if (fired) VolleyFired?.Invoke(origin);
            _remainingShotCooldown = _config.ShotIntervalSeconds /
                GetUpgradeMultiplier(UpgradeEffectKind.AttackRate);
        }

        private float GetUpgradeMultiplier(UpgradeEffectKind effect)
        {
            return _upgradeState != null
                ? _upgradeState.GetMultiplier(PlayerRole.DeepSeek, effect)
                : 1f;
        }

        private float GetBonus(UpgradeEffectKind effect) => _upgradeState != null
            ? _upgradeState.GetAdditiveValue(PlayerRole.DeepSeek, effect) : 0f;

        private Vector2 CorrectLane(Vector2 origin, Vector2 lane)
        {
            float best = float.PositiveInfinity;
            Vector2 result = lane;
            DeepSleep.Runtime.Combat.Damage.IDamageReceiver chosen = null;
            foreach (var candidate in _fanTargets)
            {
                if (candidate == null || !candidate.TryGetComponent(out DeepSleep.Runtime.Combat.Damage.DamageHitbox2D hit) ||
                    !hit.CanReceiveDamage || !hit.TryGetReceiver(out var receiver) || _assignedTargets.Contains(receiver)) continue;
                Vector2 offset = (Vector2)candidate.bounds.center - origin;
                if (Vector2.Angle(lane, offset) > _config.CorrectionDegrees || offset.sqrMagnitude >= best) continue;
                if (Physics2D.Linecast(origin, candidate.bounds.center, _config.ObstacleLayers).collider != null) continue;
                best = offset.sqrMagnitude; result = offset.normalized; chosen = receiver;
            }
            if (chosen != null) _assignedTargets.Add(chosen);
            return result;
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
