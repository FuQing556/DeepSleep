using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>真实大脑命令 → 真实电机速度/边界 → 固定步数值积分；不执行物理、战斗按键或自动准备。</summary>
    public static class CompanionPortalMotorChecks
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Transport : ITransportAdapter
        {
            public bool IsServer => true;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => true;
            public bool StartClient(string address) => true;
            public void Send(ulong peer, byte[] payload, bool reliable) => throw new Exception("Motor diagnostic unexpectedly sent a network packet");
            public void Stop() { }
        }

        // 除标量意图外，也保存寻路/感知数组的内容；不把一次测试路径留给恢复后的真人或 AI。
        private sealed class Snapshot
        {
            private readonly object _owner;
            private readonly FieldInfo[] _fields;
            private readonly object[] _values;
            public Snapshot(object owner)
            {
                _owner = owner;
                _fields = owner.GetType().GetFields(PRIVATE | BindingFlags.DeclaredOnly)
                    .Where(f => !f.IsInitOnly || f.FieldType.IsArray).ToArray();
                _values = _fields.Select(f => f.GetValue(owner) is Array array ? array.Clone() : f.GetValue(owner)).ToArray();
            }
            public void Restore()
            {
                for (int i = 0; i < _fields.Length; i++)
                {
                    if (_fields[i].IsInitOnly && _values[i] is Array original)
                        Array.Copy(original, (Array)_fields[i].GetValue(_owner), original.Length);
                    else _fields[i].SetValue(_owner, _values[i]);
                }
            }
        }

        public static string Run(bool includeFormation = true)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play through Boot/MainMenu first.");
            var helpers = UnityEngine.Object.FindObjectsByType<CompanionNodeGoal2D>(FindObjectsSortMode.None);
            var ds = helpers.Single(x => x.Actor.Definition.Role == PlayerRole.DeepSeek);
            var hs = helpers.Single(x => x.Actor.Definition.Role == PlayerRole.Harness);
            var session = ds.Session;
            var node = ds.Node;
            var brains = new[] { session.DeepSeekAi, session.HarnessAi };
            Require(session.Phase == SessionPhase.Offline && !session.HasPeer, "Offline scene required");
            Require(brains.All(b => b != null && b.isActiveAndEnabled && b.NodeGoal != null &&
                b.NodeGoal.Actor.MovementMotor.isActiveAndEnabled && b.Shape.enabled), "Active brain/motor assembly required");
            var portal = ((BoxCollider2D[])Get(node, "_portalGoalShapes")).Single(x => x != null);
            var hotspot = portal.GetComponent<RestNodeHotspot2D>();
            var assignment = session.Assignment;
            string[] sessionFields = { "_transport", "<Phase>k__BackingField", "<HostRole>k__BackingField",
                "_hostControl", "_guestControl", "_soloAi" };
            object[] sessionValues = sessionFields.Select(x => Get(session, x)).ToArray();
            object roleBefore = Get(assignment, "<CurrentLocalPlayerRole>k__BackingField");
            object actorBefore = Get(assignment, "<CurrentLocalPlayerActor>k__BackingField");
            object[] commandSources = brains.Select(b => Get(b.NodeGoal.Actor.CommandDispatcher, "_commandSource")).ToArray();
            object nodeState = Get(node, "<State>k__BackingField");
            object dsReady = Get(node, "_deepSeekPortalReady"), hsReady = Get(node, "_harnessPortalReady");
            bool portalActive = hotspot.gameObject.activeSelf;
            Vector3[] oldPositions = brains.Select(b => b.Owner.position).ToArray();
            Vector2[] oldVelocities = brains.Select(b => b.Body.linearVelocity).ToArray();
            Vector2[] oldColliderOffsets = brains.Select(b => b.Shape.offset).ToArray();
            float[] oldBodyRotations = brains.Select(b => b.Body.rotation).ToArray();
            float[] oldAngularVelocities = brains.Select(b => b.Body.angularVelocity).ToArray();
            var transforms = brains.SelectMany(b => b.Owner.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            Vector3[] localPositions = transforms.Select(t => t.localPosition).ToArray();
            Quaternion[] localRotations = transforms.Select(t => t.localRotation).ToArray();
            Vector3[] localScales = transforms.Select(t => t.localScale).ToArray();
            float scale = Time.timeScale;
            var snapshots = new List<Snapshot>();
            foreach (var brain in brains)
            {
                snapshots.Add(new Snapshot(brain));
                snapshots.Add(new Snapshot(Get(brain, "_navigation")));
                snapshots.Add(new Snapshot(brain.Combat));
                snapshots.Add(new Snapshot(brain.Sensor));
                snapshots.Add(new Snapshot(brain.NodeGoal));
            }
            foreach (var anchor in brains.Select(b => b.SquadAnchor).Distinct()) snapshots.Add(new Snapshot(anchor));
            foreach (var registry in brains.Select(b => b.Sensor.ObstacleRegistry).Distinct()) snapshots.Add(new Snapshot(registry));
            foreach (var facing in brains.SelectMany(b => b.Owner.GetComponentsInChildren<PlayerFacingController2D>(true)))
                snapshots.Add(new Snapshot(facing));
            var metrics = new List<string>();
            uint tick = 100000;
            try
            {
                Time.timeScale = 0f;
                Set(session, "_transport", new Transport());
                Set(node, "<State>k__BackingField", RestNodeState.Open);
                hotspot.gameObject.SetActive(true);
                Physics2D.SyncTransforms();
                Vector2 center = portal.transform.TransformPoint(portal.offset);

                // 隔离静态门的接近控制；没有模拟敌人/气泡下落，不能将本检查当作战斗导航验收。
                foreach (var brain in brains)
                {
                    var emptyFilter = new ContactFilter2D { useTriggers = true };
                    emptyFilter.SetLayerMask(0);
                    Set(brain.Sensor, "_filter", emptyFilter);
                }
                foreach (var registry in brains.Select(b => b.Sensor.ObstacleRegistry).Distinct())
                { Set(registry, "_count", 0); Set(registry, "_capacityExceeded", false); }

                foreach (PlayerRole humanRole in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Prepare(session, node, brains, humanRole, false, false, false);
                        var ai = humanRole == PlayerRole.DeepSeek ? brains[1] : brains[0];
                        var human = humanRole == PlayerRole.DeepSeek ? brains[0] : brains[1];
                        Move(human, center); Move(ai, StartPosition(ai, center, side));
                        metrics.Add("solo " + ai.Combat.Role + (side < 0 ? " left " : " right ") +
                            Drive(new[] { ai }, ref tick));
                        CheckWithdrawals(ai, human, node, center, ref tick);
                    }
                }

                foreach (bool guestAi in new[] { true, false })
                {
                    foreach (PlayerRole humanRole in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    {
                        Prepare(session, node, brains, humanRole, true, guestAi, guestAi);
                        var ai = humanRole == PlayerRole.DeepSeek ? brains[1] : brains[0];
                        var human = humanRole == PlayerRole.DeepSeek ? brains[0] : brains[1];
                        Move(human, center); Move(ai, StartPosition(ai, center, guestAi ? -1 : 1));
                        metrics.Add((guestAi ? "disconnected guest " : "voluntary host ") + ai.Combat.Role + " " +
                            Drive(new[] { ai }, ref tick));
                        CheckWithdrawals(ai, human, node, center, ref tick);
                    }
                }

                Prepare(session, node, brains, PlayerRole.DeepSeek, true, true, false);
                Set(session, "_hostControl", SlotControl.VoluntaryAi);
                Set(session, "_guestControl", SlotControl.VoluntaryAi);
                Move(brains[0], StartPosition(brains[0], center, -1));
                Move(brains[1], StartPosition(brains[1], center, 1));
                metrics.Add("both online AI " + Drive(brains, ref tick));

                Prepare(session, node, brains, PlayerRole.DeepSeek, false, false, false);
                Set(session, "_soloAi", true);
                if (session.IsSoloPlaying)
                {
                    Move(brains[0], StartPosition(brains[0], center, -1));
                    Move(brains[1], StartPosition(brains[1], center, 1));
                    metrics.Add("solo voluntary + companion " + Drive(brains, ref tick));
                }
                else metrics.Add("solo voluntary case SKIPPED: current launch is not a completed solo selection");

                if (includeFormation)
                {
                    foreach (PlayerRole hostRole in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    {
                        Prepare(session, node, brains, hostRole, true, true, false);
                        Set(session, "_hostControl", SlotControl.VoluntaryAi);
                        Set(session, "_guestControl", SlotControl.VoluntaryAi);
                        Set(node, "<State>k__BackingField", RestNodeState.Combat);
                        Vector2 fieldCenter = brains[0].MotorConfig.MovementBounds.center;
                        Move(brains[0], fieldCenter + new Vector2(-3f, 1f));
                        Move(brains[1], fieldCenter + new Vector2(3f, -1f));
                        foreach (var anchor in brains.Select(b => b.SquadAnchor).Distinct()) Set(anchor, "_active", false);
                        metrics.Add("combat dual-AI host=" + hostRole + " " + DriveFormation(brains, ref tick));
                    }
                }

                Require(!(bool)Get(node, "_deepSeekPortalReady") && !(bool)Get(node, "_harnessPortalReady"),
                    "Movement-only integration changed portal readiness");
                for (int i = 0; i < brains.Length; i++)
                    Require(brains[i].Shape.offset == oldColliderOffsets[i] &&
                        Mathf.Approximately(brains[i].Body.rotation, oldBodyRotations[i]),
                        "Numerical movement diagnostic changed collider offset or body rotation");
                return "PASS: real brain + Motor.Simulate + numeric position integration (15s limit; >=1s stopped whole-body hold). " +
                    string.Join("; ", metrics) + ". Human exit/cancel removed portal plans on the next command. No Physics2D.Simulate, combat execution, node Update or auto-ready; threats isolated. Temporary state restored.";
            }
            finally
            {
                for (int i = 0; i < brains.Length; i++)
                {
                    Move(brains[i], oldPositions[i]);
                    brains[i].Body.linearVelocity = oldVelocities[i];
                    brains[i].Body.rotation = oldBodyRotations[i];
                    brains[i].Body.angularVelocity = oldAngularVelocities[i];
                    brains[i].Shape.offset = oldColliderOffsets[i];
                    Set(brains[i].NodeGoal.Actor.CommandDispatcher, "_commandSource", commandSources[i]);
                }
                hotspot.gameObject.SetActive(portalActive);
                Set(node, "<State>k__BackingField", nodeState);
                Set(node, "_deepSeekPortalReady", dsReady); Set(node, "_harnessPortalReady", hsReady);
                Set(assignment, "<CurrentLocalPlayerRole>k__BackingField", roleBefore);
                Set(assignment, "<CurrentLocalPlayerActor>k__BackingField", actorBefore);
                foreach (var snapshot in snapshots) snapshot.Restore();
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].localPosition = localPositions[i];
                    transforms[i].localRotation = localRotations[i];
                    transforms[i].localScale = localScales[i];
                }
                Physics2D.SyncTransforms();
                for (int i = 0; i < sessionFields.Length; i++) Set(session, sessionFields[i], sessionValues[i]);
                Time.timeScale = scale;
            }
        }

        private static void Prepare(CoopSessionController session, RestNodePrototypeController2D node,
            CompanionCommandSource2D[] brains, PlayerRole humanRole, bool online, bool guestAi, bool disconnected)
        {
            Set(session, "<Phase>k__BackingField", online ? SessionPhase.Playing : SessionPhase.Offline);
            Set(session, "_soloAi", false);
            Require(session.Assignment.TrySelectLocalPlayerRole(humanRole), "Local assignment setup");
            PlayerRole aiRole = humanRole == PlayerRole.DeepSeek ? PlayerRole.Harness : PlayerRole.DeepSeek;
            Set(session, "<HostRole>k__BackingField", guestAi ? humanRole : aiRole);
            Set(session, "_hostControl", guestAi ? SlotControl.Human : SlotControl.VoluntaryAi);
            Set(session, "_guestControl", guestAi ? (disconnected ? SlotControl.DisconnectedAi : SlotControl.VoluntaryAi) : SlotControl.Human);
            Set(node, "_deepSeekPortalReady", false); Set(node, "_harnessPortalReady", false);
            foreach (var brain in brains)
            {
                Set(brain.NodeGoal, "_cancelledRoles", (byte)0);
                brain.ResetIntent();
                brain.Body.linearVelocity = Vector2.zero;
            }
        }

        private static string Drive(CompanionCommandSource2D[] brains, ref uint tick)
        {
            float dt = Time.fixedDeltaTime;
            Require(dt > 0f && dt <= .05f, "Expected fixed timestep at most 50ms");
            int maxSteps = Mathf.CeilToInt(15f / dt);
            float[] firstHold = brains.Select(_ => -1f).ToArray();
            float[] stoppedHold = new float[brains.Length];
            for (int step = 0; step < maxSteps; step++)
            {
                tick++;
                for (int i = 0; i < brains.Length; i++)
                {
                    var brain = brains[i];
                    Require(brain.TryGetCommand(tick, out var command), "Brain rejected command");
                    brain.NodeGoal.Actor.MovementMotor.Simulate(command.Move, dt);
                    Move(brain, brain.Body.position + brain.Body.linearVelocity * dt);
                    bool hold = brain.NodeGoal.TryGetGoal(out _, out bool isHolding) && isHolding;
                    if (hold && firstHold[i] < 0f) firstHold[i] = (step + 1) * dt;
                    stoppedHold[i] = hold && brain.Plan == CompanionPlan.HoldPortal && brain.Body.linearVelocity.sqrMagnitude < .0025f
                        ? stoppedHold[i] + dt : 0f;
                }
                if (stoppedHold.All(t => t >= 1f))
                    return string.Join("/", brains.Select((brain, i) => brain.Combat.Role + " entry=" +
                        firstHold[i].ToString("F2") + "s, stable=" + stoppedHold[i].ToString("F2") + "s"));
            }
            throw new Exception("Portal approach/hold timeout: " + string.Join("; ", brains.Select((brain, i) =>
                brain.Combat.Role + " position=" + brain.Owner.position + " destination=" + brain.Destination +
                " waypoint=" + brain.Waypoint + " plan=" + brain.Plan + " velocity=" + brain.Body.linearVelocity +
                " firstHold=" + firstHold[i] + " stoppedHold=" + stoppedHold[i])));
        }

        private static string DriveFormation(CompanionCommandSource2D[] brains, ref uint tick)
        {
            float dt = Time.fixedDeltaTime;
            Vector2 initialAnchor = ((Vector2)brains[0].Owner.position + (Vector2)brains[1].Owner.position) * .5f;
            Vector2[] latePositions = new Vector2[brains.Length];
            bool capturedLate = false;
            float maximumSettledRadius = 0f, maximumLateTravel = 0f, maximumAnchorError = 0f;
            int steps = Mathf.CeilToInt(20f / dt);
            for (int step = 0; step < steps; step++)
            {
                tick++;
                for (int i = 0; i < brains.Length; i++)
                {
                    var brain = brains[i];
                    Require(brain.TryGetCommand(tick, out var command), "Formation command rejected");
                    brain.NodeGoal.Actor.MovementMotor.Simulate(command.Move, dt);
                    Move(brain, brain.Body.position + brain.Body.linearVelocity * dt);
                    Require((bool)Get(brain.SquadAnchor, "_active"), "Dual AI shared anchor was not captured");
                    float anchorError = Vector2.Distance((Vector2)Get(brain.SquadAnchor, "_anchor"), initialAnchor);
                    maximumAnchorError = Mathf.Max(maximumAnchorError, anchorError);
                    Require(anchorError <= .0001f, "Shared anchor drifted with moving teammates");
                    Require(brain.Plan != CompanionPlan.ApproachPortal && brain.Plan != CompanionPlan.HoldPortal,
                        "Combat formation retained a portal plan");
                    if ((step + 1) * dt >= 5f)
                        maximumSettledRadius = Mathf.Max(maximumSettledRadius,
                            Vector2.Distance(brain.Owner.position, initialAnchor));
                    if ((step + 1) * dt >= 15f)
                    {
                        if (!capturedLate) latePositions[i] = brain.Owner.position;
                        maximumLateTravel = Mathf.Max(maximumLateTravel,
                            Vector2.Distance(brain.Owner.position, latePositions[i]));
                    }
                }
                if ((step + 1) * dt >= 15f) capturedLate = true;
            }
            Require(maximumSettledRadius <= 2f,
                "Dual-AI formation escaped two-unit radius after settling: " + maximumSettledRadius);
            Require(maximumLateTravel <= .5f,
                "Dual-AI formation kept drifting in final five seconds: " + maximumLateTravel);
            return "20s anchorError=" + maximumAnchorError.ToString("F4") + "u, settledRadius=" +
                maximumSettledRadius.ToString("F3") + "u, last5sTravel=" + maximumLateTravel.ToString("F3") + "u";
        }

        private static void CheckWithdrawals(CompanionCommandSource2D ai, CompanionCommandSource2D human,
            RestNodePrototypeController2D node, Vector2 center, ref uint tick)
        {
            Move(human, StartPosition(human, center, -1));
            Require(ai.TryGetCommand(++tick, out _) && !ai.NodeGoal.TryGetGoal(out _, out _) &&
                ai.Plan != CompanionPlan.HoldPortal && ai.Plan != CompanionPlan.ApproachPortal, "Human departure left a stale portal plan");
            Move(human, center);
            Require(ai.TryGetCommand(++tick, out _), "Re-enter command");
            string readyField = human.Combat.Role == PlayerRole.DeepSeek ? "_deepSeekPortalReady" : "_harnessPortalReady";
            Set(node, readyField, true);
            Require(ai.TryGetCommand(++tick, out _), "Ready command");
            Set(node, readyField, false);
            ((Delegate)Get(node, "PortalReadyCancelled"))?.DynamicInvoke(human.Combat.Role);
            Require(ai.TryGetCommand(++tick, out _) && !ai.NodeGoal.TryGetGoal(out _, out _) &&
                ai.Plan != CompanionPlan.HoldPortal && ai.Plan != CompanionPlan.ApproachPortal, "Cancel left a stale portal plan");
        }

        private static Vector2 StartPosition(CompanionCommandSource2D brain, Vector2 center, int side)
        {
            Vector2 offset = (Vector2)brain.Shape.bounds.center - (Vector2)brain.Owner.position;
            return CompanionThreatMath.ClampPoint(center + new Vector2(side * 6f, -1.5f),
                brain.MotorConfig.MovementBounds, brain.Shape.bounds.extents) - offset;
        }

        private static void Move(CompanionCommandSource2D brain, Vector2 position)
        {
            Vector3 previous = brain.Owner.position;
            brain.Owner.position = new Vector3(position.x, position.y, previous.z);
            brain.Body.position = position;
            Physics2D.SyncTransforms();
        }

        private static object Get(object owner, string name) => owner.GetType().GetField(name, PRIVATE).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, PRIVATE).SetValue(owner, value);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
