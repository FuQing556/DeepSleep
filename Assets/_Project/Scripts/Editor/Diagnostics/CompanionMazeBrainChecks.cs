using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 连续真实气泡 + Brain/电机的隔离移动诊断。玩家不参与物理接触，
    /// 仅计数连续扫掠不安全区间；不等价于实际战斗、扣血或双端验收。
    /// </summary>
    public static class CompanionMazeBrainChecks
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static string Run(bool measurePerformance = false, bool measureAllocations = false)
        {
            if (!EditorApplication.isPlaying || Time.timeScale != 0f ||
                SceneManager.GetActiveScene().name != "World01_EarlyInternet")
                throw new InvalidOperationException("Run in a fresh World01 Play scene while character selection is paused.");
            var encounter = UnityEngine.Object.FindAnyObjectByType<DoubaoWordWallEncounter2D>();
            if (encounter == null || encounter.State != DoubaoEncounterState.Idle)
                throw new InvalidOperationException("An idle, freshly loaded Doubao encounter is required.");
            var encounterConfig = (DoubaoWordWallConfig)new SerializedObject(encounter)
                .FindProperty("_config").objectReferenceValue;
            if (encounterConfig == null || encounterConfig.FallSpeed <= 0f)
                throw new InvalidOperationException("The encounter must have a positive configured fall speed.");
            var brains = UnityEngine.Object.FindObjectsByType<CompanionCommandSource2D>(FindObjectsSortMode.None);
            CompanionCommandSource2D ds = null, hs = null;
            foreach (var brain in brains)
            {
                if (brain.Combat == null) continue;
                if (brain.Combat.Role == PlayerRole.DeepSeek) ds = brain;
                if (brain.Combat.Role == PlayerRole.Harness) hs = brain;
            }
            if (ds == null || hs == null) throw new InvalidOperationException("Both configured role brains are required.");

            var bodies = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var bodyState = new BodyState[bodies.Length];
            for (int i = 0; i < bodies.Length; i++) bodyState[i] = new BodyState(bodies[i]);
            var colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var colliderState = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) colliderState[i] = colliders[i].enabled;
            var saved = new List<ManagedState>();
            var savedOwners = new List<object>();
            SaveBrain(ds, savedOwners, saved);
            SaveBrain(hs, savedOwners, saved);
            var mode = Physics2D.simulationMode;
            float timeScale = Time.timeScale;
            var output = new StringBuilder();
            if (measureAllocations)
            {
                output.AppendLine(EditorAllocationProbe.Calibrate());
                output.AppendLine("GC.Alloc count run: active recorder adds overhead; do not compare these timings to the CPU-only pass. Counts are NOT bytes.");
            }
            using var allocationProbe = measureAllocations ? new EditorAllocationProbe() : null;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                foreach (var brain in new[] { ds, hs })
                {
                    encounter.ResetEncounter();
                    RestoreBodies(bodyState);
                    RestoreManaged(saved);
                    foreach (var collider in colliders) if (collider != null) collider.enabled = false;
                    // 一次性启用全部池气泡的物理；不能在 MovePosition 之后重写 simulated，
                    // 否则测试本身可能清除该步待提交的位移，制造静止在顶部的假安全场景。
                    foreach (var body in bodies)
                    {
                        if (body == null) continue;
                        bool bubble = body.GetComponent<DoubaoWordWallBlock2D>() != null;
                        body.simulated = bubble;
                        // 同一次调用连续推进物理而无渲染帧，不能让旧插值 Transform 回写物理位置。
                        if (bubble) body.interpolation = RigidbodyInterpolation2D.None;
                    }
                    brain.Shape.enabled = true;
                    brain.Body.interpolation = RigidbodyInterpolation2D.None;
                    brain.Body.simulated = true;
                    brain.Body.linearVelocity = Vector2.zero;
                    brain.Body.angularVelocity = 0f;
                    Physics2D.SyncTransforms();
                    brain.ResetIntent();
                    encounter.BeginEncounter();

                    var motor = brain.Owner.GetComponent<PlayerMovementMotor2D>();
                    if (motor == null || !motor.isActiveAndEnabled)
                        throw new InvalidOperationException("The selected role's configured movement motor must be enabled.");
                    var snapshots = new CompanionObstacleSnapshot[CompanionObstacleRegistry2D.Capacity];
                    var navigation = (CompanionNavigation2D)typeof(CompanionCommandSource2D)
                        .GetField("_navigation", Fields).GetValue(brain);
                    Vector2 start = brain.Body.position;
                    float health = brain.Health.CurrentHealth;
                    float allyHealth = brain.AllyLife.CurrentHealth;
                    float travelled = 0f, elapsed = 0f, observedFallDistance = 0f, expectedFallDistance = 0f;
                    int unsafeSteps = 0, episodes = 0, peakBubbles = 0, maxExpanded = 0, movingSteps = 0;
                    int fallSamples = 0;
                    DoubaoWordWallBlock2D previousProbe = null;
                    uint previousProbeId = 0;
                    float previousProbeAfterY = 0f;
                    bool previouslyUnsafe = false;
                    float dt = Time.fixedDeltaTime;
                    if (dt <= 0f) throw new InvalidOperationException("A positive fixed step is required.");
                    int ticks = Mathf.CeilToInt(45f / dt);
                    var commandTimes = measurePerformance ? new long[ticks] : null;
                    var mazeTimes = measurePerformance ? new long[ticks] : null;
                    long commandAllocations = 0, mazeAllocations = 0;
                    // 只测两个业务调用区间；不把诊断的反射/截图/几何验证开销算成游戏耗时。
                    // 跳过前100固定步的首次JIT/冷启动，但保留整段移动与下落安全验证。
                    int performanceSamples = 0;
                    for (int tick = 0; tick < ticks; tick++)
                    {
                        // 感知时玩家参与查询，推进气泡物理时关闭玩家刚体以免触发真实伤害/救援。
                        brain.Body.simulated = true;
                        Physics2D.SyncTransforms();
                        Vector2 from = brain.Shape.bounds.center;
                        Vector2 extent = brain.Shape.bounds.extents;
                        Vector2 previousPosition = brain.Body.position;
                        int count = brain.Sensor.ObstacleRegistry.CopyVisible(from, 100f, snapshots, out bool saturated);
                        if (saturated) throw new InvalidOperationException("Diagnostic hazard snapshot unexpectedly truncated.");
                        bool sample = measurePerformance && tick >= 100;
                        bool countAllocations = measureAllocations && tick >= 100;
                        if (countAllocations) allocationProbe.Begin();
                        long started = sample ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                        bool hasCommand = brain.TryGetCommand((uint)(tick + 1), out var command);
                        if (sample)
                        {
                            commandTimes[performanceSamples] = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                        }
                        if (countAllocations) commandAllocations += ReadAllocationCount(allocationProbe);
                        if (!hasCommand)
                            throw new InvalidOperationException("The real companion brain rejected a diagnostic tick.");
                        // 只消费移动，不消费 AI 产生的攻击、技能、取消或准备意图。
                        motor.Simulate(command.Move, dt);
                        Vector2 next = brain.Body.position + brain.Body.linearVelocity * dt;
                        brain.Body.position = next;
                        Vector3 position = brain.Body.transform.position;
                        brain.Body.transform.position = new Vector3(next.x, next.y, position.z);
                        Physics2D.SyncTransforms();
                        Vector2 to = brain.Shape.bounds.center;
                        bool unsafeStep = !CompanionNavigation2D.IsSegmentSafe(from, to, 0f, dt, extent, 0f, snapshots, count);
                        if (unsafeStep)
                        {
                            unsafeSteps++;
                            if (!previouslyUnsafe) episodes++;
                        }
                        previouslyUnsafe = unsafeStep;
                        float distance = Vector2.Distance(previousPosition, next);
                        travelled += distance;
                        if (distance > .0001f) movingSteps++;

                        brain.Body.simulated = false;
                        var probe = encounter.ActiveBlocks.Count > 0 ? encounter.ActiveBlocks[0] : null;
                        uint probeId = probe != null ? probe.ReplicationId : 0;
                        var probeBody = probe != null ? probe.GetComponent<Rigidbody2D>() : null;
                        float beforeY = probeBody != null ? probeBody.position.y : 0f;
                        if (probe != null && probe == previousProbe && probeId == previousProbeId &&
                            Mathf.Abs(beforeY - previousProbeAfterY) > .001f)
                            throw new InvalidOperationException("Bubble position was overwritten between physics steps for " +
                                brain.Combat.Role + " at tick " + tick + ": previous=" + previousProbeAfterY.ToString("F6") +
                                ", current=" + beforeY.ToString("F6") + ". Results are invalid.");
                        if (countAllocations) allocationProbe.Begin();
                        started = sample ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                        encounter.Simulate(dt);
                        Physics2D.Simulate(dt);
                        if (sample)
                        {
                            mazeTimes[performanceSamples] = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                            performanceSamples++;
                        }
                        if (countAllocations) mazeAllocations += ReadAllocationCount(allocationProbe);
                        // 校验实际物理位置，不以生成数量或假设速度证明墙确实在运动。
                        // 回池或同一对象已重新出池的步骤不作为同一泡的下落样本。
                        if (probe != null && probe.IsActive && probe.ReplicationId == probeId)
                        {
                            float observed = beforeY - probeBody.position.y;
                            float expected = encounterConfig.FallSpeed * dt;
                            if (Mathf.Abs(observed - expected) > .001f)
                                throw new InvalidOperationException("Bubble fall validation failed for " + brain.Combat.Role +
                                    " at tick " + tick + ": observed=" + observed.ToString("F6") +
                                    ", expected=" + expected.ToString("F6") + ". Results are invalid.");
                            observedFallDistance += observed;
                            expectedFallDistance += expected;
                            fallSamples++;
                            previousProbe = probe;
                            previousProbeId = probeId;
                            previousProbeAfterY = probeBody.position.y;
                        }
                        else previousProbe = null;
                        elapsed += dt;
                        peakBubbles = Mathf.Max(peakBubbles, encounter.ActiveBlocks.Count);
                        maxExpanded = Mathf.Max(maxExpanded, navigation.LastExpandedNodes);
                        if (brain.Health.CurrentHealth != health || brain.AllyLife.CurrentHealth != allyHealth)
                            throw new InvalidOperationException("Contact isolation failed: a player's health changed.");
                    }
                    if (fallSamples < ticks / 2)
                        throw new InvalidOperationException("Too few actual falling-bubble samples; locomotion results are invalid.");
                    Vector2 end = brain.Body.position;
                    output.Append(brain.Combat.Role).Append(": simulated ").Append(elapsed.ToString("F2"))
                        .Append("s, unsafe swept intervals=").Append(unsafeSteps).Append('/').Append(ticks)
                        .Append(", overlap episodes=").Append(episodes)
                        .Append(", distance=").Append(travelled.ToString("F2"))
                        .Append(", net displacement=").Append(Vector2.Distance(start, end).ToString("F2"))
                        .Append(", moving steps=").Append(movingSteps)
                        .Append(", max expanded nodes=").Append(maxExpanded)
                        .Append(", peak bubbles=").Append(peakBubbles)
                        .Append(", observed sampled fall=").Append(observedFallDistance.ToString("F3"))
                        .Append('/').Append(expectedFallDistance.ToString("F3"))
                        .Append("u over ").Append(fallSamples).Append(" verified steps")
                        .Append(", HP unchanged=").Append(health.ToString("F0"))
                        .Append(", final plan=").Append(brain.Plan).AppendLine();
                    if (measurePerformance)
                    {
                        AppendPerformance(output, "TryGetCommand", commandTimes, performanceSamples);
                        AppendPerformance(output, "Encounter+Physics2D", mazeTimes, performanceSamples);
                    }
                    if (measureAllocations) output.Append("Warmed current-thread GC.Alloc events: command=")
                        .Append(commandAllocations).Append(", encounter+physics=").Append(mazeAllocations)
                        .Append(" over ").Append(ticks - 100).AppendLine(" calls each; byte sizes not measured.");
                }
                output.Append("Diagnostic only: real Brain/motor and continuous encounter/physics; no attacks, player contact or damage. Unsafe intervals are geometric overlap observations, not actual battle hit counts. Zero overlaps alone do not prove combat/phone/network acceptance.");
                return output.ToString();
            }
            finally
            {
                encounter.ResetEncounter();
                RestoreBodies(bodyState);
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null) colliders[i].enabled = colliderState[i];
                RestoreManaged(saved);
                Physics2D.SyncTransforms();
                Physics2D.simulationMode = mode;
                Time.timeScale = timeScale;
            }
        }

        private static long ReadAllocationCount(EditorAllocationProbe probe)
        {
            var result = probe.End();
            if (!result.IsValid) throw new InvalidOperationException("GC.Alloc probe unavailable or overflowed; counts are invalid.");
            return result.Count;
        }

        private static void AppendPerformance(StringBuilder output, string name, long[] samples, int count)
        {
            long total = 0;
            for (int i = 0; i < count; i++) total += samples[i];
            Array.Sort(samples, 0, count);
            double ms = 1000d / System.Diagnostics.Stopwatch.Frequency;
            output.Append(name).Append(": warmed samples=").Append(count)
                .Append(", mean/p95/max ms=").Append((total * ms / count).ToString("F4")).Append('/')
                .Append((samples[Mathf.CeilToInt(count * .95f) - 1] * ms).ToString("F4")).Append('/')
                .Append((samples[count - 1] * ms).ToString("F4")).AppendLine();
        }

        private static void SaveBrain(CompanionCommandSource2D brain, List<object> owners, List<ManagedState> saved)
        {
            Save(brain, owners, saved);
            Save(typeof(CompanionCommandSource2D).GetField("_navigation", Fields).GetValue(brain), owners, saved);
            Save(brain.Sensor, owners, saved);
            Save(brain.Combat, owners, saved);
            Save(brain.NodeGoal, owners, saved);
            Save(brain.SquadAnchor, owners, saved);
        }

        private static void Save(object owner, List<object> owners, List<ManagedState> saved)
        {
            if (owner == null || owners.Contains(owner)) return;
            owners.Add(owner);
            saved.Add(new ManagedState(owner));
        }

        private static void RestoreManaged(List<ManagedState> saved)
        {
            foreach (var state in saved) state.Restore();
        }

        private static void RestoreBodies(BodyState[] states)
        {
            foreach (var state in states) state.Restore();
        }

        private readonly struct BodyState
        {
            private readonly Rigidbody2D _body;
            private readonly Vector2 _position, _velocity;
            private readonly float _rotation, _angularVelocity;
            private readonly Vector3 _transformPosition;
            private readonly Quaternion _transformRotation;
            private readonly bool _simulated;
            private readonly RigidbodyInterpolation2D _interpolation;

            public BodyState(Rigidbody2D body)
            {
                _body = body; _position = body.position; _velocity = body.linearVelocity;
                _rotation = body.rotation; _angularVelocity = body.angularVelocity;
                _transformPosition = body.transform.position; _transformRotation = body.transform.rotation;
                _simulated = body.simulated;
                _interpolation = body.interpolation;
            }

            public void Restore()
            {
                if (_body == null) return;
                _body.simulated = false;
                _body.position = _position; _body.rotation = _rotation;
                _body.transform.SetPositionAndRotation(_transformPosition, _transformRotation);
                _body.linearVelocity = _velocity; _body.angularVelocity = _angularVelocity;
                _body.interpolation = _interpolation;
                _body.simulated = _simulated;
            }
        }

        /// <summary>保留原数组身份及内容；导航/感知的预分配缓冲不会被替换或遗漏。</summary>
        private sealed class ManagedState
        {
            private readonly object _owner;
            private readonly FieldInfo[] _fields;
            private readonly object[] _values;
            private readonly Array[] _arrays;

            public ManagedState(object owner)
            {
                _owner = owner;
                _fields = owner.GetType().GetFields(Fields);
                _values = new object[_fields.Length];
                _arrays = new Array[_fields.Length];
                for (int i = 0; i < _fields.Length; i++)
                {
                    _values[i] = _fields[i].GetValue(owner);
                    if (_values[i] is Array array) _arrays[i] = (Array)array.Clone();
                }
            }

            public void Restore()
            {
                for (int i = 0; i < _fields.Length; i++)
                {
                    if (_arrays[i] != null) Array.Copy(_arrays[i], (Array)_values[i], _arrays[i].Length);
                    if (!_fields[i].IsInitOnly) _fields[i].SetValue(_owner, _values[i]);
                }
            }
        }
    }
}
