using System;
using System.IO;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>
    /// 保持 BinaryWriter 线格式，仅避开 Unity Mono 的 Write(float) 临时 byte[]。
    /// SingleToInt32Bits 是位重解释，不是数值转换；base.Write(int) 仍按标准小端输出。
    /// </summary>
    public sealed class NetworkBinaryWriter : BinaryWriter
    {
        public NetworkBinaryWriter(Stream output) : base(output) { }

        public override void Write(float value) => base.Write(BitConverter.SingleToInt32Bits(value));
    }
}
