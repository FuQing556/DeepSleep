using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class AutoBuyBuffChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        public static string RunShopRandomness()
        {
            if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>("Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            var distinct = new[] { new System.Collections.Generic.HashSet<string>(), new System.Collections.Generic.HashSet<string>() };
            var firstSlots = new[] { new System.Collections.Generic.Dictionary<UpgradeCardId, int>(), new System.Collections.Generic.Dictionary<UpgradeCardId, int>() };
            int checks = 0;
            void Check(bool ok, string message) { checks++; Require(ok, message); }
            UpgradeCardId[] Offers(Fixture f, PlayerRole role)
            {
                var state = Call(f.Controller, "GetOffer", role);
                return (UpgradeCardId[])((UpgradeCardId[])state.GetType().GetField("Offers").GetValue(state)).Clone();
            }
            for (int runSeed = 1; runSeed <= 96; runSeed++)
            {
                var root = new GameObject("ShopRandomness_Isolated"); root.SetActive(false);
                try
                {
                    var actual = Build(root, catalog, 20260910, 10000, runSeed);
                    var replay = Build(root, catalog, 20260910, 10000, runSeed);
                    root.SetActive(true); // 检查点恢复要求控制器启用；Editor不启动章节或存档服务。
                    var initial = new UpgradeCardId[2][];
                    foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    {
                        int index = (int)role; initial[index] = Offers(actual, role);
                        distinct[index].Add(string.Join(",", initial[index]));
                        firstSlots[index].TryGetValue(initial[index][0], out int count);
                        firstSlots[index][initial[index][0]] = count + 1;
                        Check(initial[index].SequenceEqual(Offers(replay, role)), "Same run seed replays exactly");
                        Check(initial[index].Distinct().Count() == 3 && initial[index].All(id => catalog.TryGet(id, out var d) && d.Supports(role)),
                            "Three distinct eligible cards");
                        Check(!initial[index].Contains(UpgradeCardId.RiceGuidance), "Guidance not offered before fan");
                        var state = Call(actual.Controller, "GetOffer", role);
                        Call(actual.Controller, "GenerateOffers", role, state);
                        Check(initial[index].SequenceEqual(Offers(actual, role)), "Same counters do not silently reroll");
                    }
                    actual.Controller.CaptureProgressCheckpoint();
                    foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    {
                        Call(actual.Controller, "ApplyRefresh", role); Call(replay.Controller, "ApplyRefresh", role);
                        Check(Offers(actual, role).SequenceEqual(Offers(replay, role)), "Same-seed explicit refresh replays");
                    }
                    Check(actual.Controller.RestoreProgressCheckpoint(true), "Restore checkpoint succeeds");
                    foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    {
                        Check(initial[(int)role].SequenceEqual(Offers(actual, role)), "Failure retry restores exact offers, not a new roll");
                        Call(actual.Controller, "ApplyRefresh", role);
                        Check(Offers(actual, role).SequenceEqual(Offers(replay, role)), "Restored counters preserve future sequence");
                        var id = Offers(actual, role)[0];
                        Check((bool)Call(actual.Controller, "TryPurchase", role, id) && (bool)Call(replay.Controller, "TryPurchase", role, id), "Purchase fixture");
                        Check(Offers(actual, role).SequenceEqual(Offers(replay, role)), "Purchase reroll replays deterministically");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            for (int role = 0; role < 2; role++)
            {
                Check(distinct[role].Count > 20, "Fresh run seeds must not repeat one fixed starting shop");
                var eligible = new System.Collections.Generic.List<UpgradeDefinition>(); catalog.GetEligible((PlayerRole)role, eligible);
                eligible.RemoveAll(d => d.Id == UpgradeCardId.RiceGuidance);
                Check(firstSlots[role].Count == eligible.Count && firstSlots[role].Values.All(n => n > 4 && n < 40),
                    "Every initially unlocked card appears in first slot across fixed seed sample");
            }
            return "Shop randomness: " + checks + " checks / 96 run seeds passed; distinct first offers DS/HS=" +
                distinct[0].Count + "/" + distinct[1].Count + "; isolated Editor, no device or save changes.";
        }

        public static string Run()
        {
            int checks = 0;
            int secondSlot = 0, thirdSlot = 0;
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>("Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            Require(catalog != null, "Catalog missing");
            for (int seed = 0; seed < 20; seed++)
            foreach (int balance in new[] { 0, 10, 100, 300, 1000, 10000 })
            {
                var root = new GameObject("AutoBuyChecks_Isolated"); root.SetActive(false);
                try
                {
                    var actual = Build(root, catalog, seed, balance);
                    var expected = Build(root, catalog, seed, balance);
                    actual.Controller.AutoBuyLocal();
                    foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    {
                        // 独立模拟手动依次点三槽：成功后从更新后的第一槽重新开始。
                        int purchases = 0;
                        bool bought;
                        do
                        {
                            bought = false;
                            var offer = Call(expected.Controller, "GetOffer", role);
                            var ids = (UpgradeCardId[])offer.GetType().GetField("Offers").GetValue(offer);
                            for (int slot = 0; slot < ids.Length; slot++)
                            {
                                if (!(bool)Call(expected.Controller, "TryPurchase", role, ids[slot])) continue;
                                if (slot == 1) secondSlot++;
                                if (slot == 2) thirdSlot++;
                                bought = true; purchases++; break;
                            }
                            Require(purchases <= catalog.Definitions.Sum(d => d.MaximumRank), "Loop exceeds rank cap");
                        } while (bought);
                        Require(actual.Wallet.GetBalance(role) == expected.Wallet.GetBalance(role), "Wallet differs from manual slot order"); checks++;
                        foreach (var definition in catalog.Definitions)
                        {
                            Require(actual.State.GetRank(role, definition.Id) == expected.State.GetRank(role, definition.Id), "Rank differs: " + definition.Id); checks++;
                        }
                    }
                    var before = actual.Wallet.CaptureSnapshot();
                    actual.Controller.AutoBuyLocal();
                    Require(actual.Wallet.DeepSeekBalance == before.DeepSeekBalance && actual.Wallet.HarnessBalance == before.HarnessBalance, "Repeated completed purchase spends again"); checks++;
                    Set(actual.Controller, "_nodeActive", false);
                    actual.Wallet.RestoreSnapshot(new TokenWalletSnapshot(300, 300, 0, true));
                    actual.Controller.AutoBuyLocal();
                    Require(actual.Wallet.DeepSeekBalance == 300 && actual.Wallet.HarnessBalance == 300, "Buying outside node"); checks++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Require(secondSlot > 0 && thirdSlot > 0, "Must exercise affordable second and third offers"); checks++;
            foreach (int length in new[] { 2, 3, 6, 9, 10, 11 })
            {
                byte[] packet = new byte[length + 1];
                packet[0] = NetworkMessageCatalog.Peer.UpgradeRequest; packet[1] = 3;
                Require(NetworkMessageCatalog.TryValidatePacket(packet, NetworkMessageCatalog.Direction.PeerToAuthority, out _) == (length == 10), "Auto-buy wire length"); checks++;
            }
            var networkRoot = new GameObject("AutoBuyNetworkChecks_Isolated"); networkRoot.SetActive(false);
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            var session = networkRoot.AddComponent<CoopSessionController>();
            try
            {
                var f = Build(networkRoot, catalog, 42, 300);
                session.Config = config;
                Set(session, "_transport", new Transport()); Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                Set(session, "<HostRole>k__BackingField", PlayerRole.DeepSeek);
                Set(f.Controller, "_session", session);
                Action<byte, int, int> request = (role, serial, purchases) =>
                {
                    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                    writer.Write((byte)3); writer.Write(role); writer.Write(serial); writer.Write(purchases); stream.Position = 0;
                    using var reader = new BinaryReader(stream);
                    Call(f.Controller, "ReadPeerMessage", NetworkMessageCatalog.Peer.UpgradeRequest, reader);
                };
                request(0, 1, 0); request(1, 0, 0); request(1, 1, 99);
                Require(f.Wallet.HarnessBalance == 300 && f.Wallet.DeepSeekBalance == 300, "Wrong role/stale serial/stale offers bought"); checks++;
                request(1, 1, 0);
                Require(f.Wallet.HarnessBalance < 300 && f.Wallet.DeepSeekBalance == 300, "Guest batch must spend only guest wallet"); checks++;
                int balance = f.Wallet.HarnessBalance;
                request(1, 1, 0);
                Require(f.Wallet.HarnessBalance == balance, "Duplicate request bought twice"); checks++;
            }
            finally { Set(session, "_transport", null); UnityEngine.Object.DestroyImmediate(networkRoot); UnityEngine.Object.DestroyImmediate(config); }
            return "AutoBuyBuffChecks passed: " + checks + " checks / 120 isolated balances and seeds; fallback slots 2/3=" + secondSlot + "/" + thirdSlot + ".";
        }

        private sealed class Fixture
        {
            public RestNodeUpgradeController Controller;
            public PlayerUpgradeRuntimeState State;
            public TokenWallet Wallet;
        }
        private sealed class Transport : ITransportAdapter
        {
            public bool IsServer => true;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => throw new InvalidOperationException();
            public bool StartClient(string address) => throw new InvalidOperationException();
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable) { }
        }
        private static Fixture Build(GameObject root, UpgradeCatalog catalog, int seed, int balance, int runSeed = 0)
        {
            var f = new Fixture { Controller = root.AddComponent<RestNodeUpgradeController>(), State = root.AddComponent<PlayerUpgradeRuntimeState>(), Wallet = root.AddComponent<TokenWallet>() };
            Set(f.State, "_catalog", catalog);
            Set(f.Controller, "_runtimeState", f.State); Set(f.Controller, "_wallet", f.Wallet);
            Set(f.Controller, "_panel", root.AddComponent<RestNodeUpgradePanelView>());
            Set(f.Controller, "_controlAssignment", root.AddComponent<PlayerControlAssignment>());
            Set(f.Controller, "_chapterSeed", seed); Set(f.Controller, "_isInitialized", true);
            f.Controller.InitializeRunSeed(runSeed);
            f.Wallet.RestoreSnapshot(new TokenWalletSnapshot(balance, balance, 0, true));
            f.Controller.BeginNode();
            return f;
        }
    }
}
