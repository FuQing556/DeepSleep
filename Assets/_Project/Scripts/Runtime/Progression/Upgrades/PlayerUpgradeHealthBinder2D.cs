using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Upgrades
{
    /// <summary>
    /// 把指定角色的最大生命强化投影到生命组件。强化账本仍是唯一数据源，
    /// HealthComponent只保存本局实际生命与最终上限。
    /// </summary>
    public sealed class PlayerUpgradeHealthBinder2D : MonoBehaviour
    {
        [SerializeField] private PlayerRole _role;
        [SerializeField] private PlayerUpgradeRuntimeState _upgradeState;
        [SerializeField] private HealthComponent _health;

        private bool _isInitialized;

        private void Awake()
        {
            if (_upgradeState == null || _health == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerUpgradeHealthBinder2D)}] " +
                    "强化状态和生命组件必须显式配置。",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
            ApplyCurrentValue();
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _upgradeState.Changed += ApplyCurrentValue;
            ApplyCurrentValue();
        }

        private void OnDisable()
        {
            if (_upgradeState != null)
            {
                _upgradeState.Changed -= ApplyCurrentValue;
            }
        }

        private void ApplyCurrentValue()
        {
            _health.SetMaximumHealthBonus(
                _upgradeState.GetAdditiveValue(
                    _role,
                    UpgradeEffectKind.MaximumHealth));
        }
    }
}
