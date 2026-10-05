using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>仅离线 Play 场景运行：注入已接受的受击事实，经真实 Session.Receive 检查可靠复制边界。</summary>
    public static class PlayerHitFeedbackNetworkChecks
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private const byte PLAYER_HIT_FEEDBACK = NetworkMessageCatalog.Authority.PlayerHitFeedback;

        private sealed class Transport : ITransportAdapter
        {
            public bool Server;
            public readonly List<byte[]> Packets = new();
            public bool IsServer => Server;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => true;
            public bool StartClient(string address) => true;
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable)
            {
                if (payload[0] != PLAYER_HIT_FEEDBACK) return;
                Require(reliable && peer == 1, "Player hits must be reliably sent to the peer");
                Packets.Add(payload);
            }
        }

        public static string Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play through Boot/MainMenu first.");
            var channel = UnityEngine.Object.FindAnyObjectByType<NetworkPlayerHitFeedbackChannel>();
            Require(channel != null && channel.isActiveAndEnabled, "Missing active player hit feedback channel");
            Require(channel.TryValidateConfiguration(out string reason), reason);
            var session = channel.Session;
            Require(session.Phase == SessionPhase.Offline && !session.HasPeer, "Offline scene required");
            var transport = new Transport { Server = true };
            string[] fields = { "_transport", "_hasPeer", "_peer", "<Phase>k__BackingField",
                "<HostRole>k__BackingField", "<LocalRole>k__BackingField", "_hostControl", "_guestControl" };
            object[] previous = fields.Select(field => Get(session, field)).ToArray();
            var dsHealth = (HealthComponent)Get(channel.DeepSeek.Receiver, "_health");
            var hsHealth = (HealthComponent)Get(channel.Harness.Receiver, "_health");
            float dsHp = dsHealth.CurrentHealth, hsHp = hsHealth.CurrentHealth;
            float scale = Time.timeScale;
            int checks = 0;
            try
            {
                Time.timeScale = 0f;
                Set(session, "_transport", transport);
                Set(session, "_hasPeer", true);
                Set(session, "_peer", (ulong)1);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);

                foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    Set(session, "<HostRole>k__BackingField", role);
                    Set(session, "<LocalRole>k__BackingField", role);
                    transport.Server = true;
                    Event(session, "SessionOpened", true);
                    transport.Packets.Clear();
                    uint dsBefore = channel.DeepSeek.PlayedCount, hsBefore = channel.Harness.PlayedCount;
                    Emit(channel.DeepSeek.Receiver, Vector2.left, 1.25f);
                    Emit(channel.Harness.Receiver, Vector2.up, 1.5f);
                    Require(channel.DeepSeek.PlayedCount == dsBefore + 1 &&
                        channel.Harness.PlayedCount == hsBefore + 1 && transport.Packets.Count == 2,
                        "Both host actors must present and send once, host=" + role); checks++;
                    byte[][] packets = transport.Packets.ToArray();
                    RequirePacket(packets[0], PlayerRole.DeepSeek, Vector2.left, 1.25f);
                    RequirePacket(packets[1], PlayerRole.Harness, Vector2.up, 1.5f); checks++;

                    transport.Server = false;
                    Set(session, "<LocalRole>k__BackingField", role == PlayerRole.DeepSeek ? PlayerRole.Harness : PlayerRole.DeepSeek);
                    Event(session, "SessionOpened", false);
                    Require(!channel.DeepSeek.IsPlaying && !channel.Harness.IsPlaying, "Open must reset feedback"); checks++;
                    dsBefore = channel.DeepSeek.PlayedCount; hsBefore = channel.Harness.PlayedCount;
                    Emit(channel.DeepSeek.Receiver, Vector2.right, 1f);
                    Emit(channel.Harness.Receiver, Vector2.right, 1f);
                    Require(channel.DeepSeek.PlayedCount == dsBefore && channel.Harness.PlayedCount == hsBefore &&
                        transport.Packets.Count == 2, "Client local event must not double-play or send"); checks++;
                    foreach (byte[] packet in packets) Call(session, "Receive", (ulong)0, packet);
                    Require(channel.DeepSeek.PlayedCount == dsBefore + 1 && channel.Harness.PlayedCount == hsBefore + 1,
                        "Client role routing");
                    Require(Mathf.Approximately(channel.DeepSeek.RemainingProtection, 1.25f) &&
                        Mathf.Approximately(channel.Harness.RemainingProtection, 1.5f), "Protection display duration"); checks++;
                    foreach (byte[] packet in packets.Reverse()) Call(session, "Receive", (ulong)0, packet);
                    Require(channel.DeepSeek.PlayedCount == dsBefore + 1 && channel.Harness.PlayedCount == hsBefore + 1,
                        "Duplicate/stale events replayed"); checks++;

                    Set(session, "_hostControl", SlotControl.VoluntaryAi);
                    Set(session, "_guestControl", SlotControl.VoluntaryAi);
                    Call(session, "Receive", (ulong)0, Packet(3, PlayerRole.DeepSeek, Vector2.down, 1f));
                    Require(channel.DeepSeek.PlayedCount == dsBefore + 2, "AI control must not gate presentation"); checks++;
                    Event(session, "SessionClosed");
                    Require(!channel.DeepSeek.IsPlaying && !channel.Harness.IsPlaying, "Close must reset feedback"); checks++;
                    Event(session, "SessionOpened", false);
                    Call(session, "Receive", (ulong)0, packets[0]);
                    Require(channel.DeepSeek.PlayedCount == dsBefore + 3, "Reconnect must reset sequence"); checks++;
                    Event(session, "SessionClosed");
                }

                transport.Server = false;
                Event(session, "SessionOpened", false);
                uint beforeMalformed = channel.DeepSeek.PlayedCount;
                byte[][] malformed = {
                    Packet(100, (PlayerRole)255, Vector2.right, 1f),
                    Packet(100, PlayerRole.DeepSeek, new Vector2(float.NaN, 0f), 1f),
                    Packet(100, PlayerRole.DeepSeek, new Vector2(0f, float.PositiveInfinity), 1f),
                    Packet(100, PlayerRole.DeepSeek, Vector2.right * 100f, 1f),
                    Packet(100, PlayerRole.DeepSeek, Vector2.right, float.NaN),
                    Packet(100, PlayerRole.DeepSeek, Vector2.right, float.PositiveInfinity),
                    Packet(100, PlayerRole.DeepSeek, Vector2.right, -1f),
                    Packet(100, PlayerRole.DeepSeek, Vector2.right, 10.01f),
                    new byte[] { PLAYER_HIT_FEEDBACK, 100 },
                    Packet(100, PlayerRole.DeepSeek, Vector2.right, 1f).Concat(new byte[] { 0 }).ToArray()
                };
                foreach (byte[] packet in malformed) Call(session, "Receive", (ulong)0, packet);
                Require(channel.DeepSeek.PlayedCount == beforeMalformed, "Malformed payload played"); checks++;
                Call(session, "Receive", (ulong)0, Packet(1, PlayerRole.DeepSeek, Vector2.right, 1f));
                Require(channel.DeepSeek.PlayedCount == beforeMalformed + 1, "Bad packets poisoned sequence"); checks++;
                Call(session, "Receive", (ulong)1, Packet(2, PlayerRole.DeepSeek, Vector2.right, 1f));
                Require(channel.DeepSeek.PlayedCount == beforeMalformed + 1, "Non-authority sender accepted"); checks++;
                Set(session, "<Phase>k__BackingField", SessionPhase.Lobby);
                Call(session, "Receive", (ulong)0, Packet(2, PlayerRole.DeepSeek, Vector2.right, 1f));
                Require(channel.DeepSeek.PlayedCount == beforeMalformed + 1, "Lobby must not play combat feedback"); checks++;

                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                Event(session, "SessionOpened", false);
                beforeMalformed = channel.DeepSeek.PlayedCount;
                Call(session, "Receive", (ulong)0, Packet(uint.MaxValue, PlayerRole.DeepSeek, Vector2.zero, 0f));
                Call(session, "Receive", (ulong)0, Packet(0, PlayerRole.DeepSeek, Vector2.zero, 0f));
                Call(session, "Receive", (ulong)0, Packet(uint.MaxValue, PlayerRole.DeepSeek, Vector2.zero, 0f));
                Require(channel.DeepSeek.PlayedCount == beforeMalformed + 2, "Sequence wrap/stale handling"); checks++;
                Require(Mathf.Approximately(dsHealth.CurrentHealth, dsHp) && Mathf.Approximately(hsHealth.CurrentHealth, hsHp),
                    "Presentation replication changed HP"); checks++;

                Event(session, "SessionClosed");
                Set(session, "<Phase>k__BackingField", SessionPhase.Offline);
                uint offlineBefore = channel.DeepSeek.PlayedCount;
                int sentBefore = transport.Packets.Count;
                Emit(channel.DeepSeek.Receiver, Vector2.left, 1f);
                Require(channel.DeepSeek.PlayedCount == offlineBefore + 1 && transport.Packets.Count == sentBefore,
                    "Offline local presentation/send regression"); checks++;
                return "PASS: " + checks + " player hit network checks. Both roles, reliable fact routing, no client local echo, AI, duplicate/stale/wrap, reconnect, malformed/truncated/trailing bytes, sender/phase, HP isolation, offline. Source DamageAccepted is injected; not a real dual-device test.";
            }
            finally
            {
                Event(session, "SessionClosed");
                channel.DeepSeek.ResetFeedback(); channel.Harness.ResetFeedback();
                for (int i = 0; i < fields.Length; i++) Set(session, fields[i], previous[i]);
                Time.timeScale = scale;
            }
        }

        private static void Emit(PlayerDamageReceiver2D receiver, Vector2 direction, float protection)
        {
            object previous = Get(receiver, "_remainingInvulnerabilitySeconds");
            try
            {
                Set(receiver, "_remainingInvulnerabilitySeconds", protection);
                Event(receiver, "DamageAccepted", receiver,
                    new DamagePacket(1f, receiver.transform.position, direction, receiver.gameObject));
            }
            finally { Set(receiver, "_remainingInvulnerabilitySeconds", previous); }
        }

        private static byte[] Packet(uint sequence, PlayerRole role, Vector2 direction, float protection)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(PLAYER_HIT_FEEDBACK); writer.Write(sequence); writer.Write((byte)role);
            writer.Write(direction.x); writer.Write(direction.y); writer.Write(protection);
            return stream.ToArray();
        }

        private static void RequirePacket(byte[] packet, PlayerRole role, Vector2 direction, float protection)
        {
            using var stream = new MemoryStream(packet);
            using var reader = new BinaryReader(stream);
            Require(reader.ReadByte() == PLAYER_HIT_FEEDBACK, "Message ID"); reader.ReadUInt32();
            Require(reader.ReadByte() == (byte)role && Mathf.Approximately(reader.ReadSingle(), direction.x) &&
                Mathf.Approximately(reader.ReadSingle(), direction.y) && Mathf.Approximately(reader.ReadSingle(), protection) &&
                stream.Position == stream.Length, "Hit fact serialization");
        }

        private static object Get(object owner, string name) => owner.GetType().GetField(name, PRIVATE).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, PRIVATE).SetValue(owner, value);
        private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, PRIVATE).Invoke(owner, args);
        private static void Event(object owner, string name, params object[] args) => ((Delegate)Get(owner, name))?.DynamicInvoke(args);
        private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    }
}
