using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只建立隔离对象，真实网络读写器与闪光组件；不连接网络、不修改场景或玩法实体。</summary>
    public static class MonsterHitFlashNetworkChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const byte Entity = NetworkMessageCatalog.Authority.WorldEntity;
        private const byte Despawn = NetworkMessageCatalog.Authority.WorldDespawn;
        private const byte Doubao = NetworkMessageCatalog.Authority.DoubaoSnapshot;

        [MenuItem("DeepSleep/Diagnostics/Check Monster Hit Flash Network")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            var root = new GameObject("MonsterHitFlashNetworkChecks_Isolated");
            root.hideFlags = HideFlags.HideAndDontSave;
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            config.MaximumMessageBytes = 16384; config.RemoteInterpolationSpeed = 30;
            var catalog = ScriptableObject.CreateInstance<NetworkSpriteCatalog>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2);
            catalog.Entries = new[] { new NetworkSpriteCatalog.Entry { Id = 1, Sprite = sprite } };
            var material = new Material(Shader.Find("Sprites/Default"));
            CoopSessionController sender = null, receiver = null;
            int checks = 0;
            try
            {
                Require(catalog.Initialize(), "Sprite fixture catalog");
                var capture = new Capture { Server = true };
                sender = Session(root.transform, config, capture);
                receiver = Session(root.transform, config, new Capture());
                var a = View(root.transform, sprite, material);
                var b = View(root.transform, sprite, material);
                var world = Child(root.transform, "World").AddComponent<NetworkWorldSnapshotChannel>();
                world.Session = receiver; world.Catalog = catalog; world.ViewPrefab = a;
                world.ViewRoot = root.transform; world.MaximumViews = 2;
                var views = (IDictionary)Get(world, "_views"); views.Add((uint)1, a); views.Add((uint)2, b);
                var read = Method<Action<byte, BinaryReader>>(world, "Read");

                Dispatch(read, EntityPacket(1, 1, 10, 0, 1));
                Dispatch(read, EntityPacket(2, 2, 20, 0, 1));
                Require(a.HitFlash.PlayedCount == 0, "Baseline seq zero must not flash"); checks++;
                Dispatch(read, EntityPacket(1, 1, -10, 1, .1f));
                Require(a.HitFlash.PlayedCount == 1 && a.transform.position.x == 10 &&
                    (uint)Get(world, "_receivedFrame") == 2, "Late reliable hit lost or rewound another entity's newer frame"); checks++;
                Dispatch(read, EntityPacket(1, 1, -20, 1, .05f));
                Require(a.HitFlash.PlayedCount == 1 && Near(a.HitFlash.NormalizedAge, .1f), "Duplicate restarted or rewound age"); checks++;
                Dispatch(read, EntityPacket(3, 1, 30, 1, .8f));
                Require(Near(a.HitFlash.NormalizedAge, .8f), "Same hit must advance age"); checks++;
                Dispatch(read, EntityPacket(2, 1, -30, 2, 0));
                Dispatch(read, EntityPacket(1, 1, -40, 1, 0));
                Require(a.HitFlash.PlayedCount == 2 && a.transform.position.x == 30,
                    "Independent hit ordering or pose isolation failed"); checks++;

                byte[] valid = EntityPacket(1000, 1, -1000, 3, 0);
                var bad = new List<byte[]> {
                    EntityPacket(1000, 1, -1000, 3, float.NaN), EntityPacket(1000, 1, -1000, 3, -.01f),
                    EntityPacket(1000, 1, -1000, 3, 1.01f), EntityPacket(1000, 1, -1000, 3, 0, 4),
                    Resize(valid, valid.Length - 1), Resize(valid, valid.Length + 1)
                };
                foreach (byte[] packet in bad)
                {
                    Dispatch(read, packet);
                    Require(a.HitFlash.PlayedCount == 2 && a.transform.position.x == 30 && views.Count == 2 &&
                        (uint)Get(world, "_receivedFrame") == 3, "Invalid entity packet partially applied"); checks++;
                }
                Dispatch(read, Packet(Despawn, w => { w.Write((uint)4); w.Write((uint)1); }));
                Dispatch(read, EntityPacket(2, 1, -50, 100, 0));
                Require(!views.Contains((uint)1) && a.HitFlash.NormalizedAge == 1,
                    "Stale hit resurrected a dead ID or survived Clear"); checks++;
                Dispatch(read, EntityPacket(5, 3, 50, uint.MaxValue - 1, 0));
                uint beforeWrap = a.HitFlash.PlayedCount;
                Dispatch(read, EntityPacket(6, 3, 60, 1, 0));
                Dispatch(read, EntityPacket(7, 3, 70, uint.MaxValue, 0));
                Require(ReferenceEquals(views[(uint)3], a) && a.HitFlash.PlayedCount == beforeWrap + 1,
                    "View reuse or uint hit-sequence wrap failed"); checks++;
                Dispatch(read, EntityPacket(8, 3, 80, 1, 1));
                Dispatch(read, EntityPacket(8, 3, 80, 1, 0));
                Require(a.HitFlash.NormalizedAge == 1 && a.HitFlash.PlayedCount == beforeWrap + 1,
                    "Finished same-sequence replay lit again"); checks++;
                checks += CheckWriter(root.transform, sender, catalog, a, sprite, material, capture);
                checks += CheckDoubao(root.transform, receiver, b.HitFlash);
                Require(sender.Diagnostics.HandlerFaults == 0 && receiver.Diagnostics.HandlerFaults == 0,
                    "Unexpected session fault"); checks++;
                return "PASS: " + checks + " isolated monster-flash wire assertions; reliable new hits, full base layers/no overlay, " +
                    "cross-entity late hit without pose rewind, duplicate/old/wrap/reuse, invalid age/length/capacity atomic rejection, " +
                    "Doubao parsed-before-apply and reset. No live scene or transport modified.";
            }
            finally
            {
                if (sender != null) Set(sender, "_transport", null);
                if (receiver != null) Set(receiver, "_transport", null);
                Object.DestroyImmediate(root); Object.DestroyImmediate(config); Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }

        private static int CheckWriter(Transform root, CoopSessionController sender, NetworkSpriteCatalog catalog,
            NetworkEntityView template, Sprite sprite, Material material, Capture capture)
        {
            var world = Child(root, "Writer").AddComponent<NetworkWorldSnapshotChannel>();
            world.Session = sender; world.Catalog = catalog; world.ViewPrefab = template;
            var actor = Child(root, "AuthorityEnemy").AddComponent<EnemyActor2D>();
            var flash = actor.gameObject.AddComponent<SpriteHitFlash2D>();
            flash.Sources = Renderers(actor.transform, sprite, 3);
            flash.Overlay = Child(actor.transform, "Overlay").AddComponent<SpriteRenderer>();
            flash.Overlay.sharedMaterial = material;
            var publish = Method<Action<Component>>(world, "Publish");
            Set(world, "_frame", (uint)1); publish(actor);
            Require(capture.Packets.Count == 1 && !capture.Reliable[0], "Unhit entity changed reliability");
            byte[] first = capture.Packets[0];
            Require(first.Length == 1 + 38 + 59 * 3 && first[30] == 3,
                "Base muzzle/ghost layers missing or flash overlay serialized");
            Set(flash, "<Sequence>k__BackingField", (uint)7); Set(flash, "_elapsed", .02f);
            publish(actor); publish(actor);
            Require(capture.Reliable[1] && !capture.Reliable[2], "Only new hit sequence should be reliable");
            using (var reader = new BinaryReader(new MemoryStream(capture.Packets[1])))
            {
                reader.BaseStream.Position = reader.BaseStream.Length - 8;
                Require(reader.ReadUInt32() == 7 && Near(reader.ReadSingle(), flash.NormalizedAge), "Hit tail not authoritative");
            }
            Set(actor, "<SpawnGeneration>k__BackingField", (uint)1); publish(actor);
            Require(capture.Packets.Count == 5 && capture.Packets[3][0] == Despawn && capture.Reliable[3] &&
                capture.Packets[4][0] == Entity && capture.Reliable[4], "Pool generation did not send a fresh reliable hit baseline");
            var projectile = Child(root, "Projectile").AddComponent<SpriteRenderer>(); projectile.sprite = sprite;
            publish(projectile);
            byte[] last = capture.Packets[capture.Packets.Count - 1];
            using (var reader = new BinaryReader(new MemoryStream(last)))
            {
                reader.BaseStream.Position = reader.BaseStream.Length - 8;
                Require(reader.ReadUInt32() == 0 && reader.ReadSingle() == 1 && !capture.Reliable[capture.Reliable.Count - 1],
                    "Non-enemy projectile acquired monster flash");
            }
            foreach (byte[] packet in capture.Packets)
                Require(NetworkMessageCatalog.TryValidatePacket(packet, NetworkMessageCatalog.Direction.AuthorityToPeer, out string reason), reason);
            return 7;
        }

        private static int CheckDoubao(Transform root, CoopSessionController session, SpriteHitFlash2D flash)
        {
            flash.ResetFeedback();
            var bossObject = Child(root, "InactiveBoss");
            var boss = bossObject.AddComponent<DoubaoBoss2D>();
            Set(boss, "_hitCollider", bossObject.AddComponent<BoxCollider2D>());
            Set(boss, "_renderer", bossObject.AddComponent<SpriteRenderer>());
            Set(boss, "_poseTransition", bossObject.AddComponent<SpritePoseTransition2D>());
            var channel = Child(root, "DoubaoReader").AddComponent<DoubaoEncounterNetworkChannel>();
            Set(channel, "_session", session); Set(channel, "_boss", boss); Set(channel, "_bossHitFlash", flash);
            Set(channel, "_maximumViews", 2);
            var read = Method<Action<byte, BinaryReader>>(channel, "Read");
            uint baseline = flash.PlayedCount;
            Dispatch(read, BossPacket(1, 1, .2f));
            Require(flash.PlayedCount == baseline + 1 && Near(flash.NormalizedAge, .2f), "Boss hit not applied after full decode");
            int checks = 1;
            foreach (byte[] packet in new[] { BossPacket(1000, 2, float.NaN), BossPacket(1000, 2, -1),
                BossPacket(1000, 2, 2), BossPacket(1000, 2, 0, true) })
            {
                Dispatch(read, packet);
                Require(flash.PlayedCount == baseline + 1 && boss.transform.position.x == 1 &&
                    (uint)Get(channel, "_lastReceived") == 1, "Invalid boss tail/duplicate blocks partially applied"); checks++;
            }
            Dispatch(read, BossPacket(2, 1, .9f)); Dispatch(read, BossPacket(1, 3, 0));
            Require(flash.PlayedCount == baseline + 1 && Near(flash.NormalizedAge, .9f), "Old boss frame replayed hit"); checks++;
            Method<Action>(channel, "Clear")();
            Require(flash.NormalizedAge == 1, "Boss clear left flash alive"); checks++;
            return checks;
        }

        private static NetworkEntityView View(Transform root, Sprite sprite, Material material)
        {
            var go = Child(root, "Replica");
            var view = go.AddComponent<NetworkEntityView>(); view.Layers = Renderers(go.transform, sprite, 3);
            view.Group = go.AddComponent<SortingGroup>();
            view.HitFlash = go.AddComponent<SpriteHitFlash2D>(); view.HitFlash.ReplicaOnly = true;
            view.HitFlash.Sources = view.Layers;
            var overlay = Child(go.transform, "Overlay"); overlay.SetActive(true);
            view.HitFlash.Overlay = overlay.AddComponent<SpriteRenderer>(); view.HitFlash.Overlay.sharedMaterial = material;
            go.SetActive(true); Method<Action>(view, "Awake")();
            return view;
        }
        private static SpriteRenderer[] Renderers(Transform root, Sprite sprite, int count)
        {
            var result = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            { var go = Child(root, "Base" + i); go.SetActive(true); result[i] = go.AddComponent<SpriteRenderer>(); result[i].sprite = sprite; }
            return result;
        }
        private static CoopSessionController Session(Transform root, NetworkTuningConfig config, Capture transport)
        {
            var result = Child(root, "Session").AddComponent<CoopSessionController>(); result.Config = config;
            Set(result, "_transport", transport); Set(result, "_hasPeer", true); Set(result, "_peer", (ulong)1);
            Set(result, "<Phase>k__BackingField", SessionPhase.Playing); return result;
        }
        private static byte[] EntityPacket(uint frame, uint id, float x, uint sequence, float age, int layers = 3) => Packet(Entity, w =>
        {
            w.Write(frame); w.Write(id); Vector(w, x, 0, 0); w.Write(false); w.Write(0); w.Write(0); w.Write((byte)layers);
            for (int i = 0; i < layers; i++)
            {
                w.Write((uint)1); w.Write(true); Vector(w, x, 0, 0); Vector(w, 1, 1, 1); w.Write(0f);
                w.Write(1f); w.Write(1f); w.Write(1f); w.Write(1f); w.Write(0); w.Write(i); w.Write(false); w.Write(false);
            }
            w.Write(sequence); w.Write(age);
        });
        private static byte[] BossPacket(uint frame, uint sequence, float age, bool duplicate = false) => Packet(Doubao, w =>
        {
            w.Write(frame); w.Write((byte)DoubaoEncounterState.Idle); w.Write(false);
            w.Write((float)frame); w.Write(0f); w.Write(0f); w.Write(false); w.Write(0f);
            w.Write(sequence); w.Write(age); w.Write((byte)(duplicate ? 2 : 0));
            if (duplicate) for (int i = 0; i < 2; i++) { w.Write((uint)1); w.Write(0f); w.Write(0f); w.Write(1f); w.Write(1f); w.Write("豆"); }
        });
        private static byte[] Packet(byte kind, Action<BinaryWriter> write)
        { using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); writer.Write(kind); write(writer); return stream.ToArray(); }
        private static void Vector(BinaryWriter w, float x, float y, float z) { w.Write(x); w.Write(y); w.Write(z); }
        private static byte[] Resize(byte[] source, int length) { var result = new byte[length]; Array.Copy(source, result, Math.Min(length, source.Length)); return result; }
        private static void Dispatch(Action<byte, BinaryReader> read, byte[] packet)
        { using var reader = new BinaryReader(new MemoryStream(packet)); read(reader.ReadByte(), reader); }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .00001f;
        private static GameObject Child(Transform root, string name)
        { var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(root, false); return go; }
        private static T Method<T>(object owner, string method) where T : Delegate =>
            (T)owner.GetType().GetMethod(method, Hidden).CreateDelegate(typeof(T), owner);
        private static object Get(object owner, string field) => owner.GetType().GetField(field, Hidden).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
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
            public bool StartHost() => throw new InvalidOperationException("Isolated diagnostic");
            public bool StartClient(string address) => throw new InvalidOperationException("Isolated diagnostic");
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable) { Packets.Add(payload); Reliable.Add(reliable); }
        }
    }
}
