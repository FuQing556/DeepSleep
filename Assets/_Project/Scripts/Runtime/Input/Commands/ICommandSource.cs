namespace DeepSleep.Runtime.Input.Commands
{
    /// <summary>
    /// 为固定模拟刻提供玩家命令的统一入口。
    /// 实现可以读取本地输入、远端命令缓冲区或同伴 AI，但不得直接修改玩法对象。
    /// </summary>
    public interface ICommandSource
    {
        /// <summary>
        /// 尝试取得指定模拟刻的命令。
        /// 返回 false 只表示该控制源当前没有可用命令；缺帧处理策略由命令消费者决定。
        /// </summary>
        /// <param name="simulationTick">需要命令的固定模拟刻。</param>
        /// <param name="command">成功时返回完整且标记了目标模拟刻的命令。</param>
        /// <returns>取得命令时为 true，否则为 false。</returns>
        bool TryGetCommand(uint simulationTick, out PlayerCommand command);
    }
}
