using System;

namespace DeepSleep.Runtime.Input.Commands
{
    /// <summary>
    /// 描述一个按钮在当前模拟刻内发生的阶段。
    /// Pressed 与 Released 可以同时存在，用于表示两个模拟刻之间完成的一次快速点击。
    /// </summary>
    [Flags]
    public enum CommandButtonState : byte
    {
        None = 0,
        Pressed = 1 << 0,
        Held = 1 << 1,
        Released = 1 << 2,
    }
}
