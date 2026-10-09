using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudeActorChecks
    {
        private sealed class BlockContact : IPlayerDamageInterceptor
        {
            public bool TryIntercept(PlayerDamageReceiver2D receiver, in DamagePacket damage) => true;
        }
        private static void Awake(object target) => target.GetType().GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(target, null);
        [MenuItem("DeepSleep/Diagnostics/Claude Actor and HUD")]
        private static void Menu() => Debug.Log(Run());
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var original = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var physics = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            int checks = 0;
            void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
            try
            {
                var actor = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClaudeBossActor2D>(true)).Single();
                var hud = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHealthHudView>(true)).Single();
                Check(actor.TryValidateConfiguration(out _), "Explicit actor references");
                Check(actor.Config.MaximumHealth == 10000 && actor.Config.PhaseTwoHealthFraction == .5f &&
                    actor.Config.ContactDamage == 1 && actor.Config.ContactIntervalSeconds == 1 &&
                    actor.Config.ContactKnockbackDistance == .65f && actor.Config.ContactKnockbackSeconds == .18f, "Approved balance");
                Check(hud.Boss == actor.Body && hud.HealthFill.GetComponentInParent<UnityEngine.UI.RectMask2D>() != null,
                    "HUD binds actual HP and clips fill");
                Awake(actor.Body); Awake(actor.PoseTransition); Awake(actor.HitEffects); Awake(actor); Awake(hud);
                var initialPosition = actor.transform.position;
                Check(actor.FacingVisual != null && actor.EnergyMuzzle != null, "Explicit facing visual and muzzle");
                actor.transform.position = new Vector3(-3, 0, 0); actor.FaceDefault();
                Check(actor.FacingVisual.flipX && actor.EnergyMuzzle.localPosition.x > 0, "Left half faces right by default");
                actor.FaceAt(new Vector2(-5, 0));
                Check(!actor.FacingVisual.flipX && actor.EnergyMuzzle.localPosition.x < 0, "Aim left overrides half-screen default");
                actor.transform.position = new Vector3(3, 0, 0); actor.FaceDefault();
                Check(!actor.FacingVisual.flipX, "Right half uses normal left-facing sprite");
                actor.FaceAt(new Vector2(5, 0));
                Check(actor.FacingVisual.flipX && actor.EnergyMuzzle.localPosition.x > 0, "Aim right mirrors sprite and muzzle without mirroring sphere");
                actor.transform.position = initialPosition; actor.FaceDefault();
                hud.RenderNow();
                Check(!actor.Body.IsShown && hud.Visibility.alpha == 0, "Dormant before encounter");
                Check(actor.BeginAuthority(null, null), "Authority starts configured HP");
                hud.RenderNow();
                Check(hud.HealthFill.fillAmount == 1 && hud.HealthText.text.Contains("10000 / 10000"), "Full HP readout");
                var damage = new DamagePacket(99999, Vector2.zero, Vector2.left, null);
                Check(actor.Body.TryReceiveDamage(in damage) && actor.Body.CurrentHealth == 5000, "Half-health floor");
                actor.BeginPhaseTransition(); hud.RenderNow();
                Check(actor.Pose == ClaudePose.PhaseChange && !actor.Body.CanReceiveDamage &&
                    !actor.Body.TryReceiveDamage(in damage), "Phase pose protects body");
                Check(actor.CompletePhaseTransition() && actor.Body.PhaseTwo && actor.Body.CanReceiveDamage, "Explicit phase end unlocks");
                hud.RenderNow();
                Check(hud.HealthFill.fillAmount == .5f && hud.HealthText.text.Contains("II"), "Phase II HUD");
                actor.SetPose(ClaudePose.Move);
                Check(actor.Pose == ClaudePose.Move, "Moving pose available");
                actor.Body.TryReceiveDamage(in damage); hud.RenderNow();
                Check(actor.Pose == ClaudePose.Defeated && !actor.Body.Sphere.enabled && hud.Visibility.alpha == 0,
                    "Defeat pose, contact disabled and HUD hidden");
                actor.ResetActor();
                Check(actor.Pose == ClaudePose.Idle && !actor.Body.IsShown, "Reset restores idle");

                actor.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(actor.gameObject, physics);
                var health = new HealthComponent[2]; var receiver = new PlayerDamageReceiver2D[2];
                for (int i = 0; i < 2; i++)
                {
                    SceneManager.MoveGameObjectToScene(actor.Targets[i].transform.root.gameObject, physics);
                    health[i] = actor.Targets[i].GetComponent<HealthComponent>();
                    receiver[i] = actor.Targets[i].GetComponent<PlayerDamageReceiver2D>();
                    Awake(health[i]); Awake(receiver[i]); Awake(actor.Targets[i]); Awake(receiver[i].MovementMotor);
                    health[i].ResetToMaximum(); receiver[i].ResetDamageGate();
                    actor.Targets[i].transform.position = new Vector2(-20, i * 10);
                }
                actor.transform.position = Vector3.zero;
                actor.BeginAuthority(null, null);
                int impacts = 0; actor.HitEffects.Played += (_, _) => impacts++;
                float before = health[0].CurrentHealth;
                actor.Targets[0].transform.position = (Vector2)actor.Body.Sphere.bounds.center + Vector2.left;
                Physics2D.SyncTransforms();
                actor.SimulateContact(.01f);
                Check(health[0].CurrentHealth == before - 1 && impacts == 1, "Sphere contact damage and Claude impact");
                Check(Mathf.Approximately(receiver[0].MovementMotor.KnockbackRemaining, .18f) &&
                    receiver[0].MovementMotor.KnockbackVelocity.x < 0, "Actual HP loss rebounds outward");
                actor.SimulateContact(.5f);
                Check(health[0].CurrentHealth == before - 1 && impacts == 1, "Contact cooldown prevents repeated hits");
                receiver[0].ResetDamageGate(); actor.SimulateContact(.51f);
                Check(health[0].CurrentHealth == before - 2 && impacts == 2, "Contact resumes after interval");
                actor.ResetActor(); receiver[0].ResetDamageGate(); actor.SimulateContact(5);
                Check(health[0].CurrentHealth == before - 2 && impacts == 2, "No ghost contact after cleanup");
                health[0].ResetToMaximum(); receiver[0].ResetDamageGate(); receiver[0].MovementMotor.ResetKnockback();
                var blocker = new BlockContact(); receiver[0].RegisterDamageInterceptor(blocker);
                actor.BeginAuthority(null, null); impacts = 0; actor.SimulateContact(.01f);
                Check(health[0].CurrentHealth == before && receiver[0].MovementMotor.KnockbackRemaining == 0 && impacts == 1,
                    "Shield blocks HP and rebound but keeps Claude impact");
                receiver[0].UnregisterDamageInterceptor(blocker);
                actor.ResetActor();
                return $"Claude Actor/HUD: {checks} checks passed; isolated Editor, not a full encounter/device test.";
            }
            finally
            {
                EditorSceneManager.CloseScene(physics, true);
                EditorSceneManager.ClosePreviewScene(preview);
                SceneManager.SetActiveScene(original);
            }
        }
    }
}
