using DeepSleep.Runtime.Input.Commands;

namespace DeepSleep.Runtime.Players.Commands
{
    /// <summary>权限封禁时只接收取消已有动作的意图，绝不能开启或造成新的攻击。</summary>
    public interface IPlayerCancellationCommandConsumer
    {
        void ConsumeCancellation(in PlayerCommand command);
    }
}
