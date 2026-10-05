using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>隔离节点读目标/控制矩阵；不运行节点 Update、不执行准备离场、不移动真实模拟刻。</summary>
    public static class CompanionNodeGoalChecks
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Transport : ITransportAdapter
        {
            public bool Server = true;
            public bool IsServer => Server;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => true;
            public bool StartClient(string address) => true;
            public void Send(ulong peer, byte[] payload, bool reliable) => throw new Exception("Read-only node goal sent a network message");
            public void Stop() { }
        }

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play through Boot/MainMenu first.");
            var helpers = UnityEngine.Object.FindObjectsByType<CompanionNodeGoal2D>(FindObjectsSortMode.None);
            var ds = helpers.Single(x => x.Actor.Definition.Role == PlayerRole.DeepSeek);
            var hs = helpers.Single(x => x.Actor.Definition.Role == PlayerRole.Harness);
            Require(ds.TryValidateConfiguration(out _) && hs.TryValidateConfiguration(out _), "Incomplete goal assembly");
            Require(ds.isActiveAndEnabled && hs.isActiveAndEnabled && ds.Node == hs.Node && ds.Session == hs.Session,
                "Both enabled helpers must share one node/session");
            var node = ds.Node;
            var session = ds.Session;
            Require(session.Phase == SessionPhase.Offline && !session.HasPeer, "Offline scene required");
            var portals = ((BoxCollider2D[])Get(node, "_portalGoalShapes")).Where(x => x != null).ToArray();
            Require(portals.Length == 1, "This isolated test expects the scene's single exit portal");
            var portal = portals[0];
            var hotspot = portal.GetComponent<RestNodeHotspot2D>();
            Require(hotspot != null, "Missing portal hotspot");
            var transport = new Transport();
            string[] sessionFields = { "_transport", "<Phase>k__BackingField", "<HostRole>k__BackingField", "_hostControl", "_guestControl" };
            object[] sessionState = sessionFields.Select(x => Get(session, x)).ToArray();
            object nodeState = Get(node, "<State>k__BackingField");
            object dsReady = Get(node, "_deepSeekPortalReady"), hsReady = Get(node, "_harnessPortalReady");
            object dsCancel = Get(ds, "_cancelledRoles"), hsCancel = Get(hs, "_cancelledRoles");
            bool portalActive = hotspot.gameObject.activeSelf;
            Vector3 dsPosition = ds.Actor.transform.position, hsPosition = hs.Actor.transform.position;
            var dsBody = ds.Shape.attachedRigidbody;
            var hsBody = hs.Shape.attachedRigidbody;
            Vector2 dsVelocity = dsBody != null ? dsBody.linearVelocity : Vector2.zero;
            Vector2 hsVelocity = hsBody != null ? hsBody.linearVelocity : Vector2.zero;
            float scale = Time.timeScale;
            int checks = 0;
            try
            {
                Time.timeScale = 0f;
                Set(session, "_transport", transport);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                Set(session, "<HostRole>k__BackingField", PlayerRole.DeepSeek);
                Set(node, "<State>k__BackingField", RestNodeState.Open);
                hotspot.gameObject.SetActive(true);
                Vector2 center = portal.transform.TransformPoint(portal.offset);
                Vector2 away = center + Vector2.right * (portal.bounds.size.x + 4f);

                foreach (PlayerRole humanRole in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    Set(session, "_hostControl", humanRole == PlayerRole.DeepSeek ? SlotControl.Human : SlotControl.VoluntaryAi);
                    Set(session, "_guestControl", humanRole == PlayerRole.Harness ? SlotControl.Human : SlotControl.VoluntaryAi);
                    Set(node, "_deepSeekPortalReady", false); Set(node, "_harnessPortalReady", false);
                    Set(ds, "_cancelledRoles", (byte)0); Set(hs, "_cancelledRoles", (byte)0);
                    var human = humanRole == PlayerRole.DeepSeek ? ds : hs;
                    var ai = humanRole == PlayerRole.DeepSeek ? hs : ds;
                    Move(human, away); Move(ai, away + Vector2.up);
                    Require(!ai.TryGetGoal(out _, out _), "Rest opening alone must not force companion to leave"); checks++;
                    Move(human, center);
                    Require(ai.TryGetGoal(out Vector2 destination, out bool hold) && !hold,
                        "Human physically in portal must recruit companion"); checks++;
                    Require(!human.TryGetGoal(out _, out _), "Human actor must not receive an AI movement goal"); checks++;

                    Vector2 edge = portal.transform.TransformPoint(portal.offset + Vector2.right * (portal.size.x * .5f - .001f));
                    Move(ai, edge);
                    Require(node.TryReadPortalGoal(ai.Actor, ai.Shape, out var partial) && partial.IsInside && !partial.FullyInside,
                        "Root-only overlap must not count as full body entry");
                    Require(ai.TryGetGoal(out destination, out hold) && !hold, "Do not stop while body sticks out"); checks++;
                    Move(ai, destination);
                    Require(ai.TryGetGoal(out destination, out hold) && hold, "Whole body near center must hold in portal"); checks++;

                    string readyField = humanRole == PlayerRole.DeepSeek ? "_deepSeekPortalReady" : "_harnessPortalReady";
                    Set(node, readyField, true);
                    Require(ai.TryGetGoal(out _, out _), "Explicit ready intent"); checks++;
                    Set(node, readyField, false);
                    ((Delegate)Get(node, "PortalReadyCancelled"))?.DynamicInvoke(humanRole);
                    Require(!ai.TryGetGoal(out _, out _), "Explicit cancellation wins while still physically inside"); checks++;
                    ai.ResetIntent();
                    Require(!ai.TryGetGoal(out _, out _), "Control reset must not undo cancellation"); checks++;
                    Set(node, readyField, true);
                    Require(ai.TryGetGoal(out _, out _), "Ready again clears cancellation"); checks++;
                    Set(node, readyField, false);
                    ((Delegate)Get(node, "PortalReadyCancelled"))?.DynamicInvoke(humanRole);
                    Move(human, away);
                    Require(!ai.TryGetGoal(out _, out _), "Human exit removes goal"); checks++;
                    Move(human, center);
                    Require(ai.TryGetGoal(out _, out _), "New physical entry after cancellation can recruit again"); checks++;
                }

                Set(node, "_deepSeekPortalReady", false); Set(node, "_harnessPortalReady", false);
                Set(session, "_hostControl", SlotControl.VoluntaryAi);
                Set(session, "_guestControl", SlotControl.DisconnectedAi);
                Set(ds, "_cancelledRoles", (byte)0); Set(hs, "_cancelledRoles", (byte)0);
                Move(ds, away); Move(hs, away + Vector2.up);
                bool dsHasGoal = ds.TryGetGoal(out Vector2 dsGoal, out _);
                bool hsHasGoal = hs.TryGetGoal(out Vector2 hsGoal, out _);
                Require(dsHasGoal && hsHasGoal,
                    "Both AI slots must share a portal objective"); checks++;
                Require(!(bool)Get(node, "_deepSeekPortalReady") && !(bool)Get(node, "_harnessPortalReady"),
                    "Query must not automatically prepare either role"); checks++;
                Move(ds, dsGoal); Move(hs, hsGoal);
                Require(ds.TryGetGoal(out _, out bool dsHold) && hs.TryGetGoal(out _, out bool hsHold) && dsHold && hsHold,
                    "Both AI can hold inside the common portal"); checks++;
                transport.Server = false;
                Require(!ds.TryGetGoal(out _, out _) && !hs.TryGetGoal(out _, out _), "Client must not drive node movement"); checks++;
                transport.Server = true;
                Set(node, "<State>k__BackingField", RestNodeState.Combat);
                Require(!ds.TryGetGoal(out _, out _) && !hs.TryGetGoal(out _, out _), "Combat must not expose portal goal"); checks++;
                return "PASS: " + checks + " read-only portal-goal cases. Both human/AI role pairings, physical intent, full body entry, hold, cancel/re-ready/re-enter, control reset, both-AI/shared goal, no auto-ready, authority and node-state gates. Uses temporary role/node/pose state; does not prove movement simulation.";
            }
            finally
            {
                Move(ds, dsPosition); Move(hs, hsPosition);
                if (dsBody != null) dsBody.linearVelocity = dsVelocity;
                if (hsBody != null) hsBody.linearVelocity = hsVelocity;
                hotspot.gameObject.SetActive(portalActive);
                Set(node, "<State>k__BackingField", nodeState);
                Set(node, "_deepSeekPortalReady", dsReady); Set(node, "_harnessPortalReady", hsReady);
                Set(ds, "_cancelledRoles", dsCancel); Set(hs, "_cancelledRoles", hsCancel);
                for (int i = 0; i < sessionFields.Length; i++) Set(session, sessionFields[i], sessionState[i]);
                Time.timeScale = scale;
            }
        }

        private static void Move(CompanionNodeGoal2D helper, Vector2 position)
        {
            Vector3 old = helper.Actor.transform.position;
            helper.Actor.transform.position = new Vector3(position.x, position.y, old.z);
            if (helper.Shape.attachedRigidbody != null) helper.Shape.attachedRigidbody.position = position;
            Physics2D.SyncTransforms();
        }

        private static object Get(object owner, string name) => owner.GetType().GetField(name, PRIVATE).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, PRIVATE).SetValue(owner, value);
        private static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    }
}
