using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>双隔离场景、无真实连接/存档：实际写入和接收链，不用同一实例伪造两端。</summary>
    public static class ClaudeNetworkChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
        private static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private)?.Invoke(o, args);
        private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
        [MenuItem("DeepSleep/Diagnostics/Claude Network")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var original = SceneManager.GetActiveScene(); float scale = Time.timeScale;
            var hostScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var peerScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var host = Channel(hostScene); var peer = Channel(peerScene);
            int checks = 0;
            void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
            try
            {
                var transport = new CaptureTransport { IsServer = true };
                Initialize(host, transport); Initialize(peer, new CaptureTransport());
                Check(host.TryValidateConfiguration(out _) && peer.TryValidateConfiguration(out _), "Explicit two-scene assembly");
                var e = host.Encounter; var p = peer.Encounter;
                Check(e.Actor.BeginAuthority(host.Session, e.Perception), "Host body authority");
                e.Actor.Body.TryReceiveDamage(new DamagePacket(5000, Vector2.zero, Vector2.left, null));
                e.Actor.Body.CommitPhaseAtSkillBoundary(); e.Actor.SetPose(ClaudePose.Cut);
                e.Presentation.BeginEntrance(); e.Presentation.Advance(e.Presentation.Timing.EntranceSeconds, false);
                e.Presentation.Rain.Advance(1, false);
                host.ChapterDriver.ApplyReplica(true); e.ApplyReplicaState(ClaudeEncounterState.Casting);
                Check(e.SpatialCut.Begin(true, 318), "Host full screen cast"); e.SpatialCut.Simulate(3.55f);
                Check(e.Permissions.BeginBatch(true, 48), "Host permission batch");
                Check(e.Permissions.Books.All(book => book.State == ClaudeBookState.Sealed && book.RemainingSeconds == 10),
                    "Host batch immediately sealed before any advancement");
                e.Permissions.Advance(1.5f);
                e.Permissions.Books[0].TryReceiveDamage(new DamagePacket(1, Vector2.zero, Vector2.left, null));
                host.ChapterDriver.SceneEffects.ApplyBossClockReplica(true, true, 318, 250);
                byte[] Send(uint sequence)
                {
                    Set(host, "_frame", sequence);
                    host.Session.SendAuthority(NetworkMessageCatalog.Authority.ClaudeSnapshot, Get<Action<BinaryWriter>>(host, "_write"), true);
                    byte[] data = transport.Packets.Last();
                    Check(NetworkMessageCatalog.TryValidatePacket(data, NetworkMessageCatalog.Direction.AuthorityToPeer, out _), "Actual session writer passes catalog");
                    return data;
                }
                void Receive(byte[] data)
                {
                    Call(peer.Session, "Receive", (ulong)0, data);
                }
                var packet = Send(1);
                Check(packet.Length == ClaudeEncounterNetworkChannel.MaximumPayloadBytes + 1, "Three full 20-line banks fit exact maximum payload");
                Receive(packet);
                Check(peer.ChapterDriver.HasTakenOver && p.Actor.Body.CurrentHealth == 5000 && p.Actor.Body.PhaseTwo &&
                    !p.Actor.Body.CanReceiveDamage && !p.Actor.Body.Sphere.enabled, "Boss phase/health replicated without local collider");
                Check(p.Presentation.IsEntranceComplete && p.Presentation.NightOverlay.color.a == 1 &&
                    p.Presentation.FigureLayers[0].enabled && p.Presentation.Rain.Group.alpha == 1, "Rain/night/figure restored together");
                Check(p.Presentation.Barrier.EntranceAlpha == 1, "Shield entry alpha restored through actual session snapshot");
                Check(p.SpatialCut.CutsFired == 2 && p.SpatialCut.Warning.Renderer.enabled &&
                    p.SpatialCut.Warning.Filter.sharedMesh.vertexCount == 80 && p.SpatialCut.Flashes[1].Renderer.enabled,
                    "Batched warning and live flash mesh actually rendered");
                var a = new ClaudeEncounterSnapshot(); var b = new ClaudeEncounterSnapshot();
                e.SpatialCut.CaptureSnapshot(a); p.SpatialCut.CaptureSnapshot(b);
                for (int i = 0; i < 3; i++) for (int j = 0; j < a.CutCounts[i]; j++)
                    Check(a.CutLanes[i][j].Origin == b.CutLanes[i][j].Origin &&
                        Vector2.Distance(a.CutLanes[i][j].Direction, b.CutLanes[i][j].Direction) < .00001f, "Authoritative cut geometry, not peer randomness");
                for (int i = 0; i < 4; i++)
                {
                    var book = p.Permissions.Books[i]; var source = e.Permissions.Books[i];
                    Check(book.State == ClaudeBookState.Sealed && book.Role == source.Role && book.Permission == source.Permission &&
                        book.CurrentHealth == source.CurrentHealth && book.Visual.flipX == source.Visual.flipX &&
                        p.Permissions.Gates[(int)book.Role].IsBlocked(ClaudePermissionBook2D.BlockFor(book.Permission)) &&
                        !book.CanReceiveDamage && !book.Shape.enabled, "Each book identity, mirror, gate, health and authority boundary");
                }
                Check(peer.ChapterDriver.SceneEffects.BossClockElapsed == 250 &&
                    peer.ChapterDriver.SceneEffects.BossClockSeed == 318, "Late clock snapshot spans several schedule blocks");
                foreach (SceneBattleEffect effect in Enum.GetValues(typeof(SceneBattleEffect)))
                    Check(peer.ChapterDriver.SceneEffects.Count(effect) == host.ChapterDriver.SceneEffects.Count(effect) &&
                        peer.ChapterDriver.SceneEffects.Count(effect, true) == host.ChapterDriver.SceneEffects.Count(effect, true),
                        "Boss state clock uses authority seed/time");
                peer.Barrier.Advance(.08f); float restored = peer.Barrier.Alpha;
                Receive(Send(2)); Check(peer.Barrier.Alpha >= restored, "New snapshot of same hit cannot restart shield dimming");
                p.SpatialCut.Simulate(20); p.Permissions.Advance(20);
                Check(p.SpatialCut.CutsFired == 2 && p.Permissions.HasOpenBooks, "Replica cannot fire or locally expire books");
                Receive(packet); Check(Get<uint>(peer, "_lastReceived") == 2, "Duplicate rejected");
                for (int length = 1; length < packet.Length; length++)
                {
                    var truncated = new byte[length]; Array.Copy(packet, truncated, length); Receive(truncated);
                    Check(Get<uint>(peer, "_lastReceived") == 2 && p.Actor.Body.CurrentHealth == 5000, "Truncated frame cannot partially apply");
                }
                host.Capture(a); a.Sequence = 50; a.Health = 10001;
                byte[] Encode(ClaudeEncounterSnapshot f)
                {
                    using var s = new MemoryStream(); using var w = new BinaryWriter(s);
                    w.Write(NetworkMessageCatalog.Authority.ClaudeSnapshot); ClaudeEncounterNetworkChannel.WriteFrame(w, f); return s.ToArray();
                }
                Receive(Encode(a)); Check(Get<uint>(peer, "_lastReceived") == 2 && p.Permissions.HasOpenBooks, "Config-invalid health is atomic");
                a.Health = 5000; a.Books[3] = new ClaudeBookSnapshot(Vector2.zero, a.Books[0].Role, a.Books[0].Permission,
                    ClaudeBookState.Sealed, 100, 5, false);
                Receive(Encode(a)); Check(Get<uint>(peer, "_lastReceived") == 2, "Invalid final book cannot partially apply cut/body");
                var extra = packet.Concat(new byte[] { 0 }).ToArray(); Receive(extra);
                Check(Get<uint>(peer, "_lastReceived") == 2, "Trailing bytes rejected");
                e.SpatialCut.Cancel(); e.Permissions.Clear(); e.Actor.SetPose(ClaudePose.EnergyCharge);
                Check(e.Energy.Begin(true, new Vector2(-4, 1)), "Host energy begins"); e.Energy.Simulate(2.1f);
                Check(e.SecondaryEnergy.Begin(true,new Vector2(-5,-1)),"Independent second energy begins during first flight");
                e.SecondaryEnergy.Simulate(.5f);
                Receive(Send(3));
                Check(p.Energy.State == ClaudeEnergyState.Flying && p.Energy.Position == e.Energy.Position &&
                    p.Energy.CurrentHealth == 999 && p.Energy.Core.enabled && p.Energy.Halo.enabled &&
                    !p.Energy.Shape.enabled && !p.Energy.CanReceiveDamage, "Flying sphere restores both layers, not gameplay collision");
                Check(p.Energy.Core.transform.localRotation == e.Energy.Core.transform.localRotation &&
                    p.Energy.Halo.transform.localRotation == e.Energy.Halo.transform.localRotation, "Counter-rotation progress matches authority");
                p.Energy.Simulate(10); Check(p.Energy.State == ClaudeEnergyState.Flying, "Replica sphere cannot explode from local simulation");
                Check(p.SecondaryEnergy.State==ClaudeEnergyState.Charging && p.SecondaryEnergy.CurrentHealth==999 &&
                    p.SecondaryEnergy.Core.enabled && !p.SecondaryEnergy.Shape.enabled,"Second charging sphere independently replicated");
                p.SecondaryEnergy.Simulate(10);
                Check(p.SecondaryEnergy.State==ClaudeEnergyState.Charging,"Second replica cannot locally launch");
                e.Energy.TryReceiveDamage(new DamagePacket(999, e.Energy.Position, Vector2.left, null));
                e.Energy.Simulate(.2f); Receive(Send(4));
                Check(p.Energy.State == ClaudeEnergyState.Exploding && p.Energy.Dissipation.enabled && p.Energy.RadialBurst.enabled,
                    "Explosion/linger layers restored without duplicate damage");
                e.Energy.Cancel();
                e.SecondaryEnergy.Cancel();
                for (int i = 0; i < 2; i++) e.Actor.Targets[i].transform.position = new Vector2(-4, i * 2 - 1);
                Check(e.TrackingCut.Begin(true, 97), "Host local slash begins"); e.TrackingCut.Simulate(.9f);
                Receive(Send(5));
                Check(p.TrackingCut.State == ClaudeTrackingCutState.Locked && p.TrackingCut.Marker.enabled &&
                    p.TrackingCut.MarkerPosition == e.TrackingCut.MarkerPosition && p.TrackingCut.Lane.Direction == e.TrackingCut.Lane.Direction,
                    "Frozen local slash marker/diagonal restored");
                Check(p.TrackingCut.SecondaryMarker.enabled &&
                    p.TrackingCut.SecondaryTargetIndex == e.TrackingCut.SecondaryTargetIndex &&
                    p.TrackingCut.SecondaryMarkerPosition == e.TrackingCut.SecondaryMarkerPosition &&
                    p.TrackingCut.SecondaryLane.Direction == e.TrackingCut.SecondaryLane.Direction,
                    "Simultaneous second player lock is replicated");
                e.TrackingCut.Simulate(.35f); Receive(Send(6));
                Check(p.TrackingCut.ShotsFired == 1 && p.TrackingCut.Blades[0].enabled &&
                    p.TrackingCut.Blades[0].transform.position == e.TrackingCut.Blades[0].transform.position, "Local slash blade restored at authoritative spot");
                Check(p.TrackingCut.Blades[3].enabled &&
                    p.TrackingCut.Blades[3].transform.position == e.TrackingCut.Blades[3].transform.position,
                    "Second simultaneous slash restores its independent geometry");
                Check(p.TrackingCut.CrossBlades[0].enabled && p.TrackingCut.CrossBlades[3].enabled,
                    "Both players' crossing blades restored from phase-two geometry");
                p.TrackingCut.Simulate(20); Check(p.TrackingCut.ShotsFired == 1 && p.TrackingCut.DamageApplications == 0, "No client slash damage");
                Receive(packet); Check(Get<uint>(peer, "_lastReceived") == 6, "Old cast cannot resurrect old state");
                e.TrackingCut.Cancel(); e.Actor.Body.TryReceiveDamage(new DamagePacket(99999, Vector2.zero, Vector2.left, null));
                e.Presentation.BeginDeparture(); e.Presentation.Advance(1.5f, true);
                Time.timeScale = 0; Receive(Send(7));
                Check(p.Actor.Pose == ClaudePose.Defeated && p.Presentation.State == ClaudePresentationState.Departing &&
                    Mathf.Approximately(p.Presentation.NightOverlay.color.a, .5f) && Time.timeScale == 0, "Victory fade does not unpause client");
                Call(peer, "Clear");
                Check(!p.Actor.Body.IsShown && !p.Permissions.HasOpenBooks && !peer.ChapterDriver.HasTakenOver &&
                    !Get<bool>(peer, "_received") && p.Presentation.State == ClaudePresentationState.Hidden, "Close clears scene and sequence baseline");
                Receive(packet); Check(Get<uint>(peer, "_lastReceived") == 1 && p.Permissions.HasOpenBooks, "Reconnect restores full current snapshot");
                host.Capture(a); a.Sequence = uint.MaxValue; Receive(Encode(a)); // 先重置序号基线，测试回绕。
                Call(peer, "Clear"); Receive(Encode(a)); a.Sequence = 0; Receive(Encode(a));
                Check(Get<uint>(peer, "_lastReceived") == 0 && Get<bool>(peer, "_received"), "Unsigned sequence wrap accepted");
                var impactChannel = e.Actor.HitEffects.GetComponent<NetworkEffectEventChannel>();
                var peerEffect = p.Actor.HitEffects.GetComponent<NetworkEffectEventChannel>();
                Check(impactChannel.EffectId == 136 && peerEffect.EffectId == 136 && e.SpatialCut.HitEffects == impactChannel.Pool &&
                    e.Energy.HitEffects == impactChannel.Pool && e.TrackingCut.HitEffects == impactChannel.Pool,
                    "All Claude damage sources share explicit dedicated hit channel");
                Call(impactChannel, "OnEnable"); Call(peerEffect, "OnEnable");
                Check(e.Actor.HitEffects.TryPlay(Vector2.one, 47), "Authority hit pool plays event");
                var impact = transport.Packets.Last(); Receive(impact);
                Check(p.Actor.HitEffects.ActiveCount == 1, "Real effect event reaches peer pool through Session.Receive");
                Receive(impact); Check(p.Actor.HitEffects.ActiveCount == 1, "Hit event duplicate does not double-play");
                Check(peer.Session.Diagnostics.HandlerFaults == 0, "Actual session dispatcher has no handler faults");
                return $"Claude network: {checks} checks passed; two isolated scenes, actual Session.SendAuthority/Receive dispatch. Not device or natural chapter acceptance.";
            }
            finally
            {
                Cleanup(host); Cleanup(peer); EditorSceneManager.ClosePreviewScene(hostScene); EditorSceneManager.ClosePreviewScene(peerScene);
                Time.timeScale = scale; SceneManager.SetActiveScene(original);
            }
        }
        private static ClaudeEncounterNetworkChannel Channel(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<ClaudeEncounterNetworkChannel>(true)).Single();
        private static void Initialize(ClaudeEncounterNetworkChannel n, CaptureTransport transport)
        {
            Set(n.Session, "_transport", transport); Set(n.Session, "<Phase>k__BackingField", SessionPhase.Playing); Set(n.Session, "_hasPeer", true);
            var e = n.Encounter;
            Call(e.Actor.Body, "Awake"); Call(e.Actor.PoseTransition, "Awake"); Call(e.Actor.HitEffects, "Awake"); Call(e.Actor, "Awake");
            Call(n.Barrier, "OnEnable"); Call(e.Presentation, "Awake");
            var effects = n.ChapterDriver.SceneEffects; Call(effects, "Awake");
            Set(effects.Chapter, "_isInitialized", true); Set(effects.Chapter, "_phase", ChapterRunPhase.Combat);
            Set(effects.Chapter, "_remainingCombatSeconds", 999f);
            foreach (var v in new[] { e.SpatialCut.Warning }.Concat(e.SpatialCut.Flashes).Concat(e.SpatialCut.Fractures)) Call(v, "Awake");
            Call(e.Energy, "Awake"); Call(e.SecondaryEnergy, "Awake"); Call(e.SpatialCut, "Awake"); Call(e.TrackingCut, "Awake"); Call(e, "Awake");
            foreach (var book in e.Permissions.Books) { book.Clear(); Call(book.GetComponent<DamageHitbox2D>(), "Awake"); }
            foreach (var flash in n.BookFlashes) Call(flash, "OnEnable");
            foreach (var target in e.Actor.Targets)
            {
                Call(target.GetComponent<HealthComponent>(), "Awake"); Call(target.GetComponent<PlayerDamageReceiver2D>(), "Awake"); Call(target, "Awake");
                target.transform.position = new Vector2(-30, -20);
            }
            Call(n, "Awake");
            Call(n, "OnEnable");
        }
        private static void Cleanup(ClaudeEncounterNetworkChannel n)
        {
            Call(n, "Clear");
            (Get<IDisposable>(n.Session, "_sendWriter"))?.Dispose(); (Get<IDisposable>(n.Session, "_sendBuffer"))?.Dispose();
            Set(n.Session, "_sendWriter", null); Set(n.Session, "_sendBuffer", null); Set(n.Session, "_transport", null);
            foreach (var v in new[] { n.Encounter.SpatialCut.Warning }.Concat(n.Encounter.SpatialCut.Flashes).Concat(n.Encounter.SpatialCut.Fractures))
            { UnityEngine.Object.DestroyImmediate(v.Filter.sharedMesh); Set(v, "_mesh", null); }
        }
        private sealed class CaptureTransport : ITransportAdapter
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
