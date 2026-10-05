namespace DeepSleep.Runtime.Networking
{
    /// <summary>Stop 发出后，场景路由必须等底层关闭真正完成；不以调用 Stop 本身当作完成。</summary>
    public interface ITransportShutdownStatus
    {
        bool IsShutdownComplete { get; }
    }
}
