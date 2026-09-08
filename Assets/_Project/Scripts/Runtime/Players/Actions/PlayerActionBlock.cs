using System;

namespace DeepSleep.Runtime.Players.Actions
{
    /// <summary>
    /// 可以被上层状态临时禁止的玩家行动类别。
    /// 使用位标记是为了允许复活、眩晕和剧情等来源叠加限制。
    /// </summary>
    [Flags]
    public enum PlayerActionBlock
    {
        None = 0,
        Movement = 1 << 0,
        AutomaticCombat = 1 << 1,
        ActiveCombat = 1 << 2,
    }
}
