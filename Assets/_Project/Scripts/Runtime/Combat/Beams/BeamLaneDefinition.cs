using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams
{
    /// <summary>
    /// 配置资产中的一条相对束线。局内强化可以组合不同定义，
    /// 开火时会转换成不再依赖配置资产的 BeamLaneSnapshot。
    /// </summary>
    [Serializable]
    public struct BeamLaneDefinition
    {
        [SerializeField] private float _angleOffsetDegrees;
        [SerializeField] private float _lateralOffset;
        [SerializeField, Min(0.01f)] private float _widthMultiplier;
        [SerializeField, Min(0.01f)] private float _damageMultiplier;

        public float AngleOffsetDegrees => _angleOffsetDegrees;
        public float LateralOffset => _lateralOffset;
        public float WidthMultiplier => _widthMultiplier;
        public float DamageMultiplier => _damageMultiplier;

        public bool TryValidate(out string reason)
        {
            if (!IsFinite(_angleOffsetDegrees) ||
                !IsFinite(_lateralOffset))
            {
                reason = "束线角度与横向偏移必须是有限数值。";
                return false;
            }

            if (!IsFinite(_widthMultiplier) ||
                _widthMultiplier <= 0f ||
                !IsFinite(_damageMultiplier) ||
                _damageMultiplier <= 0f)
            {
                reason = "束线宽度倍率与伤害倍率必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
