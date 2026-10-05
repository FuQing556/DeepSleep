using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Players.Companion;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>用真实遭遇生成30秒快照，再做独立电机闭环；不冒充完整连续刷新迷宫通关测试。</summary>
    public static class CompanionMazeSnapshotChecks
    {
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("World01 Play required");
            var encounter = UnityEngine.Object.FindAnyObjectByType<DoubaoWordWallEncounter2D>();
            if (encounter == null || encounter.State != DoubaoEncounterState.Idle)
                throw new InvalidOperationException("Use a fresh World01 selection pause (idle encounter).");
            var bodies = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b.simulated && b.GetComponent<DoubaoWordWallBlock2D>() == null).ToArray();
            float scale = Time.timeScale;
            var mode = Physics2D.simulationMode;
            try
            {
                Time.timeScale = 0; Physics2D.simulationMode = SimulationMode2D.Script;
                foreach (var body in bodies) body.simulated = false;
                encounter.BeginEncounter();
                for (int i = 0; i < 1500; i++) { encounter.Simulate(.02f); Physics2D.Simulate(.02f); }
                // 不再推进物理；恢复角色bounds供诊断读取，临时气泡不会接触真实玩家。
                foreach (var body in bodies) body.simulated = true;
                Physics2D.SyncTransforms();
                string result = "Actual encounter snapshot: " + encounter.ActiveBlocks.Count + " bubbles at 30 seconds.\n";
                return result + CompanionNavigationMotorChecks.Run();
            }
            finally
            {
                encounter.ResetEncounter();
                foreach (var body in bodies) if (body != null) body.simulated = true;
                Physics2D.SyncTransforms();
                Physics2D.simulationMode = mode; Time.timeScale = scale;
            }
        }
    }
}
