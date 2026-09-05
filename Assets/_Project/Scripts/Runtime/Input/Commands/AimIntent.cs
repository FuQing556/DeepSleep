using UnityEngine;

namespace DeepSleep.Runtime.Input.Commands
{
    /// <summary>
    /// 一份带明确坐标语义的瞄准意图。
    /// </summary>
    public readonly struct AimIntent
    {
        public AimIntent(AimReference reference, Vector2 value)
        {
            Reference = reference;
            Value = value;
        }

        /// <summary>
        /// Value 的解释方式。
        /// </summary>
        public AimReference Reference { get; }

        /// <summary>
        /// 原始瞄准值。方向归一化、范围限制和合法性判断由后续玩法系统负责。
        /// </summary>
        public Vector2 Value { get; }

        /// <summary>
        /// 当前命令是否包含有效的瞄准意图。
        /// </summary>
        public bool HasValue => Reference != AimReference.None;
    }
}
