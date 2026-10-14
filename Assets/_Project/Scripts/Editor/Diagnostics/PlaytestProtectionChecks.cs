using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只在隔离的角色预制体中验证现实秒保护，不写正式资源或存档。</summary>
    public static class PlaytestProtectionChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int checks = 0;
            foreach (string role in new[] { "DeepSeek", "Harness" })
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Players/PF_Player_" + role + ".prefab");
                try
                {
                    var hp = root.GetComponent<HealthComponent>();
                    var receiver = root.GetComponent<PlayerDamageReceiver2D>();
                    foreach (object component in new object[] { hp, receiver })
                        component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
                    foreach (float scale in new[] { .5f, 1f, 2f, 4f })
                    {
                        receiver.ResetDamageGate();
                        receiver.BeginInvulnerability(1.5f);
                        float realStep = .02f / scale;
                        int ticks = Mathf.RoundToInt(1.4f / realStep);
                        for (int tick = 0; tick < ticks; tick++) receiver.AdvanceProtection(realStep);
                        Require(receiver.IsReviveProtected && Mathf.Abs(receiver.RemainingInvulnerabilitySeconds - .1f) < .001f, role + " protected at 1.4 real seconds"); checks++;
                        float remaining = receiver.RemainingInvulnerabilitySeconds;
                        receiver.AdvanceProtection(0); receiver.AdvanceProtection(float.NaN);
                        Require(receiver.RemainingInvulnerabilitySeconds == remaining, "Paused/invalid step cannot spend protection"); checks++;
                        receiver.AdvanceProtection(.11f);
                        Require(!receiver.IsInvulnerable && !receiver.IsReviveProtected, "Protection ends by real duration"); checks++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return checks + " protection checks passed";
        }

        private static void Require(bool valid, string reason)
        { if (!valid) throw new Exception(reason); }
    }
}
