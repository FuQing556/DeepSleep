using System;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Upgrades
{
    [Flags]
    public enum PlayerRoleMask : byte
    {
        None = 0,
        DeepSeek = 1,
        Harness = 2,
        Both = DeepSeek | Harness
    }

    public enum UpgradeOwnershipScope : byte
    {
        Role,
        Team
    }

    public enum UpgradeEffectKind : byte
    {
        WeaponDamage,
        AttackRate,
        BeamWidth,
        ProjectileCount,
        MaximumHealth
    }

    public enum UpgradeCardId : byte
    {
        DataCompression = 1,
        RuntimeOverclock = 2,
        GiantRiceBall = 10,
        RiceStorm = 11,
        TerminalAmplifier = 20,
        CoolingCircuit = 21
    }

    [Serializable]
    public sealed class UpgradeDefinition
    {
        [SerializeField] private UpgradeCardId _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private PlayerRoleMask _eligibleRoles;
        [SerializeField] private UpgradeOwnershipScope _ownershipScope;
        [SerializeField, Min(1)] private int _maximumRank = 5;
        [SerializeField, Min(0)] private int _baseTokenCost = 12;
        [SerializeField, Min(0)] private int _additionalCostPerRank = 3;
        [SerializeField] private UpgradeEffectKind _effect;
        [SerializeField] private float _effectPerRank;

        public UpgradeCardId Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public UpgradeOwnershipScope OwnershipScope => _ownershipScope;
        public int MaximumRank => _maximumRank;
        public int BaseTokenCost => _baseTokenCost;
        public int AdditionalCostPerRank => _additionalCostPerRank;
        public UpgradeEffectKind Effect => _effect;
        public float EffectPerRank => _effectPerRank;

        public UpgradeDefinition(
            UpgradeCardId id,
            string displayName,
            string description,
            PlayerRoleMask eligibleRoles,
            UpgradeOwnershipScope ownershipScope,
            int maximumRank,
            int baseTokenCost,
            int additionalCostPerRank,
            UpgradeEffectKind effect,
            float effectPerRank)
        {
            _id = id;
            _displayName = displayName;
            _description = description;
            _eligibleRoles = eligibleRoles;
            _ownershipScope = ownershipScope;
            _maximumRank = maximumRank;
            _baseTokenCost = baseTokenCost;
            _additionalCostPerRank = additionalCostPerRank;
            _effect = effect;
            _effectPerRank = effectPerRank;
        }

        public bool Supports(PlayerRole role)
        {
            PlayerRoleMask roleMask = role == PlayerRole.DeepSeek
                ? PlayerRoleMask.DeepSeek
                : PlayerRoleMask.Harness;
            return (_eligibleRoles & roleMask) != 0;
        }

        public bool TryValidate(out string reason)
        {
            if (_eligibleRoles == PlayerRoleMask.None ||
                string.IsNullOrWhiteSpace(_displayName) ||
                string.IsNullOrWhiteSpace(_description) ||
                _maximumRank <= 0 ||
                _baseTokenCost < 0 || _additionalCostPerRank < 0 ||
                _effectPerRank <= 0f)
            {
                reason = $"强化 {_id} 的名称、描述、角色、等级或效果无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public int GetTokenCost(int currentRank)
        {
            return _baseTokenCost +
                Mathf.Max(0, currentRank) * _additionalCostPerRank;
        }
    }
}
