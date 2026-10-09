using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class KimiNetworkChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string Run()
        {
            if (!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only") == null)
                throw new InvalidOperationException("Use isolated Kimi Play preview, never a live match.");
            var encounter = UnityEngine.Object.FindAnyObjectByType<KimiEncounter2D>();
            var root = new GameObject("Kimi_Wire_Only"); root.SetActive(false);
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            var healthConfig = ScriptableObject.CreateInstance<HealthConfig>();
            var healthSo = new SerializedObject(healthConfig); healthSo.FindProperty("_maximumHealth").floatValue = 100; healthSo.ApplyModifiedPropertiesWithoutUndo();
            var targetRoot = new GameObject("Kimi_Wire_Probes"); targetRoot.SetActive(false);
            float oldTime = Time.timeScale; int checks = 0;
            void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
            try
            {
                Time.timeScale = 0;
                var session = root.AddComponent<CoopSessionController>(); session.Config = config;
                config.MaximumMessageBytes = 16384; config.RemoteInterpolationSpeed = 30;
                var transport = new NoNetworkTransport(); Set(session, "_transport", transport); Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                var driver = root.AddComponent<KimiChapterEncounterDriver2D>(); driver.Encounter = encounter; driver.Session = session;
                driver.Presentation = encounter.Boss.GetComponent<KimiBossPresentation2D>();
                driver.Backdrop = GameObject.Find("MoonNight").GetComponent<SpriteRenderer>();
                driver.NightBackdrop = driver.Backdrop.sprite; Set(driver, "_dayBackdrop", driver.Backdrop.sprite);
                driver.ApplyReplica(true);
                var channel = root.AddComponent<KimiEncounterNetworkChannel>(); channel.Session = session; channel.Encounter = encounter; channel.ChapterDriver = driver;
                channel.BossFlash = encounter.Boss.GetComponent<SpriteHitFlash2D>(); channel.CurtainFlash = encounter.Ultimate.Curtain.GetComponent<SpriteHitFlash2D>();
                Check(channel.TryValidateConfiguration(out _), "Explicit snapshot references");
                var targets = new DamageHitbox2D[2];
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject("Target" + i); go.transform.SetParent(targetRoot.transform); go.transform.position = new Vector2(-5, i * 2);
                    var health = go.AddComponent<HealthComponent>(); var so = new SerializedObject(health); so.FindProperty("_config").objectReferenceValue = healthConfig; so.ApplyModifiedPropertiesWithoutUndo();
                    targets[i] = go.AddComponent<DamageHitbox2D>(); so = new SerializedObject(targets[i]); so.FindProperty("_receiverComponent").objectReferenceValue = health; so.ApplyModifiedPropertiesWithoutUndo();
                }
                targetRoot.SetActive(true);
                byte[] Packet(uint sequence)
                {
                    Set(channel, "_frame", sequence);
                    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                    writer.Write(NetworkMessageCatalog.Authority.KimiSnapshot);
                    typeof(KimiEncounterNetworkChannel).GetMethod("Write", Private).Invoke(channel, new object[] { writer });
                    byte[] data = stream.ToArray();
                    Check(data.Length == KimiEncounterNetworkChannel.PayloadBytes + 1 && NetworkMessageCatalog.TryValidatePacket(data,
                        NetworkMessageCatalog.Direction.AuthorityToPeer, out _), "Actual writer passes catalog/size");
                    return data;
                }
                void Receive(byte[] data)
                {
                    using var stream = new MemoryStream(data); using var reader = new BinaryReader(stream); byte id = reader.ReadByte();
                    typeof(KimiEncounterNetworkChannel).GetMethod("Read", Private).Invoke(channel, new object[] { id, reader });
                }
                void Prepare()
                { encounter.Cancel(); encounter.Boss.BeginAuthority(new Vector2(6.5f, -.8f), null, null); driver.ApplyReplica(true); }
                void SafeReplica()
                {
                    Check(!encounter.Boss.CanReceiveDamage && !encounter.Boss.HitCollider.enabled && !encounter.Ultimate.Curtain.CanReceiveDamage &&
                        !encounter.Ultimate.Curtain.Shape.enabled, "Replica boss/curtain cannot receive damage");
                    foreach (var side in encounter.Prism.Sides) Check(!side.Shape.enabled && !side.CanReceiveDamage, "Mirror replica cannot collide/damage");
                    foreach (var orb in encounter.Prism.Orbs) Check(!orb.Active && !orb.Shape.enabled, "Orb replica never simulates/collides");
                    Check(encounter.Moon.Projectiles.ActiveCount == 0 && encounter.Ultimate.Blades.ActiveCount == 0 &&
                        encounter.Ultimate.ReinforcementPool.ActiveCount == 0, "Snapshot never rents gameplay entities");
                }
                Prepare(); Check(encounter.Moon.Begin(true, 7, null), "Moon begins"); encounter.Moon.Simulate(.1f);
                driver.Presentation.ApplyReplicaEntry(4.5f);
                var packet = Packet(1); int lane = encounter.Moon.GetWarningLane(0); bool left = encounter.Moon.IsWarningFromLeft(0);
                encounter.Cancel(); Receive(packet); SafeReplica();
                driver.Presentation.AdvancePresentation(0, 0);
                Check(driver.Presentation.EntryAge == 4.5f && encounter.Boss.Body.color.a == 1 &&
                    Mathf.Approximately(driver.Presentation.MoonShield.color.a, .8f), "Actual wire restores late-join figure/shield stage");
                Check(encounter.Moon.WarningCount == 4 && encounter.Moon.GetWarningLane(0) == lane && encounter.Moon.IsWarningFromLeft(0) == left &&
                    encounter.Moon.Warnings[3].enabled && encounter.Boss.IsShown && driver.HasTakenOver, "Four lanes/directions/boss/takeover restored");
                for (int n = 1; n < packet.Length; n++)
                { var shortPacket = new byte[n]; Array.Copy(packet, shortPacket, n); Receive(shortPacket); Check(Get<uint>(channel, "_lastReceived") == 1, "Truncation preserves receive watermark"); }
                Prepare(); Check(encounter.Prism.Begin(true, null, targets, null, null), "Prism begins");
                encounter.Prism.TryLaunch(encounter.Prism.Muzzle.position, Vector2.left); encounter.Prism.Simulate(.12f);
                var smash = new DamagePacket(2000, Vector2.zero, Vector2.left, null); encounter.Prism.Sides[0].TryReceiveDamage(in smash);
                packet = Packet(2); Vector2 orbPosition = encounter.Prism.Orbs[0].Position;
                encounter.Cancel(); Receive(packet); SafeReplica();
                Check(!encounter.Prism.Sides[0].Visual.enabled && encounter.Prism.Sides[1].Visual.enabled &&
                    encounter.Prism.Orbs[0].Visual.enabled && encounter.Prism.Orbs[0].Position == orbPosition, "Broken side/ball positions restored");
                Receive(packet); Check(Get<uint>(channel, "_lastReceived") == 2, "Duplicate ignored");
                var bad = (byte[])packet.Clone(); Array.Copy(BitConverter.GetBytes((uint)50), 0, bad, 1, 4);
                Array.Copy(BitConverter.GetBytes(10001f), 0, bad, 15, 4); Receive(bad);
                Check(Get<uint>(channel, "_lastReceived") == 2 && encounter.Boss.CurrentHealth == 10000, "Config-invalid high health applies nothing");
                bad = (byte[])packet.Clone(); Array.Copy(BitConverter.GetBytes((uint)50), 0, bad, 1, 4);
                Array.Copy(BitConverter.GetBytes(99f), 0, bad, bad.Length - 4, 4); Receive(bad);
                Check(Get<uint>(channel, "_lastReceived") == 2, "Config-invalid entry age rejected before applying frame");
                Prepare(); Check(encounter.Laser.Begin(new Vector2(-4, 1), null), "Laser begins"); encounter.Laser.Simulate(.4f);
                packet = Packet(3); var locked = encounter.Laser.Lane; encounter.Cancel(); Receive(packet); SafeReplica();
                Check(encounter.Laser.State == KimiLaserState.Charging && encounter.Laser.Lane.Origin == locked.Origin &&
                    Vector2.Distance(encounter.Laser.Lane.Direction, locked.Direction) < .0001f, "Frozen laser warning geometry roundtrip");
                encounter.Laser.Simulate(10); Check(encounter.Laser.State == KimiLaserState.Charging, "Replica cannot fire via Simulate");
                Check(encounter.Laser.TargetMarker.enabled && encounter.Laser.MarkerPosition == new Vector2(-4,1), "Target marker roundtrip");
                Prepare(); encounter.Boss.TryReceiveDamage(new DamagePacket(encounter.Boss.MaximumHealth*.5f,Vector2.zero,Vector2.left,null));
                encounter.Boss.CommitPhaseAtSkillBoundary();
                encounter.Laser.Begin(new Vector2(-4, 1), null); encounter.Laser.Simulate(encounter.Laser.Config.ChargeSeconds + .05f);
                packet = Packet(4); encounter.Cancel(); Receive(packet); Check(encounter.Laser.State == KimiLaserState.Firing, "Laser firing restored");
                Check(encounter.Laser.RayCount==5 && Enumerable.Range(0,5).All(i=>(i==0?encounter.Laser.Beam:encounter.Laser.BranchBeams[i-1]).GetComponent<MeshRenderer>().enabled == (encounter.Laser.Elapsed>=encounter.Laser.GetRayStart(i) && encounter.Laser.Elapsed<encounter.Laser.GetRayStart(i)+encounter.Laser.Config.FireSeconds)),"Phase2 replica uses authoritative staggered five-ray timing");
                Prepare(); Check(encounter.Ultimate.Begin(true, null, targets, null), "Ultimate begins");
                var hit = new DamagePacket(1, Vector2.zero, Vector2.left, null); encounter.Ultimate.Curtain.TryReceiveDamage(in hit);
                packet = Packet(5); encounter.Cancel(); Receive(packet); SafeReplica();
                Check(encounter.Ultimate.Curtain.Remaining == encounter.Ultimate.Config.PhaseTwoHits-1 && encounter.Ultimate.Curtain.Maximum == encounter.Ultimate.Config.PhaseTwoHits &&
                    encounter.Ultimate.Curtain.Visual.enabled && encounter.Boss.Pose == KimiPose.FluteCharge, "Phase-two curtain count/pose");
                transport.IsServer = true; var before = encounter.Boss.CurrentHealth; Receive(packet);
                Check(encounter.Boss.CurrentHealth == before && Get<uint>(channel, "_lastReceived") == 5, "Authority ignores replica messages"); transport.IsServer = false;
                typeof(KimiEncounterNetworkChannel).GetMethod("Clear", Private).Invoke(channel, null);
                Check(!encounter.Boss.IsShown && !driver.HasTakenOver && !Get<bool>(channel, "_received"), "Room close clears visual and watermark");
                Receive(packet); Check(Get<uint>(channel, "_lastReceived") == 5 && encounter.Ultimate.Curtain.Remaining == encounter.Ultimate.Config.PhaseTwoHits-1, "Fresh reconnect restores full current state");
                // 同一通用世界通道的实际采集/发送/接收：独立遭遇池不能漏于普通关卡敌人清单之外。
                Prepare();
                Check(encounter.Moon.Projectiles.TryRent(new Vector2(-5, 3), Vector2.right, encounter.Boss.gameObject, out _), "Moon entity seed");
                encounter.Ultimate.Begin(false, null, targets, null); encounter.Ultimate.Simulate(.02f);
                Check(encounter.Ultimate.Blades.TryRent(new Vector2(5, 0), Vector2.left, encounter.Boss.gameObject, out _), "Tidal entity seed");
                var world = root.AddComponent<NetworkWorldSnapshotChannel>(); world.Session = session;
                world.Catalog = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
                Check(world.Catalog.Initialize(), "Existing stable sprite catalog initializes");
                world.ViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab").GetComponent<NetworkEntityView>();
                world.ViewRoot = targetRoot.transform; world.MaximumViews = 32;
                world.Rice = root.AddComponent<DeepSleep.Runtime.Combat.Projectiles.RiceProjectilePool>();
                world.EnemyBullets = root.AddComponent<DeepSleep.Runtime.Combat.Projectiles.EnemyProjectilePool2D>();
                world.EncounterEnemyPools = new[] { encounter.Ultimate.ReinforcementPool };
                world.EncounterProjectilePools = new[] { encounter.Moon.Projectiles, encounter.Ultimate.Blades };
                Check(world.TryValidateConfiguration(out _), "Extra pools explicitly validated");
                transport.IsServer = true; Set(session, "_hasPeer", true); transport.Packets.Clear();
                typeof(NetworkWorldSnapshotChannel).GetMethod("LateUpdate", Private).Invoke(world, null);
                Check(transport.Packets.Count == 3, "World collector sends guard/moon/tidal exactly once");
                var entityPackets = transport.Packets.ToArray(); transport.IsServer = false;
                void ReceiveWorld(byte[] data)
                {
                    using var stream = new MemoryStream(data); using var reader = new BinaryReader(stream); byte id = reader.ReadByte();
                    typeof(NetworkWorldSnapshotChannel).GetMethod("Read", Private).Invoke(world, new object[] { id, reader });
                }
                foreach (var data in entityPackets) ReceiveWorld(data);
                Check(world.VisibleEntities == 3, "Three pure visual replicas created");
                foreach (var view in targetRoot.GetComponentsInChildren<NetworkEntityView>())
                {
                    Check(view.Layers.Any(l => l.enabled && l.sprite != null), "No invisible entity due to missing sprite ID");
                    Check(view.GetComponentsInChildren<Collider2D>().Length == 0, "Generic replicas collision-free");
                }
                encounter.Cancel(); transport.IsServer = true; transport.Packets.Clear(); Set(world, "_nextSend", 0f);
                typeof(NetworkWorldSnapshotChannel).GetMethod("LateUpdate", Private).Invoke(world, null); transport.IsServer = false;
                Check(transport.Packets.Count == 3 && transport.Packets.All(p => p[0] == NetworkMessageCatalog.Authority.WorldDespawn), "All extra pools emit despawn");
                foreach (var data in transport.Packets) ReceiveWorld(data);
                foreach (var data in entityPackets) ReceiveWorld(data);
                Check(world.VisibleEntities == 0, "Late old entity packets cannot resurrect cleared hazards");
                (Get<IDisposable>(session, "_sendWriter"))?.Dispose(); (Get<IDisposable>(session, "_sendBuffer"))?.Dispose();
                Set(session, "_sendWriter", null); Set(session, "_sendBuffer", null); Set(session, "_transport", null);
                string report = "PASS " + checks + " Kimi snapshot/entity assertions: actual " + KimiEncounterNetworkChannel.PayloadBytes + "-byte writer/reader, late-join presentation age and invalid-age rejection, four lane warnings, broken mirror/orb, locked warning/fire and target marker, phase2 five-ray fan, configured curtain hits, authority gate, all truncations, invalid frame atomicity, duplicate, close/reconnect. Existing world collector roundtrip shows360/moon/tidal with stable sprites; reliable despawn rejects late resurrection. Controlled in-process transport capture, not dual-device, full hit VFX/audio or natural gameplay acceptance.";
                File.WriteAllText(KimiMoonBladeChecks.Evidence + "/network_verification.txt", report); return report;
            }
            finally
            {
                encounter.Cancel(); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(targetRoot);
                UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(healthConfig); Time.timeScale = oldTime;
            }
        }
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
        private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
        private sealed class NoNetworkTransport : ITransportAdapter
        {
            public readonly List<byte[]> Packets = new();
            public bool IsServer { get; set; }
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => throw new InvalidOperationException();
            public bool StartClient(string address) => throw new InvalidOperationException();
            public void Stop() { }
            public void Send(ulong peer, byte[] payload, bool reliable) => Packets.Add(payload);
        }
    }
}
