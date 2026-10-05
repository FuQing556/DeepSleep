namespace DeepSleep.Runtime.Networking
{
    /// <summary>
    /// 连接前由会话显式传入关卡身份；传输将其作为兼容性范围，在预留重连身份前拒绝错关卡。
    /// 活跃连接/连接中/关闭中不得改写；失败不更改已保存的范围。
    /// </summary>
    public interface ITransportLevelScope
    {
        bool TrySetLevelId(string levelId);
    }
}
