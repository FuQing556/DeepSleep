using System.IO;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    public sealed class NetworkPlayerSnapshotChannel : MonoBehaviour
    {
        private const byte PLAYER_SNAPSHOT = 32;
        public CoopSessionController Session;
        public NetworkPlayerReplica DeepSeek, Harness;
        private float _nextSend;
        private uint _sequence, _lastReceived;
        private bool _hasReceived;
        public uint ReceivedSequence => _lastReceived;
        private void OnEnable() { Session.AuthorityMessage += Read; Session.SessionOpened += Reset; }
        private void OnDisable() { Session.AuthorityMessage -= Read; Session.SessionOpened -= Reset; }
        private void Reset(bool authority) { _hasReceived = false; _sequence = 0; _nextSend = 0; }
        private void LateUpdate()
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 1f / Session.Config.SnapshotRate;
            Session.SendAuthority(PLAYER_SNAPSHOT, Write, false);
        }
        private void Write(BinaryWriter w) { w.Write(++_sequence); w.Write(Session.LastGuestCommand); DeepSeek.Write(w); Harness.Write(w); }
        private void Read(byte kind, BinaryReader r)
        {
            if (kind != PLAYER_SNAPSHOT) return;
            uint sequence = r.ReadUInt32();
            uint ack = r.ReadUInt32();
            if (_hasReceived && !RemoteCommandSource.IsNewer(sequence, _lastReceived)) return;
            // 两个角色使用稳定Role，不使用场景数组顺序作为网络身份。
            for (int i = 0; i < 2; i++)
            {
                byte role = r.ReadByte();
                if (role == 0) DeepSeek.Read(r, ack); else if (role == 1) Harness.Read(r, ack); else return;
            }
            _lastReceived = sequence; _hasReceived = true;
        }
    }
}
