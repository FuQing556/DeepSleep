using System;
using System.Reflection;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 独立临时几何的登记表检查；不推进全场物理、不改现役会话或运行场景中的对象。
    /// 会话仅在始终 inactive 的根下建立，避免未装配会话触发 Awake。
    /// </summary>
    public static class CompanionObstacleRegistryChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class Transport : ITransportAdapter
        {
            public bool Server;
            public bool IsServer => Server;
            public bool IsConnected => true;
            public string DisconnectReason => string.Empty;
            public event Action<ulong> Connected { add { } remove { } }
            public event Action<ulong> Disconnected { add { } remove { } }
            public event Action<ulong, byte[]> Received { add { } remove { } }
            public bool StartHost() => true;
            public bool StartClient(string address) => true;
            public void Send(ulong peer, byte[] payload, bool reliable) { }
            public void Stop() { }
        }

        public static string Run()
        {
            var root = new GameObject("CompanionObstacleRegistryChecks")
                { hideFlags = HideFlags.HideAndDontSave };
            CoopSessionController session = null;
            int checks = 0;
            try
            {
                var registry = root.AddComponent<CompanionObstacleRegistry2D>();
                var first = CreateCircle(root.transform, "First", new Vector2(2, 1), out var body, out var source);
                var second = CreateCircle(root.transform, "Second", new Vector2(4, 1), out _, out _);
                first.offset = new Vector2(.2f, -.1f);
                first.transform.localScale = Vector3.one * 1.5f;
                Physics2D.SyncTransforms();

                Vector2 velocity = new Vector2(0, -.85f);
                Require(registry.Register(first, velocity, source), "First registration", ref checks);
                Require(registry.Register(second, velocity, second.GetComponent<Light>()), "Second registration", ref checks);
                var snapshots = new CompanionObstacleSnapshot[2];
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out bool saturated) == 2 && !saturated,
                    "An exactly full destination must not report truncation", ref checks);
                Require((snapshots[0].Center - (Vector2)first.bounds.center).sqrMagnitude < .000001f &&
                    Mathf.Abs(snapshots[0].Radius - .6f) < .0001f && snapshots[0].Velocity == velocity,
                    "Snapshot must use actual transformed collider center/radius and supplied velocity", ref checks);

                Require(registry.Register(first, Vector2.left, source) && registry.RegisteredCount == 2,
                    "Duplicate registration must update, not duplicate", ref checks);
                registry.CopyVisible(Vector2.zero, 10, snapshots, out _);
                Require(snapshots[0].Velocity == Vector2.left, "Duplicate updates velocity", ref checks);
                Require(registry.CopyVisible(Vector2.zero, 10, new CompanionObstacleSnapshot[1], out saturated) == 1 && saturated,
                    "Small destination reports truncation", ref checks);
                Require(registry.CopyVisible(Vector2.zero, 10, Array.Empty<CompanionObstacleSnapshot>(), out saturated) == 0 && saturated,
                    "Empty destination with visible hazards reports truncation", ref checks);
                Require(registry.CopyVisible(new Vector2(100, 100), 1, snapshots, out saturated) == 0 && !saturated,
                    "Out-of-range circles excluded", ref checks);
                Vector2 edge = (Vector2)first.bounds.center + Vector2.left * .8f;
                Require(registry.CopyVisible(edge, .21f, snapshots, out saturated) == 1 && !saturated,
                    "Observation includes radius at the range boundary", ref checks);

                source.enabled = false;
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out _) == 1,
                    "Disabled source excluded", ref checks);
                source.enabled = true;
                first.enabled = false;
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out _) == 1,
                    "Disabled collider excluded", ref checks);
                first.enabled = true;
                first.gameObject.SetActive(false);
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out _) == 1,
                    "Inactive pooled object excluded", ref checks);
                first.gameObject.SetActive(true);
                body.simulated = false;
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out _) == 1,
                    "Non-simulated body excluded", ref checks);
                body.simulated = true;
                Physics2D.SyncTransforms();
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out saturated) == 2 && !saturated,
                    "Re-enabled pooled geometry observable again", ref checks);
                registry.enabled = false;
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out saturated) == 0 && saturated,
                    "Disabled perception service fails closed", ref checks);
                registry.enabled = true;

                var sessionObject = new GameObject("InactiveSessionFixture");
                sessionObject.SetActive(false);
                sessionObject.transform.SetParent(root.transform, false);
                session = sessionObject.AddComponent<CoopSessionController>();
                var transport = new Transport();
                Set(session, "_transport", transport);
                registry.Session = session;
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out saturated) == 2 && !saturated,
                    "Offline session may observe", ref checks);
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out saturated) == 0 && !saturated,
                    "Client session cannot observe authority hazards", ref checks);
                Require(!registry.Register(first, Vector2.right, source),
                    "Client cannot register or update hazards", ref checks);
                transport.Server = true;
                Require(registry.CopyVisible(Vector2.zero, 10, snapshots, out saturated) == 2 && !saturated &&
                    snapshots[0].Velocity == Vector2.left,
                    "Authority sees hazards; rejected client update did not change velocity", ref checks);

                registry.Unregister(first);
                registry.Unregister(first);
                Require(registry.RegisteredCount == 1 &&
                    registry.CopyVisible(Vector2.zero, 10, snapshots, out _) == 1,
                    "Unregister is idempotent and retains other hazards", ref checks);
                registry.Unregister(second);
                Require(registry.RegisteredCount == 0 &&
                    registry.CopyVisible(Vector2.zero, 10, snapshots, out saturated) == 0 && !saturated,
                    "Final pool return clears visible registry", ref checks);
                return "PASS: " + checks + " obstacle registry checks; world geometry, pooling, disabled states, buffer truncation and offline/authority/client gates. Isolated fixtures, no gameplay simulation.";
            }
            finally
            {
                // 此 fixture 没有执行过 Awake；不让销毁回调触及全局 runInBackground。
                if (session != null) Set(session, "_transport", null);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static CircleCollider2D CreateCircle(Transform parent, string name, Vector2 position,
            out Rigidbody2D body, out Light source)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = .4f;
            collider.isTrigger = true;
            // 无场景依赖或 Awake 副作用的 Behaviour，仅作为 source.enabled 测试替身。
            source = go.AddComponent<Light>();
            source.intensity = 0;
            return collider;
        }

        private static void Set(object owner, string field, object value) =>
            owner.GetType().GetField(field, Private).SetValue(owner, value);

        private static void Require(bool passed, string message, ref int checks)
        {
            if (!passed) throw new InvalidOperationException(message);
            checks++;
        }
    }
}
