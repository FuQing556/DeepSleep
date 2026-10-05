using System;
using DeepSleep.Runtime.Networking;

namespace DeepSleep.Editor.Networking
{
    /// <summary>编辑器装配唯一网络版本入口；新发布只在这里推进内容版本，旧安装器不能回退版本。</summary>
    public static class NetworkBuildRevision
    {
        public const string CurrentContentVersion = "20261005-rooftop-node-1";

        /// <summary>只设置版本字段；调用方继续负责原有的 SetDirty/Save 行为。</summary>
        public static void Apply(NetworkTuningConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.ProtocolVersion = NetworkMessageCatalog.ProtocolVersion;
            config.ContentVersion = CurrentContentVersion;
        }
    }
}
