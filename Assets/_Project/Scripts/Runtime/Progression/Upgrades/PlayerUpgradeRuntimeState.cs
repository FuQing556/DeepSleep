using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Upgrades
{
    public sealed class PlayerUpgradeSnapshot
    {
        internal readonly Dictionary<UpgradeCardId, int> DeepSeek;
        internal readonly Dictionary<UpgradeCardId, int> Harness;
        internal readonly Dictionary<UpgradeCardId, int> Team;

        internal PlayerUpgradeSnapshot(
            Dictionary<UpgradeCardId, int> deepSeek,
            Dictionary<UpgradeCardId, int> harness,
            Dictionary<UpgradeCardId, int> team)
        {
            DeepSeek = deepSeek;
            Harness = harness;
            Team = team;
        }
    }

    public sealed class PlayerUpgradeRuntimeState : MonoBehaviour
    {
        [SerializeField] private UpgradeCatalog _catalog;

        private readonly Dictionary<UpgradeCardId, int> _deepSeekRanks = new();
        private readonly Dictionary<UpgradeCardId, int> _harnessRanks = new();
        private readonly Dictionary<UpgradeCardId, int> _teamRanks = new();

        public event Action Changed;
        public UpgradeCatalog Catalog => _catalog;

        private void Awake()
        {
            string reason = "未配置强化目录。";
            if (_catalog == null || !_catalog.TryValidate(out reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerUpgradeRuntimeState)}] 强化目录无效：{reason}",
                    this);
                enabled = false;
            }
        }

        public int GetRank(PlayerRole role, UpgradeCardId id)
        {
            if (!_catalog.TryGet(id, out UpgradeDefinition definition))
            {
                return 0;
            }

            Dictionary<UpgradeCardId, int> ranks =
                GetRanks(role, definition.OwnershipScope);
            return ranks.TryGetValue(id, out int rank) ? rank : 0;
        }

        public bool IsMaximumRank(PlayerRole role, UpgradeCardId id)
        {
            return _catalog.TryGet(id, out UpgradeDefinition definition) &&
                GetRank(role, id) >= definition.MaximumRank;
        }

        public bool TryApply(PlayerRole role, UpgradeCardId id)
        {
            if (!enabled ||
                !_catalog.TryGet(id, out UpgradeDefinition definition) ||
                !definition.Supports(role))
            {
                return false;
            }

            Dictionary<UpgradeCardId, int> ranks =
                GetRanks(role, definition.OwnershipScope);
            int rank = ranks.TryGetValue(id, out int current) ? current : 0;
            if (rank >= definition.MaximumRank)
            {
                return false;
            }

            ranks[id] = rank + 1;
            Changed?.Invoke();
            return true;
        }

        public float GetMultiplier(
            PlayerRole role,
            UpgradeEffectKind effect)
        {
            if (_catalog == null)
            {
                return 1f;
            }

            float additiveBonus = 0f;
            IReadOnlyList<UpgradeDefinition> definitions = _catalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                UpgradeDefinition definition = definitions[index];
                if (definition.Effect == effect && definition.Supports(role))
                {
                    additiveBonus += GetRank(role, definition.Id) *
                        definition.EffectPerRank;
                }
            }

            return Mathf.Max(0.01f, 1f + additiveBonus);
        }

        public void SetRankFromAuthority(
            PlayerRole role,
            UpgradeCardId id,
            int rank)
        {
            if (!_catalog.TryGet(id, out UpgradeDefinition definition))
            {
                return;
            }

            Dictionary<UpgradeCardId, int> ranks =
                GetRanks(role, definition.OwnershipScope);
            ranks[id] = Mathf.Clamp(rank, 0, definition.MaximumRank);
        }

        public void NotifySnapshotApplied()
        {
            Changed?.Invoke();
        }

        public PlayerUpgradeSnapshot CaptureSnapshot()
        {
            return new PlayerUpgradeSnapshot(
                new Dictionary<UpgradeCardId, int>(_deepSeekRanks),
                new Dictionary<UpgradeCardId, int>(_harnessRanks),
                new Dictionary<UpgradeCardId, int>(_teamRanks));
        }

        public void RestoreSnapshot(PlayerUpgradeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            CopyRanks(snapshot.DeepSeek, _deepSeekRanks);
            CopyRanks(snapshot.Harness, _harnessRanks);
            CopyRanks(snapshot.Team, _teamRanks);
            Changed?.Invoke();
        }

        private static void CopyRanks(
            Dictionary<UpgradeCardId, int> source,
            Dictionary<UpgradeCardId, int> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<UpgradeCardId, int> pair in source)
            {
                destination[pair.Key] = pair.Value;
            }
        }

        private Dictionary<UpgradeCardId, int> GetRanks(
            PlayerRole role,
            UpgradeOwnershipScope scope)
        {
            if (scope == UpgradeOwnershipScope.Team)
            {
                return _teamRanks;
            }

            return role == PlayerRole.DeepSeek
                ? _deepSeekRanks
                : _harnessRanks;
        }
    }
}
