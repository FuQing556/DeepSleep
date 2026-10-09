using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudeBossBaseChecks
    {
        [MenuItem("DeepSleep/Diagnostics/Claude Boss Base")]
        private static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Combat/Encounters/Claude/PF_CL_BossBase.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            root.hideFlags = HideFlags.HideAndDontSave;
            int checks = 0;
            void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
            void Invoke(object target, string method) => target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
            try
            {
                var boss = root.GetComponent<BossDamageBody2D>();
                var feedback = root.GetComponent<BossBarrierFeedback2D>();
                var hitbox = root.GetComponent<DamageHitbox2D>();
                Check(boss.TryValidateConfiguration(out _) && feedback.TryValidateConfiguration(out _), "Explicit references");
                Invoke(boss, "Awake"); Invoke(hitbox, "Awake");
                Invoke(feedback, "OnEnable");
                Check(!boss.IsShown && !boss.Sphere.enabled, "Hidden before encounter");
                Check(AssetDatabase.GetAssetPath(feedback.Barrier.sprite).EndsWith("VFX_CL_Barrier_v01.png"), "User-selected first shield");
                Check(hitbox.StopsPiercingBeams && !hitbox.UseReceiverHitFeedback && hitbox.DamageNumberAnchor == null, "Sphere blocks beams but retains player weapon feedback");
                Check(Mathf.Approximately(boss.Sphere.radius, 2.6f) && boss.Sphere.offset == Vector2.zero, "Kimi shield size, Claude centered offset");
                // 100/.5 are test fixtures, not Claude gameplay balance.
                Check(boss.BeginAuthority(100, .5f, null, null), "Authority start");
                feedback.Advance(1);
                Check(feedback.Barrier.enabled && Mathf.Approximately(feedback.Alpha, .8f), "Idle shield");
                Vector2 point = boss.Sphere.transform.TransformPoint(boss.Sphere.offset);
                point += Vector2.left * boss.Sphere.radius;
                var packet = new DamagePacket(10, point, Vector2.right, null);
                Check(hitbox.TryReceiveDamage(packet) && boss.CurrentHealth == 90, "Shared damage receiver");
                Check(Mathf.Approximately(feedback.Alpha, .5f), "Hit dims shield using Kimi alpha");
                feedback.Advance(.1f);
                Check(Mathf.Approximately(feedback.Alpha, .8f), "0.1s recovery");
                var lethal = new DamagePacket(999, point, Vector2.right, null);
                Check(boss.TryReceiveDamage(lethal) && boss.CurrentHealth == 50 && boss.IsPhaseHealthLocked, "Phase floor");
                Check(!boss.TryReceiveDamage(packet), "Phase damage lock");
                Check(boss.CommitPhaseAtSkillBoundary() && boss.CanReceiveDamage, "Phase boundary unlock");
                int defeats = 0; boss.Defeated += () => defeats++;
                Check(boss.TryReceiveDamage(lethal) && !boss.IsAlive && !boss.Sphere.enabled && defeats == 1, "Defeat");
                Check(!boss.TryReceiveDamage(packet) && defeats == 1, "No duplicate defeat");
                boss.ResetEncounter(); feedback.Advance(0);
                Check(!feedback.Barrier.enabled && !boss.IsShown, "Reset cleanup");
                Check(boss.ApplyReplica(100, 50, true, true) && !boss.CanReceiveDamage && !boss.Sphere.enabled, "Replica cannot simulate damage");
                return $"Claude Boss base: {checks} checks passed; isolated Editor fixture, not actual fight/device verification.";
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
