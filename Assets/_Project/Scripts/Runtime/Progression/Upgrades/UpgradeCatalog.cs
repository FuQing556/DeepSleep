using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Upgrades
{
    [CreateAssetMenu(
        fileName = "CFG_UpgradeCatalog_",
        menuName = "DeepSleep/配置/成长/强化卡目录")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        [SerializeField] private UpgradeDefinition[] _definitions;

        public IReadOnlyList<UpgradeDefinition> Definitions => _definitions;

        public bool TryGet(
            UpgradeCardId id,
            out UpgradeDefinition definition)
        {
            if (_definitions != null)
            {
                for (int index = 0; index < _definitions.Length; index++)
                {
                    if (_definitions[index] != null &&
                        _definitions[index].Id == id)
                    {
                        definition = _definitions[index];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public void GetEligible(
            PlayerRole role,
            List<UpgradeDefinition> results)
        {
            results.Clear();
            if (_definitions == null)
            {
                return;
            }

            for (int index = 0; index < _definitions.Length; index++)
            {
                UpgradeDefinition definition = _definitions[index];
                if (definition != null && definition.Supports(role))
                {
                    results.Add(definition);
                }
            }
        }

        public void SetDefinitions(UpgradeDefinition[] definitions)
        {
            _definitions = definitions;
        }

        public bool TryValidate(out string reason)
        {
            if (_definitions == null || _definitions.Length < 3)
            {
                reason = "强化目录至少需要三张卡。";
                return false;
            }

            var ids = new HashSet<UpgradeCardId>();
            for (int index = 0; index < _definitions.Length; index++)
            {
                UpgradeDefinition definition = _definitions[index];
                if (definition == null)
                {
                    reason = $"第 {index} 张强化卡为空。";
                    return false;
                }

                if (!definition.TryValidate(out reason))
                {
                    return false;
                }

                if (!ids.Add(definition.Id))
                {
                    reason = $"强化编号 {definition.Id} 重复。";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }
}
