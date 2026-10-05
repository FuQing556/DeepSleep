using System.Text;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>常驻有界计数和最近事件；不逐包刷日志，不记录地址、身份票据或玩家输入内容。</summary>
    public sealed class NetworkSessionDiagnostics
    {
        private readonly uint[] _sent = new uint[256], _received = new uint[256], _rejected = new uint[256];
        private readonly string[] _events = new string[24];
        private int _next, _count;
        public uint HandlerFaults { get; private set; }
        public uint SentCount(byte kind) => _sent[kind];
        public uint ReceivedCount(byte kind) => _received[kind];
        public uint RejectedCount(byte kind) => _rejected[kind];
        public void Sent(byte kind) => _sent[kind]++;
        public void Received(byte kind) => _received[kind]++;
        public void Rejected(byte kind, string reason)
        {
            _rejected[kind]++;
            // 计数完整，坏包文字只保留该类别第一次，避免恶意流量造成逐包字符串/日志洪水。
            if (_rejected[kind] == 1) Event("Rejected " + kind + ": " + reason);
        }
        public void HandlerFault(byte kind, string handler)
        { HandlerFaults++; if (HandlerFaults <= 8) Event("Handler fault " + kind + ": " + handler); }
        public void Event(string message)
        { _events[_next] = message; _next = (_next + 1) % _events.Length; _count = System.Math.Min(_count + 1, _events.Length); }
        public string Describe()
        {
            var b = new StringBuilder("Network message counters (sent/received/rejected):");
            for (int i = 0; i < 256; i++)
                if (_sent[i] != 0 || _received[i] != 0 || _rejected[i] != 0)
                    b.Append('\n').Append(i).Append(": ").Append(_sent[i]).Append('/').Append(_received[i]).Append('/').Append(_rejected[i]);
            b.Append("\nHandler faults: ").Append(HandlerFaults);
            for (int i = 0; i < _count; i++) b.Append('\n').Append(_events[(_next - _count + i + _events.Length) % _events.Length]);
            return b.ToString();
        }
    }
}
