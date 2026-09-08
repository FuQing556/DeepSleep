using DeepSleep.Runtime.Input.Commands;

namespace DeepSleep.Runtime.Players.Commands
{
    /// <summary>
    /// 在普通命令消费者之前观察同一份命令。
    /// 用于先解除复活引导等限制，再让本帧操作正常执行。
    /// </summary>
    public interface IPlayerCommandPreprocessor
    {
        void PreprocessCommand(in PlayerCommand command, float deltaTime);
    }
}
