using DeepSleep.Runtime.Input.Commands;

namespace DeepSleep.Runtime.Players.Commands
{
    /// <summary>
    /// 在普通命令消费者之前观察同一份命令。
    /// 用于先解除复活引导等限制，再让本帧操作正常执行。
    /// </summary>
    public interface IPlayerCommandPreprocessor
    {
        /// <param name="realDeltaTime">未受场景倍率影响的活动时间；暂停时不调用。</param>
        void PreprocessCommand(in PlayerCommand command, float realDeltaTime);
    }
}
