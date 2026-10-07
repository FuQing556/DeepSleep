using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Catalog = DeepSleep.Runtime.Networking.NetworkMessageCatalog;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>目录/格式检查可在 EditMode 运行；组合检查只建立临时隔离对象，不修改当前关卡。</summary>
    public static class NetworkProtocolChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("DeepSleep/Diagnostics/Check Network Protocol")]
        private static void Menu() => Debug.Log(RunCatalog() + "\n" + RunCombined());

        public static string RunCatalog()
        {
            Require(Catalog.TryValidateDefinitions(out string reason), reason);
            int symbols = CheckSymbols(typeof(Catalog.Authority), Catalog.Direction.AuthorityToPeer) +
                CheckSymbols(typeof(Catalog.Peer), Catalog.Direction.PeerToAuthority);
            Require(symbols == Catalog.Definitions.Count, "Every registered message must have one public catalog symbol");
            CheckSourceConstants();
            int checks = 0;
            foreach (var definition in Catalog.Definitions)
            {
                byte[] packet = Sample(definition);
                Require(Catalog.TryValidatePacket(packet, definition.Flow, out reason), definition.Name + ": " + reason);
                checks++;
                using (var stream = new MemoryStream(packet))
                using (var reader = new BinaryReader(stream))
                {
                    stream.Position = 1;
                    Require(Catalog.TryValidatePayload(definition.Id, definition.Flow, reader, out reason) &&
                        stream.Position == 1, "Preflight consumed valid payload: " + definition.Name);
                    checks++;
                }
                for (int length = 0; length < packet.Length; length++)
                {
                    Require(!Catalog.TryValidatePacket(packet.Take(length).ToArray(), definition.Flow, out _),
                        "Accepted truncated " + definition.Name + " at " + length);
                    checks++;
                }
                Require(!Catalog.TryValidatePacket(packet.Concat(new byte[] { 0 }).ToArray(), definition.Flow, out _),
                    "Accepted trailing byte: " + definition.Name); checks++;
                var opposite = definition.Flow == Catalog.Direction.AuthorityToPeer
                    ? Catalog.Direction.PeerToAuthority : Catalog.Direction.AuthorityToPeer;
                Require(!Catalog.TryValidatePacket(packet, opposite, out _), "Wrong direction: " + definition.Name); checks++;
            }

            // 变长格式用真实 beam codec，避免诊断与目录各自写出同一个错误长度。
            var lane = new BeamLaneSnapshot(0, Vector2.zero, Vector2.right, 10, .3f, 2, 1);
            var beam = new BeamFireSnapshot(3, Vector2.zero, Vector2.right, 1, new[] { lane, lane });
            byte[] laser = Packet(Catalog.Authority.LaserFire, w =>
            { w.Write((uint)1); Floats(w, 2); NetworkBeamSnapshotCodec.Write(w, beam); });
            Require(Catalog.TryValidatePacket(laser, Catalog.Direction.AuthorityToPeer, out reason), "Real beam codec: " + reason); checks++;
            foreach (int blocks in new[] { 0, 1, 240 })
            {
                byte[] packet = DoubaoPacket(4, blocks, false);
                Require(Catalog.TryValidatePacket(packet, Catalog.Direction.AuthorityToPeer, out reason), "Doubao variable payload: " + reason); checks++;
            }
            Catalog.TryGet(Catalog.Authority.WorldEntity, Catalog.Direction.AuthorityToPeer, out var entityDefinition);
            foreach (byte[] packet in new[] { Sample(entityDefinition), DoubaoPacket(4, 0, false) })
            {
                // 无气泡的豆包包在 age 后还包含 count；实体包以 age 结尾。
                int ageOffset = packet.Length - (packet[0] == Catalog.Authority.DoubaoSnapshot ? 5 : 4);
                foreach (float age in new[] { float.NaN, float.PositiveInfinity, -.01f, 1.01f })
                {
                    byte[] invalid = (byte[])packet.Clone();
                    Array.Copy(BitConverter.GetBytes(age), 0, invalid, ageOffset, 4);
                    Require(!Catalog.TryValidatePacket(invalid, Catalog.Direction.AuthorityToPeer, out _),
                        "Invalid monster flash age accepted: " + packet[0]); checks++;
                }
            }
            Require(!Catalog.TryValidatePacket(new byte[] { 255 }, Catalog.Direction.AuthorityToPeer, out _), "Unknown ID accepted"); checks++;
            Require(Catalog.Authority.RestNodeState != Catalog.Authority.DoubaoSnapshot, "Rest/Doubao ID collision"); checks++;
            Require(Catalog.Authority.CombatFeedback == Catalog.Peer.RestNodeReady, "Expected direction-scoped legacy ID"); checks++;
            checks += CheckWelcomeIdentity();
            checks += CheckCombatPresentation();
            checks += CheckUpgradeResultWire();
            return "PASS: " + symbols + " directional catalog entries, " + checks +
                " protocol assertions; complete symbols, no numeric send IDs, sizes/directions/truncation/trailing bytes, strict bounded UTF-8 level identity, real beam codec, dense Doubao payload. Protocol=" + Catalog.ProtocolVersion;
        }

        private static int CheckSymbols(Type owner, Catalog.Direction direction)
        {
            var ids = new HashSet<byte>();
            var fields = owner.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                Require(field.IsLiteral && field.FieldType == typeof(byte), "Message symbols must be byte constants");
                byte id = (byte)field.GetRawConstantValue();
                Require(ids.Add(id), "Duplicate constant in " + direction + ": " + field.Name);
                Require(Catalog.TryGet(id, direction, out var definition) && definition.Name == field.Name,
                    "Unregistered/aliased message constant: " + field.Name);
            }
            return fields.Length;
        }

        private static int CheckCombatPresentation()
        {
            int checks = 0;
            foreach (Catalog.CombatPresentationKind kind in Enum.GetValues(typeof(Catalog.CombatPresentationKind)))
            {
                byte[] packet = Packet(Catalog.Authority.CombatPresentation, w =>
                { w.Write((uint)4); w.Write((uint)9); w.Write((byte)kind); w.Write((byte)1); w.Write(2f); w.Write(-3f); });
                Require(Catalog.TryValidatePacket(packet, Catalog.Direction.AuthorityToPeer, out _), "Combat presentation kind rejected: " + kind);
                checks++;
                byte[] invalid = (byte[])packet.Clone(); invalid[9] = (byte)((byte)Catalog.CombatPresentationKind.GuardImpact + 1);
                Require(!Catalog.TryValidatePacket(invalid, Catalog.Direction.AuthorityToPeer, out _), "Unknown combat presentation kind accepted"); checks++;
                invalid = (byte[])packet.Clone(); invalid[10] = 2;
                Require(!Catalog.TryValidatePacket(invalid, Catalog.Direction.AuthorityToPeer, out _), "Unknown combat presentation role accepted"); checks++;
                foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    invalid = (byte[])packet.Clone(); Array.Copy(BitConverter.GetBytes(bad), 0, invalid, 11, 4);
                    Require(!Catalog.TryValidatePacket(invalid, Catalog.Direction.AuthorityToPeer, out _), "Nonfinite combat presentation position accepted"); checks++;
                }
            }
            return checks;
        }

        private static void CheckSourceConstants()
        {
            string root = Path.Combine(Application.dataPath, "_Project/Scripts/Runtime");
            var declaration = new Regex(@"\bconst\s+byte\s+[^;]+;", RegexOptions.Singleline);
            var numeric = new Regex(@"=\s*(0x[0-9a-fA-F]+|\d+)\b");
            var directSend = new Regex(@"\bSend(?:Authority|ToAuthority)\s*\(\s*(?:\(\s*byte\s*\)\s*)?(?:0x[0-9a-fA-F]+|\d+)\b");
            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == "NetworkMessageCatalog.cs") continue;
                string source = File.ReadAllText(path);
                Require(!directSend.IsMatch(source), "Numeric message send bypasses catalog: " + path);
                foreach (Match item in declaration.Matches(source))
                {
                    // 只审计已经声明为目录消息的一组常量；普通 byte 容量/遮罩不属于网络协议。
                    // 未登记新消息由 Send/Receive 的目录预检阻断，不猜测所有 byte 的用途。
                    if (!item.Value.Contains("NetworkMessageCatalog.Authority.") &&
                        !item.Value.Contains("NetworkMessageCatalog.Peer.")) continue;
                    Require(!numeric.IsMatch(item.Value), "Mixed catalog/numeric message declaration: " + path + " / " + item.Value);
                }
            }
        }

        private static int CheckUpgradeResultWire()
        {
            int checks = 0;
            foreach (byte role in new byte[] { 0, 1 })
            foreach (byte result in new byte[] { 0, 1, 2 })
            foreach (uint sequence in new uint[] { 1, uint.MaxValue })
            {
                byte[] packet = UpgradeResultPacket(7, sequence, role, result);
                Require(packet.Length == 11 && Catalog.TryValidatePacket(packet, Catalog.Direction.AuthorityToPeer, out _),
                    "Valid upgrade result rejected"); checks++;
            }
            foreach (byte[] packet in new[] { UpgradeResultPacket(-1, 1, 0, 0), UpgradeResultPacket(1, 0, 0, 0),
                UpgradeResultPacket(1, 1, 2, 0), UpgradeResultPacket(1, 1, 0, 3) })
            {
                Require(!Catalog.TryValidatePacket(packet, Catalog.Direction.AuthorityToPeer, out _), "Invalid upgrade result accepted"); checks++;
                using var stream = new MemoryStream(packet); stream.Position = 1;
                using var reader = new BinaryReader(stream);
                Require(!Catalog.TryValidatePayload(Catalog.Authority.UpgradeResult, Catalog.Direction.AuthorityToPeer, reader, out _) &&
                    stream.Position == 1, "Invalid upgrade result preflight consumed input"); checks++;
            }
            return checks;
        }

        private static byte[] Sample(Catalog.Definition definition)
        {
            if (definition.Flow == Catalog.Direction.PeerToAuthority)
                return Packet(definition.Id, w =>
                {
                    switch (definition.Id)
                    {
                        case Catalog.Peer.Headwear: w.Write((ushort)1); w.Write((ushort)0); w.Write((ushort)2); w.Write((ushort)0); break;
                        case Catalog.Peer.Ready: case Catalog.Peer.Control: case Catalog.Peer.RestNodeReady: w.Write(true); break;
                        case Catalog.Peer.Input: w.Write(new byte[29]); break;
                        case Catalog.Peer.UpgradeRequest: w.Write((byte)1); w.Write((byte)0); break;
                        default: throw new Exception("No peer fixture for " + definition.Name);
                    }
                });
            return Packet(definition.Id, w =>
            {
                switch (definition.Id)
                {
                    case Catalog.Authority.Headwear: w.Write((ushort)1); w.Write((ushort)0); w.Write((ushort)2); w.Write((ushort)0); break;
                    case Catalog.Authority.Welcome: w.Write((byte)0); w.Write((uint)1); w.Write(false); w.Write("World01"); break;
                    case Catalog.Authority.Start: break;
                    case Catalog.Authority.Room: w.Write(new byte[8]); break;
                    case Catalog.Authority.PlayerSnapshot:
                        w.Write((uint)1); w.Write((uint)0); WritePlayer(w, 0); WritePlayer(w, 1); break;
                    case Catalog.Authority.WorldEntity:
                        w.Write((uint)1); w.Write((uint)2); Floats(w, 3); w.Write(false); w.Write(0); w.Write(0); w.Write((byte)0);
                        w.Write((uint)0); w.Write(1f); break;
                    case Catalog.Authority.WorldDespawn: w.Write((uint)1); w.Write((uint)2); break;
                    case Catalog.Authority.Effect: w.Write((ushort)1); w.Write((uint)1); Floats(w, 3); break;
                    case Catalog.Authority.WeaponState:
                        w.Write((uint)1); w.Write((byte)0); Floats(w, 2); w.Write(false); Floats(w, 2);
                        w.Write(false); w.Write(false); w.Write((uint)1); w.Write((uint)1); Floats(w, 6);
                        w.Write(false); w.Write(false); w.Write(0); Floats(w, 2); break;
                    case Catalog.Authority.LaserFire:
                        w.Write((uint)1); Floats(w, 2);
                        NetworkBeamSnapshotCodec.Write(w, new BeamFireSnapshot(1, Vector2.zero, Vector2.right, 1,
                            new[] { new BeamLaneSnapshot(0, Vector2.zero, Vector2.right, 10, .3f, 2, 1) })); break;
                    case Catalog.Authority.MeleeWave: w.Write((uint)1); w.Write((uint)1); Floats(w, 3); w.Write((byte)0); Floats(w, 2); break;
                    case Catalog.Authority.GuardBlock: w.Write((uint)1); Floats(w, 5); w.Write(0); break;
                    case Catalog.Authority.RestNodeState: w.Write((byte)0); w.Write(false); w.Write(false); break;
                    case Catalog.Authority.CombatFeedback: w.Write((uint)1); w.Write((byte)1); Floats(w, 6); break;
                    case Catalog.Authority.UpgradeSnapshot: w.Write(new byte[40]); break;
                    case Catalog.Authority.UpgradeResult: w.Write(1); w.Write((uint)1); w.Write((byte)1); w.Write((byte)2); break;
                    case Catalog.Authority.TokenBalance: w.Write(0); w.Write(0); w.Write(0); w.Write(false); break;
                    case Catalog.Authority.ChapterState:
                        w.Write((byte)0); w.Write((byte)0); w.Write(0); w.Write(0f); w.Write(0);
                        Floats(w, 3); w.Write(false); w.Write(0); w.Write(0f); break;
                    case Catalog.Authority.PlayerHitFeedback: w.Write((uint)1); w.Write((byte)0); Floats(w, 3); break;
                    case Catalog.Authority.CombatPresentation:
                        w.Write((uint)1); w.Write((uint)1); w.Write((byte)Catalog.CombatPresentationKind.RiceVolley);
                        w.Write((byte)0); Floats(w, 2); break;
                    case Catalog.Authority.DoubaoSnapshot: WriteDoubao(w, 1, 0, false); break;
                    case Catalog.Authority.KimiSnapshot: w.Write(new byte[KimiEncounterNetworkChannel.PayloadBytes]); break;
                    default: throw new Exception("No authority fixture for " + definition.Name);
                }
            });
        }

        private static void WritePlayer(BinaryWriter w, byte role)
        {
            w.Write(role); Floats(w, 4); w.Write(false); w.Write((sbyte)1); w.Write((uint)0);
            Floats(w, 7); w.Write(true); w.Write(1f); Floats(w, 7);
            Floats(w, 2); w.Write(false); w.Write(false); Floats(w, 2); w.Write(false);
            w.Write((byte)0); w.Write(0f); w.Write(0); w.Write((byte)0); w.Write(0f);
        }

        private static int CheckWelcomeIdentity()
        {
            int checks = 0;
            foreach (string levelId in new[] { "a", "World01", "黄昏故都", "\U0001F307", new string('a', 127),
                new string('a', 128), new string('界', 42) + "ab", "\u007f\u0080\u07ff\u0800\ud7ff\ue000\uffff\U00010000\U0010ffff" })
            {
                CheckWelcome(WelcomeString(levelId), true); checks++;
            }
            foreach (string levelId in new[] { string.Empty, new string('a', 129), new string('界', 43) })
            {
                CheckWelcome(WelcomeString(levelId), false); checks++;
            }
            byte[][] invalidUtf8 = {
                new byte[] { 0x80 }, new byte[] { 0xc0, 0xaf }, new byte[] { 0xc1, 0xbf },
                new byte[] { 0xe0, 0x80, 0x80 }, new byte[] { 0xf0, 0x80, 0x80, 0x80 },
                new byte[] { 0xed, 0xa0, 0x80 }, new byte[] { 0xed, 0xbf, 0xbf },
                new byte[] { 0xf4, 0x90, 0x80, 0x80 }, new byte[] { 0xf5, 0x80, 0x80, 0x80 },
                new byte[] { 0xff }, new byte[] { 0xc2 }, new byte[] { 0xe1, 0x80 },
                new byte[] { 0xf0, 0x90, 0x80 }, new byte[] { 0xc2, 0x41 }, new byte[] { 0xe2, 0x28, 0xa1 }
            };
            foreach (byte[] bytes in invalidUtf8)
            {
                CheckWelcome(WelcomeRaw(w => { w.Write((byte)bytes.Length); w.Write(bytes); }), false); checks++;
            }
            foreach (byte[] encoded in new[] {
                new byte[] { 0x81, 0x00, 0x61 }, // 非最短长度前缀：1 不应该占两字节。
                new byte[] { 0x80, 0x81, 0x00 }, new byte[] { 0x80, 0x01, 0x61 },
                new byte[] { 0x80 }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0x0f },
                new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80 } })
            {
                CheckWelcome(WelcomeRaw(w => w.Write(encoded)), false); checks++;
            }
            byte[] maximum = WelcomeString(new string('a', 128));
            Require(maximum.Length == 137, "Maximum Welcome packet must include two-byte string prefix"); checks++;
            CheckWelcome(maximum.Take(maximum.Length - 1).ToArray(), false); checks++;
            return checks;
        }

        private static byte[] WelcomeString(string id) => WelcomeRaw(w => w.Write(id));
        private static byte[] WelcomeRaw(Action<BinaryWriter> write) => Packet(Catalog.Authority.Welcome, w =>
        { w.Write((byte)0); w.Write((uint)1); w.Write(false); write(w); });
        private static void CheckWelcome(byte[] packet, bool expected)
        {
            Require(Catalog.TryValidatePacket(packet, Catalog.Direction.AuthorityToPeer, out _) == expected,
                "Welcome level identity packet validation mismatch: " + BitConverter.ToString(packet));
            using var stream = new MemoryStream(packet);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            stream.Position = 1;
            Require(Catalog.TryValidatePayload(Catalog.Authority.Welcome, Catalog.Direction.AuthorityToPeer, reader, out _) == expected &&
                stream.Position == 1, "Welcome stream preflight disagreed or consumed input");
        }

        public static string RunCombined()
        {
            // 从不激活临时根节点：不会触发未装配组件的 Awake、网络连接、存档或关卡 Update。
            var root = new GameObject("ProtocolChecks_Isolated"); root.SetActive(false);
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            CoopSessionController session = null;
            try
            {
                session = root.AddComponent<CoopSessionController>();
                session.Config = config; config.MaximumMessageBytes = 16384; config.RemoteInterpolationSpeed = 30;
                Set(session, "_transport", new FakeTransport());
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                var node = root.AddComponent<RestNodePrototypeController2D>();
                Set(node, "_session", session); Set(node, "_isInitialized", true);
                // 节点现在只执行表现；隔离夹具也显式提供其表现依赖，不能靠空引用早退跳过转换。
                var cloud = new GameObject("ProtocolChecks_Cloud"); cloud.transform.SetParent(root.transform);
                Set(node, "_cloudLayerRoot", cloud.transform);
                Set(node, "_scrollingCloudLayer", cloud.AddComponent<DeepSleep.Runtime.World.Scrolling.LoopingBackgroundLayer2D>());
                Set(node, "_templeRenderer", root.AddComponent<SpriteRenderer>());
                Set(node, "_hotspots", Array.Empty<RestNodeHotspot2D>());
                var bossObject = new GameObject("ProtocolChecks_Boss"); bossObject.transform.SetParent(root.transform);
                var boss = bossObject.AddComponent<DoubaoBoss2D>();
                Set(boss, "_hitCollider", bossObject.AddComponent<BoxCollider2D>());
                Set(boss, "_renderer", bossObject.AddComponent<SpriteRenderer>());
                Set(boss, "_poseTransition", bossObject.AddComponent<SpritePoseTransition2D>());
                var channel = root.AddComponent<DoubaoEncounterNetworkChannel>();
                Set(channel, "_session", session); Set(channel, "_boss", boss); Set(channel, "_maximumViews", 2);
                Set(channel, "_bossHitFlash", bossObject.AddComponent<SpriteHitFlash2D>());
                var nodeReader = Reader(node, "ReadNetworkState"); var doubaoReader = Reader(channel, "Read");
                session.AuthorityMessage += nodeReader; session.AuthorityMessage += doubaoReader;
                int nodeChanges = 0, checks = 0;
                node.StateChanged += _ => nodeChanges++;
                for (uint frame = 256; frame <= 260; frame++)
                {
                    Receive(session, DoubaoPacket(frame, 0, false));
                    Require((uint)Get(channel, "_lastReceived") == frame && node.State == RestNodeState.Combat &&
                        nodeChanges == 0, "Doubao frame low byte changed node state: " + frame); checks++;
                }
                byte[] reveal = { Catalog.Authority.RestNodeState, (byte)RestNodeState.Revealing, 1, 0 };
                Receive(session, reveal);
                Require(node.State == RestNodeState.Revealing && nodeChanges == 1 &&
                    (bool)Get(node, "_deepSeekPortalReady") && !(bool)Get(node, "_harnessPortalReady") &&
                    (uint)Get(channel, "_lastReceived") == 260, "Node state was not isolated from Doubao"); checks++;
                Receive(session, reveal);
                Require(nodeChanges == 1, "Duplicate node snapshot re-entered the lifecycle"); checks++;

                byte[][] badNodes = {
                    new byte[] { Catalog.Authority.RestNodeState, 255, 0, 0 },
                    new byte[] { Catalog.Authority.RestNodeState, 2, 2, 0 },
                    new byte[] { Catalog.Authority.RestNodeState, 2, 0 },
                    reveal.Concat(new byte[] { 0 }).ToArray()
                };
                foreach (byte[] packet in badNodes)
                {
                    DispatchDirect(nodeReader, packet); Receive(session, packet);
                    Require(nodeChanges == 1 && node.State == RestNodeState.Revealing &&
                        (bool)Get(node, "_deepSeekPortalReady"), "Malformed node packet partially applied"); checks++;
                }
                byte[] oldCollision = DoubaoPacket(512, 0, false); oldCollision[0] = Catalog.Authority.RestNodeState;
                Receive(session, oldCollision);
                Require(nodeChanges == 1 && (uint)Get(channel, "_lastReceived") == 260, "Old ID 40 collision accepted"); checks++;

                byte[] valid = DoubaoPacket(1000, 0, false);
                byte[] nan = (byte[])valid.Clone(); Array.Copy(BitConverter.GetBytes(float.NaN), 0, nan, 7, 4);
                byte[] badEnum = (byte[])valid.Clone(); badEnum[5] = 255;
                byte[][] badDoubao = {
                    valid.Take(valid.Length - 1).ToArray(), valid.Concat(new byte[] { 0 }).ToArray(), nan, badEnum,
                    DoubaoPacket(1000, 2, true), DoubaoPacket(1000, 3, false),
                    Packet(Catalog.Authority.DoubaoSnapshot, w => WriteDoubao(w, 1000, 1, false, 0)),
                    Packet(Catalog.Authority.DoubaoSnapshot, w => WriteDoubao(w, 1000, 1, false, 1, -1f))
                };
                Vector3 before = boss.transform.position;
                foreach (byte[] packet in badDoubao)
                {
                    DispatchDirect(doubaoReader, packet); Receive(session, packet);
                    Require((uint)Get(channel, "_lastReceived") == 260 && boss.transform.position == before &&
                        !boss.IsActive && nodeChanges == 1, "Malformed Doubao packet partially applied"); checks++;
                }
                Receive(session, DoubaoPacket(261, 0, false));
                Require((uint)Get(channel, "_lastReceived") == 261, "Rejected high frame poisoned valid sequence"); checks++;
                Require(session.Diagnostics.HandlerFaults == 0, "A subscriber threw during combined dispatch"); checks++;
                return "PASS: " + checks + " isolated real Session.Receive + node/Doubao checks; ID 40/47 separation, frame low bytes 0..4, malformed lengths/enums/bools/NaN/duplicate and zero IDs/capacity, no partial boss/node/sequence mutation. No live network or scene state used.";
            }
            finally
            {
                // 防止临时 Session.OnDestroy 修改应用 runInBackground；没有启动过任何传输。
                if (session != null) Set(session, "_transport", null);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>隔离对象验证回执发送和接收门禁，不播放音效、联网或写档。</summary>
        public static string RunUpgradeResults()
        {
            var root = new GameObject("UpgradeResultChecks_Isolated"); root.SetActive(false);
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            CoopSessionController session = null;
            RestNodeUpgradeController upgrade = null;
            try
            {
                session = root.AddComponent<CoopSessionController>(); session.Config = config;
                config.MaximumMessageBytes = 16384;
                var transport = new FakeTransport(); Set(session, "_transport", transport);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                var assignment = root.AddComponent<PlayerControlAssignment>();
                Set(assignment, "<CurrentLocalPlayerRole>k__BackingField", PlayerRole.Harness);
                upgrade = root.AddComponent<RestNodeUpgradeController>();
                Set(upgrade, "_session", session); Set(upgrade, "_controlAssignment", assignment);
                Set(upgrade, "_isInitialized", true); Set(upgrade, "_nodeActive", true); Set(upgrade, "_nodeSerial", 7);
                Set(upgrade, "_wallet", root.AddComponent<TokenWallet>());
                Set(upgrade, "_runtimeState", root.AddComponent<PlayerUpgradeRuntimeState>());
                Set(upgrade, "_panel", root.AddComponent<RestNodeUpgradePanelView>());
                typeof(RestNodeUpgradeController).GetMethod("OnEnable", Private).Invoke(upgrade, null);
                var read = (UpgradeResultReader)typeof(RestNodeUpgradeController).GetMethod("TryReadResult", Private)
                    .CreateDelegate(typeof(UpgradeResultReader), upgrade);
                int checks = 0;
                bool Accept(byte[] packet, out AudioCue cue)
                {
                    using var stream = new MemoryStream(packet); stream.Position = 1;
                    using var reader = new BinaryReader(stream);
                    return read(reader, out cue);
                }
                void Check(bool condition, string message) { Require(condition, message); checks++; }
                Check(Accept(UpgradeResultPacket(7, 9, 1, 0), out AudioCue cue) && cue == AudioCue.UiReject, "Reject result mapping");
                Check(!Accept(UpgradeResultPacket(7, 9, 1, 2), out _), "Duplicate result played");
                Check(!Accept(UpgradeResultPacket(7, 8, 1, 2), out _), "Old result played");
                foreach (byte[] packet in new[] { UpgradeResultPacket(8, 100, 1, 2), UpgradeResultPacket(7, 100, 0, 2),
                    UpgradeResultPacket(7, 100, 1, 3), UpgradeResultPacket(7, 0, 1, 2),
                    UpgradeResultPacket(7, 100, 1, 2).Take(10).ToArray(),
                    UpgradeResultPacket(7, 100, 1, 2).Concat(new byte[] { 0 }).ToArray() })
                    Check(!Accept(packet, out _) && (uint)Get(upgrade, "_lastResultSequence") == 9, "Wrong/malformed result poisoned sequence");
                Check(Accept(UpgradeResultPacket(7, 10, 1, 1), out cue) && cue == AudioCue.Refresh, "Refresh result mapping");
                Set(upgrade, "_nodeActive", false);
                Check(!Accept(UpgradeResultPacket(7, 11, 1, 2), out _), "Result played outside node");
                Set(upgrade, "_nodeActive", true);
                Check(Accept(UpgradeResultPacket(7, 12, 1, 2), out cue) && cue == AudioCue.Upgrade, "Purchase result mapping");
                Set(session, "<Phase>k__BackingField", SessionPhase.Connecting);
                Check(!Accept(UpgradeResultPacket(7, 13, 1, 2), out _), "Result played while reconnecting");
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                Set(upgrade, "_lastResultSequence", uint.MaxValue);
                Check(Accept(UpgradeResultPacket(7, 1, 1, 2), out _), "Sequence wrap rejected");
                Check(!Accept(UpgradeResultPacket(7, uint.MaxValue, 1, 2), out _), "Pre-wrap old result accepted");

                // 真实订阅必须在重新入房重置；初始/重复快照不消耗或创建结果序号。
                ((Action<bool>)Get(session, "SessionOpened"))?.Invoke(false);
                Check(!(bool)Get(upgrade, "_receivedResult") && (uint)Get(upgrade, "_lastResultSequence") == 0, "Room reopen did not reset results");
                var snapshotReader = Reader(upgrade, "ReadAuthorityMessage");
                byte[] snapshot = Packet(Catalog.Authority.UpgradeSnapshot, w => { w.Write(7); w.Write(new byte[36]); });
                DispatchDirect(snapshotReader, snapshot); DispatchDirect(snapshotReader, snapshot);
                Check(!(bool)Get(upgrade, "_receivedResult") && (uint)Get(upgrade, "_lastResultSequence") == 0, "Initial/duplicate snapshot created a result");

                // 实际发送出口：远端角色可靠10字节回执，序号回绕跳过0。
                transport.IsServer = true; Set(session, "_hasPeer", true);
                Set(assignment, "<CurrentLocalPlayerRole>k__BackingField", PlayerRole.DeepSeek);
                Set(upgrade, "_resultSequence", uint.MaxValue);
                var play = typeof(RestNodeUpgradeController).GetMethod("PlayLocalResult", Private);
                byte expected = 0;
                foreach (AudioCue resultCue in new[] { AudioCue.UiReject, AudioCue.Refresh, AudioCue.Upgrade })
                {
                    play.Invoke(upgrade, new object[] { PlayerRole.Harness, resultCue });
                    byte[] packet = transport.LastPacket;
                    Check(transport.LastReliable && packet != null && packet.Length == 11 &&
                        Catalog.TryValidatePacket(packet, Catalog.Direction.AuthorityToPeer, out _), "Authority result wire/reliability");
                    using var stream = new MemoryStream(packet); using var reader = new BinaryReader(stream);
                    Check(reader.ReadByte() == Catalog.Authority.UpgradeResult && reader.ReadInt32() == 7 &&
                        reader.ReadUInt32() == (uint)(expected + 1) && reader.ReadByte() == 1 && reader.ReadByte() == expected,
                        "Authority result fields/sequence");
                    expected++;
                }
                Check(!Accept(UpgradeResultPacket(7, 20, 0, 2), out _), "Authority consumed its own result");
                ((Action)Get(session, "SessionClosed"))?.Invoke();
                Check((uint)Get(upgrade, "_resultSequence") == 0 && !(bool)Get(upgrade, "_receivedResult"), "Room close did not reset results");
                return $"PASS: {checks} isolated upgrade result checks; wire, node/role/phase gates, duplicate/old/wrap, snapshots silent and session reset. No audio played or files saved.";
            }
            finally
            {
                if (upgrade != null) typeof(RestNodeUpgradeController).GetMethod("OnDisable", Private).Invoke(upgrade, null);
                if (session != null)
                {
                    (Get(session, "_sendWriter") as IDisposable)?.Dispose();
                    (Get(session, "_sendBuffer") as IDisposable)?.Dispose();
                    Set(session, "_sendWriter", null); Set(session, "_sendBuffer", null); Set(session, "_transport", null);
                }
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private delegate bool UpgradeResultReader(BinaryReader reader, out AudioCue cue);
        private static byte[] UpgradeResultPacket(int node, uint sequence, byte role, byte result) =>
            Packet(Catalog.Authority.UpgradeResult, w => { w.Write(node); w.Write(sequence); w.Write(role); w.Write(result); });

        private sealed class FakeTransport : ITransportAdapter
        {
            public bool IsServer { get; set; }
            public byte[] LastPacket;
            public bool LastReliable;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => throw new InvalidOperationException("Isolated check cannot start transport");
            public bool StartClient(string address) => throw new InvalidOperationException("Isolated check cannot start transport");
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable)
            {
                if (!IsServer) throw new InvalidOperationException("Replica check must not send");
                LastPacket = payload; LastReliable = reliable;
            }
        }

        private static byte[] DoubaoPacket(uint frame, int count, bool duplicate) =>
            Packet(Catalog.Authority.DoubaoSnapshot, w => WriteDoubao(w, frame, count, duplicate));
        private static void WriteDoubao(BinaryWriter w, uint frame, int count, bool duplicate, uint firstId = 1, float size = 1.2f)
        {
            w.Write(frame); w.Write((byte)DoubaoEncounterState.Idle); w.Write(false);
            w.Write((float)frame); w.Write(4f); w.Write(0f); w.Write(false); w.Write(0f);
            w.Write((uint)0); w.Write(1f); w.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                w.Write(duplicate ? firstId : firstId + (uint)i); Floats(w, 2);
                w.Write(size); w.Write(size); w.Write("豆");
            }
        }
        private static byte[] Packet(byte id, Action<BinaryWriter> write)
        { using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); writer.Write(id); write(writer); return stream.ToArray(); }
        private static void Floats(BinaryWriter writer, int count) { for (int i = 0; i < count; i++) writer.Write(0f); }
        private static void Receive(CoopSessionController session, byte[] packet) =>
            typeof(CoopSessionController).GetMethod("Receive", Private).Invoke(session, new object[] { (ulong)0, packet });
        private static Action<byte, BinaryReader> Reader(object owner, string method) =>
            (Action<byte, BinaryReader>)owner.GetType().GetMethod(method, Private).CreateDelegate(typeof(Action<byte, BinaryReader>), owner);
        private static void DispatchDirect(Action<byte, BinaryReader> reader, byte[] packet)
        { using var stream = new MemoryStream(packet); using var input = new BinaryReader(stream); byte kind = input.ReadByte(); reader(kind, input); }
        private static object Get(object owner, string field) => owner.GetType().GetField(field, Private).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    }

    /// <summary>新增网络消息遗漏登记、方向重号或外部魔法编号必须在出包前阻断。</summary>
    public sealed class NetworkProtocolBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { NetworkProtocolChecks.RunCatalog(); }
            catch (Exception exception) { throw new BuildFailedException("Network protocol validation failed: " + exception.Message); }
        }
    }
}
