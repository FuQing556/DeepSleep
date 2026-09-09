using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>协议/资源兼容性与传输预算。不是技能数值的第二份配置。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/配置/联机", fileName = "CFG_Network")]
    public sealed class NetworkTuningConfig : ScriptableObject
    {
        public ushort Port;
        public ushort ProtocolVersion;
        public string ClientVersion;
        public string ContentVersion;
        [Min(1)] public int SnapshotRate;
        [Min(0.1f)] public float InputTimeout;
        [Min(1)] public float ConnectionTimeout;
        [Min(1)] public int MaximumQueuedCommands;
        [Min(128)] public int MaximumMessageBytes;
        [Min(1)] public float RemoteInterpolationSpeed;

        public bool IsValid => Port > 0 && ProtocolVersion > 0 &&
            !string.IsNullOrEmpty(ClientVersion) && !string.IsNullOrEmpty(ContentVersion) &&
            SnapshotRate > 0 && InputTimeout > 0 && ConnectionTimeout > 0 &&
            MaximumQueuedCommands > 0 && MaximumMessageBytes >= 128 && RemoteInterpolationSpeed > 0;
    }
}
