using System.IO;
using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>可靠、去重的一次性表现事件。随机旋转/淡出继续在本机对象池执行。</summary>
    public sealed class NetworkEffectEventChannel : MonoBehaviour
    {
        private const byte EFFECT = 35;
        public CoopSessionController Session;
        public OneShotSpriteEffectPool2D Pool;
        public ushort EffectId;
        private uint _sequence, _last;
        private bool _received;
        private void OnEnable()
        {
            Pool.Played += OnPlayed; Session.AuthorityMessage += Read; Session.SessionOpened += Reset;
        }
        private void OnDisable()
        {
            Pool.Played -= OnPlayed; Session.AuthorityMessage -= Read; Session.SessionOpened -= Reset;
        }
        private void Reset(bool authority) { _sequence = _last = 0; _received = false; }
        private void OnPlayed(Vector2 position, float angle)
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing) return;
            Session.SendAuthority(EFFECT, w => { w.Write(EffectId); w.Write(++_sequence);
                w.Write(position.x); w.Write(position.y); w.Write(angle); }, true);
        }
        private void Read(byte kind, BinaryReader r)
        {
            if (kind != EFFECT) return;
            // 每个订阅者必须从同一起点读取；会话层提供独立读取位置。
            if (r.ReadUInt16() != EffectId) return;
            uint sequence = r.ReadUInt32(); var position = new Vector2(r.ReadSingle(), r.ReadSingle()); float angle = r.ReadSingle();
            if (_received && !RemoteCommandSource.IsNewer(sequence, _last)) return;
            _received = true; _last = sequence; Pool.TryPlay(position, angle);
        }
    }
}
