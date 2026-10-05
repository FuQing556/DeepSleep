using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 隔离同步微基准：真实 Session/World/Doubao 方法，固定负载、预热、按线程分配。
    /// 临时根从不激活，无真实 socket/场景读写；不是实际设备帧率或网络延迟测试。
    /// </summary>
    public static class NetworkHotPathBenchmark
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static byte[] _calibrationBytes;
        private static bool _allocationCounterSupported;

        [MenuItem("DeepSleep/Diagnostics/Benchmark Network Hot Paths")]
        private static void Menu() => UnityEngine.Debug.Log(Run());

        public static string Run(int iterations = 256, int warmup = 32)
        {
            Require(iterations >= 32 && iterations <= 4096 && warmup >= 8 && warmup <= 256, "Benchmark iteration range");
            string calibration = CalibrateAllocations();
            string countCalibration = EditorAllocationProbe.Calibrate();
            var root = new GameObject("NetworkHotPathBenchmark_Isolated");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            config.MaximumMessageBytes = 16384; config.SnapshotRate = 20; config.RemoteInterpolationSpeed = 30;
            var catalog = ScriptableObject.CreateInstance<NetworkSpriteCatalog>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 2f);
            catalog.Entries = new[] { new NetworkSpriteCatalog.Entry { Id = 1, Sprite = sprite } };
            Require(catalog.Initialize(), "Temporary sprite catalog");
            CoopSessionController sender = null, receiver = null;
            try
            {
                var transport = new Sink { Server = true };
                sender = MakeSession(root.transform, "Sender", config, transport);
                receiver = MakeSession(root.transform, "Receiver", config, new Sink());
                var report = new StringBuilder("Isolated networking hot-path benchmark\n");
                report.AppendLine(calibration);
                report.AppendLine(countCalibration);
                report.Append("iterations=").Append(iterations).Append(", warmup=").Append(warmup)
                    .AppendLine("; per operation; CPU and allocation-count runs measured separately; no real socket, live scene, Physics or rendered frame.");

                Action<BinaryWriter> feedbackWriter = WriteFeedback;
                Action sendFeedback = () => sender.SendAuthority(NetworkMessageCatalog.Authority.CombatFeedback, feedbackWriter, true);
                report.AppendLine(MeasureWriter("Send feedback (30 B packet)", null, sendFeedback, transport, iterations, warmup));
                byte[] feedback = transport.LastPacket;

                var world = Child(root.transform, "World").AddComponent<NetworkWorldSnapshotChannel>();
                world.Session = sender; world.Catalog = catalog; world.MaximumViews = 64; world.ViewRoot = root.transform;
                world.ViewPrefab = Child(root.transform, "WorldViewTemplate").AddComponent<NetworkEntityView>();
                world.ViewPrefab.Layers = new SpriteRenderer[1];
                var publish = Method<Action<Component>>(world, "Publish");
                var entities = new SpriteRenderer[64];
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = Child(root.transform, "Entity" + i);
                    entity.transform.position = new Vector3(i * .125f, i % 8, 0);
                    entities[i] = entity.AddComponent<SpriteRenderer>(); entities[i].sprite = sprite;
                }
                Set(world, "_frame", (uint)1);
                Action publishWorld = () => { for (int i = 0; i < entities.Length; i++) publish(entities[i]); };
                report.AppendLine(MeasureWriter("World.Publish x64 (one renderer/entity)", null, publishWorld, transport, iterations, warmup));

                var encounter = Child(root.transform, "Encounter").AddComponent<DoubaoWordWallEncounter2D>();
                var blocks = (List<DoubaoWordWallBlock2D>)Get(encounter, "_active");
                for (int i = 0; i < 240; i++)
                {
                    var block = Child(root.transform, "Bubble" + i).AddComponent<DoubaoWordWallBlock2D>();
                    block.transform.position = new Vector3(i % 24, i / 24, 0);
                    Set(block, "_replicationId", (uint)(i + 1)); Set(block, "_size", Vector2.one * 1.24f);
                    Set(block, "_phrase", "豆"); blocks.Add(block);
                }
                Set(encounter, "<State>k__BackingField", DoubaoEncounterState.Active);
                var boss = MakeBoss(root.transform);
                var bossFlash = boss.gameObject.AddComponent<SpriteHitFlash2D>();
                var outbound = Child(root.transform, "DoubaoSender").AddComponent<DoubaoEncounterNetworkChannel>();
                Set(outbound, "_session", sender); Set(outbound, "_encounter", encounter); Set(outbound, "_boss", boss);
                Set(outbound, "_bossHitFlash", bossFlash);
                var snapshot = Method<Action>(outbound, "WriteSnapshot");
                var frameField = outbound.GetType().GetField("_frame", Hidden);
                object zero = (uint)0;
                Action resetSendFrame = () => frameField.SetValue(outbound, zero);
                report.AppendLine(MeasureWriter("Doubao.WriteSnapshot x240 (5793 B packet)", resetSendFrame, snapshot, transport, iterations, warmup));
                byte[] dense = transport.LastPacket;

                var receive = Method<Action<ulong, byte[]>>(receiver, "Receive");
                var probes = new ReadProbe[8];
                for (int i = 0; i < probes.Length; i++)
                { probes[i] = new ReadProbe(); receiver.AuthorityMessage += probes[i].Read; }
                Action receiveFeedback = () => receive(0, feedback);
                report.AppendLine(Measure("Receive feedback + 8 observers", null, receiveFeedback, iterations, warmup));
                for (int i = 0; i < probes.Length; i++)
                {
                    Require(probes[i].Count == iterations * 2 + warmup, "Receive did not deliver exactly once to every observer");
                    receiver.AuthorityMessage -= probes[i].Read;
                }

                var replica = Child(root.transform, "DoubaoReceiver").AddComponent<DoubaoEncounterNetworkChannel>();
                Set(replica, "_session", receiver); Set(replica, "_boss", boss);
                Set(replica, "_bossHitFlash", bossFlash);
                Set(replica, "_maximumViews", 240); Set(replica, "_viewRoot", root.transform);
                Set(replica, "_viewPrefab", MakeBubbleView(root.transform, sprite));
                var readReplica = Method<Action<byte, BinaryReader>>(replica, "Read");
                receiver.AuthorityMessage += readReplica;
                for (int i = 0; i < 7; i++) receiver.AuthorityMessage += probes[i].Ignore;
                var receivedField = replica.GetType().GetField("_lastReceived", Hidden);
                Action resetReceiveFrame = () => receivedField.SetValue(replica, zero);
                Action receiveDense = () => receive(0, dense);
                report.AppendLine(Measure("Receive + Doubao.Read x240 + 7 observers", resetReceiveFrame, receiveDense, iterations, warmup));
                Require((uint)Get(replica, "_lastReceived") == 1 &&
                    ((System.Collections.IDictionary)Get(replica, "_views")).Count == 240,
                    "Dense replica workload did not process all 240 views");
                Require(sender.Diagnostics.HandlerFaults == 0 && receiver.Diagnostics.HandlerFaults == 0 &&
                    sender.Diagnostics.RejectedCount(NetworkMessageCatalog.Authority.WorldEntity) == 0 &&
                    sender.Diagnostics.RejectedCount(NetworkMessageCatalog.Authority.DoubaoSnapshot) == 0 &&
                    receiver.Diagnostics.RejectedCount(NetworkMessageCatalog.Authority.DoubaoSnapshot) == 0,
                    "Benchmark encountered invalid packets or handler errors");
                report.AppendLine("Wire checks: pre/post byte-for-byte + reliability equality; fingerprints below are comparable across code revisions.");
                report.AppendLine("Includes real catalog preflight. No reflection, packet capture, hashing, test resets or report formatting inside timed/allocated scope.");
                report.AppendLine("CPU pass has no active allocation recorder. Allocation-count pass uses Begin/End around only the operation; bytes remain UNAVAILABLE if the thread API fails calibration.");
                return report.ToString();
            }
            finally
            {
                // 未启动传输；防止临时 Session.OnDestroy 改动 Application.runInBackground。
                if (sender != null) Set(sender, "_transport", null);
                if (receiver != null) Set(receiver, "_transport", null);
                Object.DestroyImmediate(root); Object.DestroyImmediate(catalog); Object.DestroyImmediate(config);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Unity Mono 的此 API 可能固定返回零；先证明它能看到已保活的已知分配。</summary>
        public static string CalibrateAllocations()
        {
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            _calibrationBytes = new byte[4096];
            _calibrationBytes[4095] = 123;
            long measured = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(_calibrationBytes);
            _allocationCounterSupported = measured >= 4096;
            return "GC thread-counter calibration: retained new byte[4096], observed=" + measured +
                " B; " + (_allocationCounterSupported ? "SUPPORTED" : "UNAVAILABLE (zero is not a zero-allocation result)");
        }

        /// <summary>区分工作流构造开销与 Mono 的原始写入器实现，避免把每个 float 的分配误判成通道闭包。</summary>
        public static string RunWriterPrimitiveCalibration()
        {
            string calibration = EditorAllocationProbe.Calibrate() + "\n" + CalibrateAllocations();
            using var stream = new MemoryStream(4096);
            using var writer = new BinaryWriter(stream);
            Action reset = () => { stream.SetLength(0); stream.Position = 0; };
            return calibration + "\n" +
                Measure("Preallocated BinaryWriter.Write(uint)", reset, () => writer.Write((uint)123), 256, 32) + "\n" +
                Measure("Preallocated BinaryWriter.Write(float)", reset, () => writer.Write(1.25f), 256, 32) + "\n" +
                Measure("Preallocated BinaryWriter.Write(string)", reset, () => writer.Write("豆"), 256, 32);
        }

        private static string MeasureWriter(string name, Action prepare, Action operation, Sink sink, int iterations, int warmup)
        {
            // 冷缓存初始化包含在首次抓包，不纳入预热后的基准。
            sink.Capture = new List<WirePacket>(); prepare?.Invoke(); operation();
            var before = sink.Capture; sink.Capture = null;
            string result = Measure(name, prepare, operation, iterations, warmup);
            sink.Capture = new List<WirePacket>(); prepare?.Invoke(); operation();
            var after = sink.Capture; sink.Capture = null;
            Require(before.Count == after.Count && before.Count > 0, "Writer packet count changed: " + name);
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            for (int i = 0; i < before.Count; i++)
            {
                var a = before[i]; var b = after[i];
                Require(a.Reliable == b.Reliable && a.Bytes.Length == b.Bytes.Length, "Writer wire metadata changed: " + name);
                for (int j = 0; j < a.Bytes.Length; j++) Require(a.Bytes[j] == b.Bytes[j], "Writer bytes changed: " + name);
                Require(NetworkMessageCatalog.TryValidatePacket(a.Bytes, NetworkMessageCatalog.Direction.AuthorityToPeer, out string error), error);
                writer.Write(a.Reliable); writer.Write(a.Bytes.Length); writer.Write(a.Bytes);
            }
            using var hash = SHA256.Create();
            string fingerprint = BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", string.Empty).Substring(0, 16);
            return result + "; packets/op=" + before.Count + "; wireSHA256=" + fingerprint;
        }

        private static string Measure(string name, Action prepare, Action operation, int iterations, int warmup)
        {
            for (int i = 0; i < warmup; i++) { prepare?.Invoke(); operation(); }
            // 两个度量 API 本身预热；临时数组在分配窗口外。
            GC.GetAllocatedBytesForCurrentThread(); Stopwatch.GetTimestamp();
            var durations = new double[iterations]; long bytes = 0; double total = 0;
            int collections = GC.CollectionCount(0);
            for (int i = 0; i < iterations; i++)
            {
                prepare?.Invoke();
                long beforeBytes = _allocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() : 0;
                long started = Stopwatch.GetTimestamp();
                operation();
                long elapsed = Stopwatch.GetTimestamp() - started;
                if (_allocationCounterSupported) bytes += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                durations[i] = elapsed * 1000000d / Stopwatch.Frequency; total += durations[i];
            }
            collections = GC.CollectionCount(0) - collections;
            Array.Sort(durations);
            long allocationCount = 0;
            using (var probe = new EditorAllocationProbe())
            {
                for (int i = 0; i < iterations; i++)
                {
                    prepare?.Invoke();
                    probe.Begin(); operation(); EditorAllocationProbe.Result sample = probe.End();
                    Require(sample.IsValid, "GC.Alloc probe overflow/unavailable in " + name);
                    allocationCount += sample.Count;
                }
            }
            return name + ": bytes/op=" + (_allocationCounterSupported ? (bytes / (double)iterations).ToString("F1") : "UNAVAILABLE") +
                ", gcAllocs/op=" + (allocationCount / (double)iterations).ToString("F2") +
                ", meanUs=" + (total / iterations).ToString("F2") +
                ", p95Us=" + durations[(int)Math.Ceiling(iterations * .95) - 1].ToString("F2") +
                ", maxUs=" + durations[iterations - 1].ToString("F2") + ", gen0=" + collections;
        }

        private readonly struct WirePacket
        {
            public WirePacket(byte[] bytes, bool reliable) { Bytes = bytes; Reliable = reliable; }
            public byte[] Bytes { get; }
            public bool Reliable { get; }
        }
        private sealed class Sink : ITransportAdapter
        {
            public bool Server;
            public List<WirePacket> Capture;
            public byte[] LastPacket;
            public bool IsServer => Server;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => throw new InvalidOperationException("Benchmark cannot start a socket");
            public bool StartClient(string address) => throw new InvalidOperationException("Benchmark cannot start a socket");
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable)
            { LastPacket = payload; Capture?.Add(new WirePacket(payload, reliable)); }
        }
        private sealed class ReadProbe
        {
            public int Count;
            public void Read(byte kind, BinaryReader reader)
            {
                if (kind != NetworkMessageCatalog.Authority.CombatFeedback) return;
                Require(reader.BaseStream.Position == 1, "Dispatch cursor must restart at payload");
                Require(reader.ReadUInt32() == 1, "Dispatch observer read corrupted payload"); Count++;
            }
            public void Ignore(byte kind, BinaryReader reader) { }
        }
        private static CoopSessionController MakeSession(Transform root, string name, NetworkTuningConfig config, Sink transport)
        {
            var session = Child(root, name).AddComponent<CoopSessionController>(); session.Config = config;
            Set(session, "_transport", transport); Set(session, "_hasPeer", true); Set(session, "_peer", (ulong)1);
            Set(session, "<Phase>k__BackingField", SessionPhase.Playing); return session;
        }
        private static DoubaoBoss2D MakeBoss(Transform root)
        {
            var go = Child(root, "Boss"); var boss = go.AddComponent<DoubaoBoss2D>();
            Set(boss, "_hitCollider", go.AddComponent<BoxCollider2D>());
            Set(boss, "_renderer", go.AddComponent<SpriteRenderer>());
            Set(boss, "_poseTransition", go.AddComponent<SpritePoseTransition2D>()); return boss;
        }
        private static DoubaoWordWallReplicaView2D MakeBubbleView(Transform root, Sprite sprite)
        {
            var go = Child(root, "BubbleViewTemplate"); var view = go.AddComponent<DoubaoWordWallReplicaView2D>();
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            Set(view, "_renderer", renderer); Set(view, "_label", Child(go.transform, "Text").AddComponent<TextMesh>());
            return view;
        }
        private static void WriteFeedback(BinaryWriter writer)
        {
            writer.Write((uint)1); writer.Write((byte)1);
            writer.Write(1f); writer.Write(2f); writer.Write(1f); writer.Write(0f); writer.Write(7f); writer.Write(.2f);
        }
        private static GameObject Child(Transform parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static T Method<T>(object owner, string name) where T : Delegate =>
            (T)owner.GetType().GetMethod(name, Hidden).CreateDelegate(typeof(T), owner);
        private static object Get(object owner, string field) => owner.GetType().GetField(field, Hidden).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    }
}
