namespace DeepSleep.Runtime.Players.Revive
{
    /// <summary>
    /// 由持续性的主动动作声明当前是否阻止自动开始救援。
    /// 冷却不属于正在执行动作，因此不应返回 true。
    /// </summary>
    public interface IPlayerReviveStartBlocker
    {
        bool BlocksReviveStart { get; }
    }
}
