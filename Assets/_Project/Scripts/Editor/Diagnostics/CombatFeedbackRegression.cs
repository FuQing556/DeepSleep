using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>在从入口启动的 Play 场景中显式执行；内存传输覆盖真实会话分发及表现订阅。</summary>
    public static class CombatFeedbackRegression
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Transport : ITransportAdapter
        {
            public bool Server;
            public readonly List<byte[]> Packets = new();
            public readonly List<byte[]> Controls = new();
            public bool IsServer => Server;
            public bool IsConnected => true;
            public string DisconnectReason => "";
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => true;
            public bool StartClient(string address) => true;
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable)
            {
                if (payload[0] == 4) Controls.Add(payload);
                if (payload[0] != 41) return;
                Require(reliable, "Feedback must be reliable");
                Packets.Add(payload);
            }
        }

        public static string RunAiToggles()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play through Boot/MainMenu first.");
            var channel = UnityEngine.Object.FindFirstObjectByType<NetworkCombatFeedbackChannel>();
            Require(channel != null, "Missing feedback channel");
            var session = channel.Session;
            Require(session.Phase == SessionPhase.Offline && !session.HasPeer, "Offline test scene required");
            var transport = new Transport { Server = true };
            string[] fields = { "_transport", "_hasPeer", "_peer", "<Phase>k__BackingField", "<HostRole>k__BackingField", "<LocalRole>k__BackingField",
                "_hostControl", "_guestControl", "_boundHost", "_boundGuest", "_epoch" };
            var previous = fields.Select(f => Get(session, f)).ToArray();
            var dsSource = Get(session.DeepSeek.CommandDispatcher, "_commandSource");
            var hsSource = Get(session.Harness.CommandDispatcher, "_commandSource");
            float scale = Time.timeScale;
            int checks = 0;
            try
            {
                Time.timeScale = 0f;
                Set(session, "_transport", transport); Set(session, "_hasPeer", true); Set(session, "_peer", (ulong)1);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                foreach (PlayerRole hostRole in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    Set(session, "<HostRole>k__BackingField", hostRole);
                    Set(session, "<LocalRole>k__BackingField", hostRole);
                    Set(session, "_guestControl", SlotControl.Human);
                    transport.Server = true; Event(session, "SessionOpened", true);
                    foreach (bool ai in new[] { false, true, false, true, false })
                    {
                        session.SetLocalAi(ai);
                        var hostActor = hostRole == PlayerRole.DeepSeek ? session.DeepSeek : session.Harness;
                        object expected = ai ? (object)(hostRole == PlayerRole.DeepSeek ? session.DeepSeekAi : session.HarnessAi) : Get(session, "_input");
                        Require(ReferenceEquals(Get(hostActor.CommandDispatcher, "_commandSource"), expected), "Host AI binding");
                        Clear(channel); transport.Packets.Clear();
                        EmitLaser(channel);
                        Require(Counts(channel) == "1/1" && transport.Packets.Count == 1, "Host feedback during AI=" + ai); checks++;
                    }
                    foreach (bool ai in new[] { false, true, false, true, false })
                    {
                        // 实际客人 SetLocalAi -> 控制包 -> 主机 Receive/BindSources。
                        transport.Server = false; transport.Controls.Clear();
                        session.SetLocalAi(ai);
                        Require(transport.Controls.Count == 1, "Client control request");
                        transport.Server = true;
                        Call(session, "Receive", (ulong)1, transport.Controls[0]);
                        var guestActor = hostRole == PlayerRole.DeepSeek ? session.Harness : session.DeepSeek;
                        object expected = ai ? (object)(hostRole == PlayerRole.DeepSeek ? session.HarnessAi : session.DeepSeekAi) : Get(session, "_remote");
                        Require(ReferenceEquals(Get(guestActor.CommandDispatcher, "_commandSource"), expected), "Guest AI binding on host");
                        Clear(channel); transport.Packets.Clear(); EmitLaser(channel);
                        Require(Counts(channel) == "1/1" && transport.Packets.Count == 1, "Authority feedback after guest AI request");
                        byte[] hitPacket = transport.Packets[0];
                        Clear(channel); transport.Server = false; Event(session, "SessionOpened", false);
                        Call(session, "Receive", (ulong)0, hitPacket);
                        Require(Counts(channel) == "1/1", "Client feedback during AI=" + ai); checks++;
                        Event(session, "SessionClosed"); transport.Server = true; Event(session, "SessionOpened", true);
                    }
                }
                return "PASS: " + checks + " AI routing/feedback cases. Host and guest DS/HS, human -> AI -> human repeated, real SetLocalAi/control packet/Receive/BindSources, local and replicated laser impact + damage number.";
            }
            finally
            {
                Event(session, "SessionClosed"); Clear(channel);
                for (int i = 0; i < fields.Length; i++) Set(session, fields[i], previous[i]);
                Set(session.DeepSeek.CommandDispatcher, "_commandSource", dsSource);
                Set(session.Harness.CommandDispatcher, "_commandSource", hsSource);
                session.DeepSeekAi.ReleaseControl(); session.DeepSeekAi.ResetIntent();
                session.HarnessAi.ReleaseControl(); session.HarnessAi.ResetIntent();
                Time.timeScale = scale;
            }
        }

        private static void EmitLaser(NetworkCombatFeedbackChannel channel) =>
            Event(channel.Laser, "HitConfirmed", new HarnessTerminalLaserHitConfirmed(0,
                channel.Session.Harness.GetComponent<DamageHitbox2D>(), Vector2.zero, Vector2.right, .3f, 10f, true));

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play through Boot/MainMenu first.");
            var channel = UnityEngine.Object.FindFirstObjectByType<NetworkCombatFeedbackChannel>();
            Require(channel != null, "Missing installed feedback channel");
            var session = channel.Session;
            Require(session.Phase == SessionPhase.Offline && !session.HasPeer, "Run only in an offline test scene");
            var gate = session.GetComponent<NetworkAuthorityGate>();
            Require(!gate.AuthorityOnly.Contains(channel.Impacts) && !gate.AuthorityOnly.Contains(channel.Numbers), "Presenters are authority gated");
            var transport = new Transport { Server = true };
            string[] fields = { "_transport", "_hasPeer", "_peer", "<Phase>k__BackingField", "<HostRole>k__BackingField", "<LocalRole>k__BackingField" };
            var previous = fields.Select(f => Get(session, f)).ToArray();
            float scale = Time.timeScale;
            int checks = 0;
            try
            {
                Time.timeScale = 0f;
                Set(session, "_transport", transport); Set(session, "_hasPeer", true); Set(session, "_peer", (ulong)1);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    Set(session, "<HostRole>k__BackingField", role); Set(session, "<LocalRole>k__BackingField", role);
                    transport.Server = true;
                    Event(session, "SessionOpened", true);
                    Clear(channel); transport.Packets.Clear();
                    var hitbox = session.Harness.GetComponent<DamageHitbox2D>();
                    Event(channel.Laser, "HitConfirmed", new HarnessTerminalLaserHitConfirmed(0, hitbox, Vector2.zero, Vector2.right, .3f, 10f, true));
                    Require(Counts(channel) == "1/1" && transport.Packets.Count == 1, "Host laser local feedback/send " + role);
                    checks++;
                    Event(channel.Rice, "HitConfirmed", new RiceProjectileHitConfirmed(hitbox, Vector2.zero, Vector2.right, 2f));
                    Event(channel.Melee, "DamageConfirmed", new HarnessMeleeDamageHitConfirmed(Vector2.zero, Vector2.right, 3f, false));
                    Event(channel.Melee, "HitConfirmed", Vector2.zero, 0f);
                    Require(Counts(channel) == "2/3" && transport.Packets.Count == 4, "Host DS/HS feedback " + role); checks++;
                    var packets = transport.Packets.ToArray();
                    Clear(channel);
                    // 同一场景切换成客人，使用实际 AuthorityGate 及会话消息分发。
                    transport.Server = false; Event(session, "SessionOpened", false);
                    Require(!channel.Laser.enabled && channel.Impacts.enabled && channel.Numbers.enabled, "Client authority boundary"); checks++;
                    foreach (var packet in packets) Call(session, "Receive", (ulong)0, packet);
                    Require(Counts(channel) == "2/3", "Client effects and all damage number sources " + role); checks++;
                    foreach (var packet in packets.Reverse()) Call(session, "Receive", (ulong)0, packet);
                    Require(Counts(channel) == "2/3" && transport.Packets.Count == 4, "Duplicates/out of order echoed or replayed"); checks++;
                    Clear(channel);
                    Event(session, "SessionClosed"); Event(session, "SessionOpened", false);
                    Call(session, "Receive", (ulong)0, packets[0]);
                    Require(Counts(channel) == "1/1", "Reconnect sequence reset"); checks++;
                    Event(session, "SessionClosed"); Clear(channel);
                    transport.Server = true; Event(session, "SessionOpened", true);
                    Event(channel.Laser, "HitConfirmed", new HarnessTerminalLaserHitConfirmed(0, hitbox, Vector2.zero, Vector2.right, .3f, 10f, true));
                    Require(Counts(channel) == "1/1" && channel.Laser.enabled, "Client to host restored subscriptions"); checks++;
                    Clear(channel);
                }
                // 主机离线本地路径依然存在，不向网络发送。
                Event(session, "SessionClosed"); Set(session, "<Phase>k__BackingField", SessionPhase.Offline);
                int sent = transport.Packets.Count;
                Event(channel.Laser, "HitConfirmed", new HarnessTerminalLaserHitConfirmed(0, session.Harness.GetComponent<DamageHitbox2D>(), Vector2.zero, Vector2.right, .3f, 10, true));
                Require(Counts(channel) == "1/1" && sent == transport.Packets.Count, "Offline feedback regression"); checks++;
                return "PASS: " + checks + " checks. Both host roles, local feedback, reliable delivery through Session.Receive, client gate, DS/HS damage numbers, melee impacts, duplicate/stale rejection, reconnect, client-to-host and offline restore.";
            }
            finally
            {
                Event(session, "SessionClosed"); Clear(channel);
                for (int i = 0; i < fields.Length; i++) Set(session, fields[i], previous[i]);
                Time.timeScale = scale;
            }
        }

        private static void Clear(NetworkCombatFeedbackChannel channel)
        {
            channel.Impacts.enabled = false; channel.Impacts.enabled = true;
            channel.Numbers.enabled = false; channel.Numbers.enabled = true;
        }
        private static string Counts(NetworkCombatFeedbackChannel channel)
        {
            var impacts = ((IEnumerable)Get(channel.Impacts, "_all")).Cast<HarnessLaserHitEffect2D>().Count(x => x.IsPlaying);
            var numbers = ((IEnumerable)Get(channel.Numbers, "_all")).Cast<DamageNumberEntryView>().Count(x => x.IsPlaying);
            return impacts + "/" + numbers;
        }
        private static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        private static void Event(object owner, string name, params object[] args) => ((Delegate)Get(owner, name))?.DynamicInvoke(args);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
