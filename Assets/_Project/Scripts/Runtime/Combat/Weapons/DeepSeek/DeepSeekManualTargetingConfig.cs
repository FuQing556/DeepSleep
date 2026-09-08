using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek
{
    /// <summary>
    /// DeepSeek 普通手动锁定的静态参数。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_DS_ManualTargeting_",
        menuName = "DeepSleep/Combat/DeepSeek Manual Targeting Config")]
    public sealed class DeepSeekManualTargetingConfig : ScriptableObject
    {
        [Header("目标选择")]
        [SerializeField, Min(0.01f)] private float _pointSelectionRadius;
        [SerializeField, Min(0.01f)] private float _maximumLockDistance;
        [SerializeField, Min(1)] private int _maximumTargetCandidates;
        [SerializeField, Range(0f, 1f)] private float _minimumDirectionDot;
        [SerializeField] private LayerMask _targetLayers;

        [Header("朝向")]
        [SerializeField, Min(0f)] private float _facingDeadZone;
        [SerializeField, Min(0f)] private float _returnDelaySeconds;

        public float PointSelectionRadius => _pointSelectionRadius;
        public float MaximumLockDistance => _maximumLockDistance;
        public int MaximumTargetCandidates => _maximumTargetCandidates;
        public float MinimumDirectionDot => _minimumDirectionDot;
        public LayerMask TargetLayers => _targetLayers;
        public float FacingDeadZone => _facingDeadZone;
        public float ReturnDelaySeconds => _returnDelaySeconds;

        public bool TryValidate(out string reason)
        {
            if (_pointSelectionRadius <= 0f ||
                _maximumLockDistance <= 0f ||
                _maximumTargetCandidates <= 0)
            {
                reason = "点选半径、最大锁定距离和候选数量必须大于 0。";
                return false;
            }

            if (_minimumDirectionDot < 0f || _minimumDirectionDot > 1f)
            {
                reason = "方向选择点积阈值必须位于 0 到 1 之间。";
                return false;
            }

            if (_targetLayers.value == 0)
            {
                reason = "目标图层不能为空。";
                return false;
            }

            if (_facingDeadZone < 0f || _returnDelaySeconds < 0f)
            {
                reason = "朝向死区和回正延迟不能小于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
