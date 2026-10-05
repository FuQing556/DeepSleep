using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>工作缓冲优化的语义门禁：数组所有权、嵌套收发、异常恢复和订阅快照顺序。</summary>
    public static class NetworkSessionBufferChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run()
        {
            var root = new GameObject("NetworkSessionBufferChecks_Isolated"); root.SetActive(false);
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>(); config.MaximumMessageBytes = 16384;
            var session = root.AddComponent<CoopSessionController>(); session.Config = config;
            var transport = new Capture { Server = true };
            Set(session, "_transport", transport); Set(session, "_hasPeer", true); Set(session, "_peer", (ulong)1);
            Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
            var receive = (Action<ulong, byte[]>)typeof(CoopSessionController).GetMethod("Receive", Hidden)
                .CreateDelegate(typeof(Action<ulong, byte[]>), session);
            int checks = 0;
            try
            {
                session.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, w => Write(w, 1), true);
                byte[] retained = transport.Packets[0];
                session.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, w => Write(w, 2), false);
                Equal(retained, Packet(1)); Equal(transport.Packets[1], Packet(2));
                Require(!ReferenceEquals(retained, transport.Packets[1]) && transport.Reliable[0] && !transport.Reliable[1], "Packet ownership or reliability changed"); checks++;

                transport.Clear();
                session.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, w =>
                {
                    w.Write((uint)10);
                    session.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, nested => Write(nested, 20), false);
                    WriteTail(w);
                }, true);
                Require(transport.Packets.Count == 2 && !transport.Reliable[0] && transport.Reliable[1], "Nested send order changed");
                Equal(transport.Packets[0], Packet(20)); Equal(transport.Packets[1], Packet(10)); checks++;

                bool threw = false;
                try { session.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, w => { w.Write(123); throw new InvalidOperationException("Expected writer fault"); }, true); }
                catch (InvalidOperationException) { threw = true; }
                transport.Clear(); session.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, w => Write(w, 30), true);
                Require(threw && transport.Packets.Count == 1, "Writer exception swallowed or partial packet sent");
                Equal(transport.Packets[0], Packet(30)); checks++;

                transport.Server = false;
                var order = new List<string>(); bool changed = false;
                Action<byte, BinaryReader> second = (kind, reader) => { CheckPayload(reader); order.Add("second"); };
                Action<byte, BinaryReader> third = (kind, reader) => { CheckPayload(reader); order.Add("third"); };
                Action<byte, BinaryReader> first = (kind, reader) =>
                {
                    CheckPayload(reader); order.Add("first");
                    if (!changed) { changed = true; session.AuthorityMessage -= second; session.AuthorityMessage += third; }
                };
                session.AuthorityMessage += first; session.AuthorityMessage += second;
                receive(0, Packet(1)); receive(0, Packet(2));
                Require(string.Join(",", order) == "first,second,first,third", "Subscription mutation changed in-flight snapshot order"); checks++;
                session.AuthorityMessage -= first; session.AuthorityMessage -= third;

                order.Clear(); byte[] nestedPacket = Packet(2);
                Action<byte, BinaryReader> nestedFirst = (kind, reader) =>
                {
                    uint sequence = reader.ReadUInt32(); order.Add("begin" + sequence);
                    if (sequence == 1) receive(0, nestedPacket);
                    Require(reader.ReadByte() == 1 && reader.ReadSingle() == 3f, "Nested receive replaced outer reader/buffer");
                    order.Add("end" + sequence);
                };
                Action<byte, BinaryReader> nestedSecond = (kind, reader) => order.Add("second" + CheckPayload(reader));
                session.AuthorityMessage += nestedFirst; session.AuthorityMessage += nestedSecond;
                receive(0, Packet(1));
                Require(string.Join(",", order) == "begin1,begin2,end2,second2,end1,second1", "Nested receive order/state changed"); checks++;
                session.AuthorityMessage -= nestedFirst; session.AuthorityMessage -= nestedSecond;

                int delivered = 0; uint rejected = session.Diagnostics.RejectedCount(NetworkMessageCatalog.Authority.CombatFeedback);
                Action<byte, BinaryReader> invalidReader = (kind, reader) => { reader.ReadByte(); throw new IOException("Expected malformed handler"); };
                Action<byte, BinaryReader> validReader = (kind, reader) => { CheckPayload(reader); delivered++; };
                session.AuthorityMessage += invalidReader; session.AuthorityMessage += validReader;
                receive(0, Packet(3));
                Require(delivered == 1 && session.Diagnostics.RejectedCount(NetworkMessageCatalog.Authority.CombatFeedback) == rejected + 1,
                    "Handler exception prevented next subscriber or failed diagnostic"); checks++;
                session.AuthorityMessage -= invalidReader;
                receive(0, new byte[] { NetworkMessageCatalog.Authority.CombatFeedback, 1 });
                Require(delivered == 1, "Truncated frame reached subscribers");
                receive(0, Packet(4)); Require(delivered == 2, "Rejected frame poisoned subsequent reader"); checks++;
                session.AuthorityMessage -= validReader;

                transport.Server = true; int peerDelivered = 0;
                Action<byte, BinaryReader> peerReader = (kind, reader) =>
                { Require(kind == NetworkMessageCatalog.Peer.RestNodeReady && reader.ReadBoolean(), "Peer dispatch changed"); peerDelivered++; };
                session.PeerMessage += peerReader;
                receive(1, new byte[] { NetworkMessageCatalog.Peer.RestNodeReady, 1 });
                Require(peerDelivered == 1 && session.Diagnostics.HandlerFaults == 0, "Direction caches mixed or handler fault occurred"); checks++;
                session.PeerMessage -= peerReader;
                return "PASS: " + checks + " session buffer semantics: retained packet ownership/reliability, nested send, writer exception recovery, subscription snapshot order, nested receive, handler isolation, malformed recovery, direction separation. Temporary objects, no socket.";
            }
            finally { Set(session, "_transport", null); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(config); }
        }

        private sealed class Capture : ITransportAdapter
        {
            public bool Server;
            public readonly List<byte[]> Packets = new();
            public readonly List<bool> Reliable = new();
            public bool IsServer => Server;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => throw new InvalidOperationException();
            public bool StartClient(string address) => throw new InvalidOperationException();
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable) { Packets.Add(payload); Reliable.Add(reliable); }
            public void Clear() { Packets.Clear(); Reliable.Clear(); }
        }
        private static byte[] Packet(uint sequence)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(NetworkMessageCatalog.Authority.CombatFeedback); Write(writer, sequence); return stream.ToArray();
        }
        private static void Write(BinaryWriter writer, uint sequence) { writer.Write(sequence); WriteTail(writer); }
        private static void WriteTail(BinaryWriter writer)
        { writer.Write((byte)1); writer.Write(3f); writer.Write(4f); writer.Write(1f); writer.Write(0f); writer.Write(7f); writer.Write(.2f); }
        private static uint CheckPayload(BinaryReader reader)
        {
            Require(reader.BaseStream.Position == 1, "Reader cursor was not reset for subscriber");
            uint sequence = reader.ReadUInt32();
            Require(reader.ReadByte() == 1 && reader.ReadSingle() == 3f && reader.ReadSingle() == 4f, "Payload changed");
            return sequence;
        }
        private static void Equal(byte[] a, byte[] b)
        { Require(a.Length == b.Length, "Packet length changed"); for (int i = 0; i < a.Length; i++) Require(a[i] == b[i], "Packet bytes changed"); }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    }
}
