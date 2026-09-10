using DeepSleep.Runtime.Combat.Beams;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// Harness 低频终端激光的基础静态参数。
    /// 局内升级生成运行时数值后再创建开火快照，不修改该资产。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_HA_TerminalLaser_",
        menuName = "DeepSleep/配置/战斗/Harness 终端激光")]
    public sealed class HarnessTerminalLaserConfig : ScriptableObject
    {
        [Header("节奏")]
        [SerializeField, Min(0f)] private float _calibrationSeconds;
        [SerializeField, Min(0.01f)] private float _fireCooldownSeconds;

        [Header("目标选择")]
        [SerializeField, Min(0.01f)] private float _pointSelectionRadius;
        [SerializeField, Min(0.01f)] private float _maximumLockDistance;
        [SerializeField, Range(0f, 1f)]
        private float _minimumDirectionDot = 0.85f;
        [SerializeField, Min(1)] private int _maximumTargetCandidates;
        [SerializeField] private LayerMask _targetLayers;

        [Header("束线与伤害")]
        [SerializeField, Min(0.01f)] private float _baseBeamWidth;
        [SerializeField, Min(1f)] private float _beamLengthMultiplier = 1.5f;
        [SerializeField, Min(0.01f)] private float _primaryTargetDamage;
        [SerializeField, Range(0.01f, 1f)]
        private float _piercingDamageMultiplier;
        [SerializeField] private BeamLaneDefinition[] _baseLanes;

        [Header("预算回收")]
        [SerializeField, Min(0f)] private float _budgetRefundPerHit;
        [SerializeField, Min(0f)] private float _budgetRefundOnPrimaryKill;
        [SerializeField, Min(0f)] private float _maximumBudgetRefundPerShot;

        public float CalibrationSeconds => _calibrationSeconds;
        public float FireCooldownSeconds => _fireCooldownSeconds;
        public float PointSelectionRadius => _pointSelectionRadius;
        public float MaximumLockDistance => _maximumLockDistance;
        public float MinimumDirectionDot => _minimumDirectionDot;
        public int MaximumTargetCandidates => _maximumTargetCandidates;
        public LayerMask TargetLayers => _targetLayers;
        public float BaseBeamWidth => _baseBeamWidth;
        public float BeamLengthMultiplier => _beamLengthMultiplier;
        public float PrimaryTargetDamage => _primaryTargetDamage;
        public float PiercingDamageMultiplier =>
            _piercingDamageMultiplier;
        public int BaseLaneCount => _baseLanes?.Length ?? 0;
        public float BudgetRefundPerHit => _budgetRefundPerHit;
        public float BudgetRefundOnPrimaryKill =>
            _budgetRefundOnPrimaryKill;
        public float MaximumBudgetRefundPerShot =>
            _maximumBudgetRefundPerShot;

        public BeamLaneDefinition GetBaseLane(int index)
        {
            return _baseLanes[index];
        }

        public bool TryValidate(out string reason)
        {
            if (_calibrationSeconds < 0f ||
                _fireCooldownSeconds <= 0f)
            {
                reason = "校准时间不能小于 0，开火冷却必须大于 0。";
                return false;
            }

            if (_pointSelectionRadius <= 0f ||
                _maximumLockDistance <= 0f ||
                _minimumDirectionDot < 0f ||
                _minimumDirectionDot > 1f ||
                _maximumTargetCandidates <= 0)
            {
                reason =
                    "点选半径、锁定距离和候选数量必须大于 0，" +
                    "方向阈值必须位于 0 到 1 之间。";
                return false;
            }

            if (_targetLayers.value == 0)
            {
                reason = "目标图层不能为空。";
                return false;
            }

            if (_baseBeamWidth <= 0f ||
                _beamLengthMultiplier < 1f ||
                _primaryTargetDamage <= 0f ||
                _piercingDamageMultiplier <= 0f ||
                _piercingDamageMultiplier > 1f)
            {
                reason = "束宽和伤害必须大于 0，长度倍率不得小于 1，贯穿伤害倍率必须位于 0 到 1 之间。";
                return false;
            }

            if (_baseLanes == null || _baseLanes.Length == 0)
            {
                reason = "至少需要配置一条基础束线。";
                return false;
            }

            for (int index = 0; index < _baseLanes.Length; index++)
            {
                if (!_baseLanes[index].TryValidate(out string laneReason))
                {
                    reason = $"基础束线 {index} 无效：{laneReason}";
                    return false;
                }
            }

            if (_budgetRefundPerHit < 0f ||
                _budgetRefundOnPrimaryKill < 0f ||
                _maximumBudgetRefundPerShot < 0f)
            {
                reason = "预算回收数值不能小于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
