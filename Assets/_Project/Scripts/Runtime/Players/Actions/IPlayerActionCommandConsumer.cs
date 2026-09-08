using DeepSleep.Runtime.Players.Commands;

namespace DeepSleep.Runtime.Players.Actions
{
    /// <summary>
    /// 声明命令消费者属于哪些可限制行动类别。
    /// 分发器据此统一拦截行为，消费者无需了解复活等具体状态。
    /// </summary>
    public interface IPlayerActionCommandConsumer : IPlayerCommandConsumer
    {
        PlayerActionBlock ActionCategory { get; }
    }
}
