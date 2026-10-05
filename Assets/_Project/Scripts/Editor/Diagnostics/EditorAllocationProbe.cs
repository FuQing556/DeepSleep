using System;
using Unity.Profiling;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// Unity 当前线程 GC.Alloc 次数探针。此 marker 的 Value 是时间单位，不得当字节数。
    /// 只供显式诊断使用；不改 Profiler 全局开关，必须 Dispose 释放原生记录缓冲。
    /// </summary>
    public sealed class EditorAllocationProbe : IDisposable
    {
        // 诊断记录容量，不是玩法或网络预算；达到容量即宣告不完整，绝不当作精确计数。
        public const int Capacity = 65536;
        private ProfilerRecorder _recorder;
        private bool _disposed;
        private static byte[] _retainedCalibration;

        public readonly struct Result
        {
            public Result(int count, bool valid, bool overflow) { Count = count; IsValid = valid; Overflow = overflow; }
            public int Count { get; }
            public bool IsValid { get; }
            public bool Overflow { get; }
        }

        public EditorAllocationProbe()
        {
            _recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", Capacity,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            _recorder.Stop();
            if (!_recorder.Valid) { _recorder.Dispose(); _disposed = true; throw new InvalidOperationException("GC.Alloc recorder unavailable"); }
        }

        public void Begin()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EditorAllocationProbe));
            _recorder.Reset();
            _recorder.Start();
        }

        public Result End()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EditorAllocationProbe));
            _recorder.Stop();
            int count = _recorder.Count;
            bool overflow = count >= Capacity;
            return new Result(count, _recorder.Valid && !overflow, overflow);
        }

        public static string Calibrate()
        {
            using var probe = new EditorAllocationProbe();
            // 先运行相同测量路径一次，排除方法首次 JIT/计数访问的冷开销。
            probe.Begin(); probe.End();
            probe.Begin(); Result empty = probe.End();
            probe.Begin();
            _retainedCalibration = new byte[4096]; _retainedCalibration[4095] = 123;
            Result known = probe.End();
            GC.KeepAlive(_retainedCalibration);
            if (!empty.IsValid || !known.IsValid || empty.Count != 0 || known.Count != 1)
                throw new InvalidOperationException("GC.Alloc calibration failed: empty=" + empty.Count +
                    ", retained4096=" + known.Count + ", overflow=" + (empty.Overflow || known.Overflow));
            return "GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=" +
                Capacity + "; count only (marker Value is TimeNanoseconds, not bytes).";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _recorder.Dispose(); _disposed = true;
        }
    }
}
