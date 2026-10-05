using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>使用真实玩法写入器而非手拼快照，验证新协议预检不会误拦截现有场景内容。</summary>
    public static class NetworkWriterCatalogChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Capture : ITransportAdapter
        {
            public readonly List<byte[]> Packets = new();
            public bool IsServer => true;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => true;
            public bool StartClient(string address) => false;
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable) => Packets.Add(payload);
        }

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in an offline gameplay scene during Play.");
            var live = UnityEngine.Object.FindAnyObjectByType<CoopSessionController>();
            Require(live != null && live.Phase == SessionPhase.Offline && !live.HasPeer, "Offline gameplay scene required.");
            var root = new GameObject("NetworkWriterCatalogCheck_Temporary");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            try
            {
                // 临时组件从不激活/初始化/订阅场景；只借现有只读引用来运行序列化方法。
                var session = root.AddComponent<CoopSessionController>();
                var capture = new Capture();
                session.Config = live.Config;
                Set(session, "_transport", capture); Set(session, "_hasPeer", true); Set(session, "_peer", (ulong)1);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);

                var players = Copy(root, live.GetComponent<NetworkPlayerSnapshotChannel>());
                players.Session = session;
                session.SendAuthority(NetworkMessageCatalog.Authority.PlayerSnapshot,
                    writer => Invoke(players, "Write", writer), false);
                Require(capture.Packets.Count == 1, "Real player writer was rejected.");

                var weapons = Copy(root, live.GetComponent<NetworkWeaponChannel>());
                weapons.Session = session;
                Invoke(weapons, "LateUpdate");
                Require(capture.Packets.Count == 2, "Real weapon-state writer was rejected.");

                var world = Copy(root, live.GetComponent<NetworkWorldSnapshotChannel>());
                world.Session = session;
                int expected = capture.Packets.Count, pools = 0;
                foreach (var pool in world.EnemyPools)
                {
                    var actor = pool.Instances.FirstOrDefault();
                    Require(actor != null, "Enemy pool has no initialized instance: " + pool.name);
                    Invoke(world, "Publish", actor); expected++; pools++;
                    Require(capture.Packets.Count == expected, "Enemy writer was rejected: " + pool.name);
                }
                var rice = world.Rice.Instances.FirstOrDefault();
                var bullet = world.EnemyBullets.Instances.FirstOrDefault();
                Require(rice != null && bullet != null, "Projectile pools have no initialized instance.");
                Invoke(world, "Publish", rice); Invoke(world, "Publish", bullet); expected += 2;
                Require(capture.Packets.Count == expected, "Projectile world writer was rejected.");
                foreach (byte[] packet in capture.Packets)
                    Require(NetworkMessageCatalog.TryValidatePacket(packet, NetworkMessageCatalog.Direction.AuthorityToPeer, out string reason), reason);
                return "PASS: " + capture.Packets.Count + " real writer packets (player pair, weapon state, " + pools +
                    " enemy pools, rice, enemy bullet) pass Session.Send preflight and catalog. Temporary inactive channels only; live gameplay state unchanged, no network connection.";
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static T Copy<T>(GameObject root, T source) where T : MonoBehaviour
        {
            Require(source != null, "Missing channel " + typeof(T).Name);
            var copy = root.AddComponent<T>();
            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (!field.IsInitOnly) field.SetValue(copy, field.GetValue(source));
            return copy;
        }
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
        private static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
