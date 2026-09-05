namespace DeepSleep.Runtime.Input.Commands
{
    /// <summary>
    /// 描述瞄准向量应当如何解释，避免不同坐标空间共用一个向量时产生歧义。
    /// 数值会进入存档或网络协议，已经使用的成员不得重新编号。
    /// </summary>
    public enum AimReference : byte
    {
        None = 0,
        Direction = 1,
        WorldPosition = 2,

        /// <summary>
        /// 以屏幕左下角为 (0, 0)、右上角为 (1, 1) 的归一化屏幕坐标。
        /// </summary>
        NormalizedScreenPosition = 3,
    }
}
