using System;
using System.IO;
using DeepSleep.Runtime.Networking;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>对照标准 BinaryWriter；特殊 IEEE 位型也必须原样一致，即使网络目录会拒绝非有限字段。</summary>
    public static class NetworkBinaryWriterChecks
    {
        public static string Run()
        {
            uint[] boundaries = {
                0x00000000, 0x80000000, 0x00000001, 0x80000001, 0x007fffff, 0x807fffff,
                0x00800000, 0x80800000, 0x3f800000, 0xbf800000, 0x7f7fffff, 0xff7fffff,
                0x7f800000, 0xff800000, 0x7fc00000, 0x7fa12345, 0xffc12345
            };
            string[] strings = { string.Empty, "ASCII", "豆包", "汉字\0换行\n", "emoji🙂𐐷" };
            using var expectedStream = new MemoryStream(512);
            using var actualStream = new MemoryStream(512);
            using var expected = new BinaryWriter(expectedStream);
            // 按基类引用调用，确保现有 Action<BinaryWriter> 能走到 float 的 override。
            using BinaryWriter actual = new NetworkBinaryWriter(actualStream);
            int cases = 0;
            foreach (uint bits in boundaries) Check(bits, strings[cases++ % strings.Length], expected, actual);
            uint random = 0x36D97A5B;
            for (int i = 0; i < 4096; i++)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                Check(random, strings[cases++ % strings.Length], expected, actual);
            }

            string calibration = EditorAllocationProbe.Calibrate();
            // 明确预分配并预热，再单独记录优化 writer 的 float 写入次数。
            for (int i = 0; i < 32; i++) { actualStream.Position = 0; actual.Write(1.25f); }
            actualStream.SetLength(0); actualStream.Position = 0;
            using var probe = new EditorAllocationProbe();
            probe.Begin(); actual.Write(1.25f); EditorAllocationProbe.Result allocation = probe.End();
            Require(allocation.IsValid && allocation.Count == 0, "NetworkBinaryWriter.Write(float) still allocated");
            return "PASS: " + cases + " IEEE float bit patterns + mixed strings/integers/bools exactly match BinaryWriter; " +
                "positive/negative zero, subnormal, finite limits, infinities, NaN payloads, fixed xorshift corpus (runtime may quiet signaling NaN before Write). " +
                "Preallocated override float write=0 GC.Alloc events. " + calibration;
        }

        private static void Check(uint bits, string text, BinaryWriter expected, BinaryWriter actual)
        {
            var expectedStream = (MemoryStream)expected.BaseStream;
            var actualStream = (MemoryStream)actual.BaseStream;
            expectedStream.SetLength(0); expectedStream.Position = 0;
            actualStream.SetLength(0); actualStream.Position = 0;
            float value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
            uint inputBits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            WriteMixed(expected, value, text); WriteMixed(actual, value, text);
            Require(expectedStream.Length == actualStream.Length, "Writer length mismatch");
            byte[] reference = expectedStream.GetBuffer(), result = actualStream.GetBuffer();
            for (int i = 0; i < expectedStream.Length; i++)
                if (reference[i] != result[i]) throw new InvalidOperationException("Writer byte mismatch at " + bits.ToString("X8"));
            // 浮点字段排在第一个字节之后；检查实际传入 float 的小端位型。
            // Mono 可能在 Int32BitsToSingle/传值阶段将 signaling NaN quiet 化；
            // 不承诺恢复进入 Write 之前已变化的 quiet bit。标准/优化 writer 的逐字节对照仍必须完全相同。
            for (int i = 0; i < 4; i++)
                if (result[i + 1] != (byte)(inputBits >> (i * 8))) throw new InvalidOperationException("IEEE bits changed at " + bits.ToString("X8"));
        }

        private static void WriteMixed(BinaryWriter writer, float value, string text)
        {
            writer.Write((byte)0xA5); writer.Write(value); writer.Write(text);
            writer.Write(unchecked((int)0x87654321)); writer.Write(true); writer.Write((ushort)65530);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
