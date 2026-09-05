using DeepSleep.Runtime.Input.Commands;

namespace DeepSleep.Runtime.Players.Commands
{
    /// <summary>
    /// 接收同一模拟刻玩家命令的玩法模块。
    /// </summary>
    public interface IPlayerCommandConsumer
    {
        void ConsumeCommand(in PlayerCommand command, float deltaTime);
    }
}
