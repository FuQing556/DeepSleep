namespace DeepSleep.Runtime.Players.Identity
{
    /// <summary>
    /// 可被存档和网络协议引用的角色职责。
    /// 显式数值用于保证枚举顺序调整时不会改变既有数据含义。
    /// </summary>
    public enum PlayerRole : byte
    {
        DeepSeek = 0,
        Harness = 1
    }
}
