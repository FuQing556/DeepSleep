using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudeEnergyChecks
    {
        [MenuItem("DeepSleep/Diagnostics/Claude Energy")]
        private static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var physicsScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
            int checks = 0;
            void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
            void Awake(object component) => component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
            try
            {
                ClaudeEnergyPattern2D e = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<ClaudeEnergyPattern2D>(true))
                    { if(item.name=="CL_Energy_Secondary")continue; Check(e == null, "One primary energy instance"); e = item; }
                Check(e != null && e.TryValidateConfiguration(out _), "Explicit scene assembly");
                Awake(e.HitEffects);
                int impacts = 0;
                e.HitEffects.Played += (_, _) => impacts++;
                Check(e.gameObject.layer == 8 && e.Perception.Hitbox.StopsPiercingBeams, "Enemy attack target stops beams");
                Check(e.Core.sortingLayerName == "Gameplay" && e.Halo.sortingLayerName == "Gameplay" &&
                    e.RadialBurst.sortingLayerName == "Gameplay" && e.Dissipation.sortingLayerName == "Gameplay", "VFX are above background sorting layer");
                Check(e.Config.PhaseOneHealth == 100 && e.Config.PhaseTwoHealth == 999 && e.Config.Damage == 2 &&
                    e.Config.ChargeSeconds == 2 && e.Config.CrossArenaSeconds == 4 && e.Config.LingerSeconds == 2.5f && e.Config.FadeSeconds == .6f,
                    "Approved balance");
                Check(Mathf.Abs(e.ExplosionRadius - 6.4f * 1.16f / 2) < .00001f, "Explosion keeps original outer halo radius");
                Check(e.Config.VisibleDiameter == 3.2f && Mathf.Abs(e.Config.HaloVisibleDiameter - 6.4f * 1.16f * 2 / 3) < .00001f &&
                    e.Config.CollisionRadius == 1.4f, "Inner half, halo two thirds, collision inner only");
                Check(e.GetComponentsInChildren<Collider2D>(true).Length == 1 && e.Shape.transform == e.transform,
                    "No collision on outer halo or explosion art layers");
                Check(e.Core.sprite.pivot == e.Halo.sprite.pivot && Vector2.Distance(e.Core.sprite.pivot, new Vector2(625.5f, 639)) < .01f,
                    "Rotation pivot aligns visible centers, not canvas midpoint");
                Awake(e.PoseTransition); Awake(e); Awake(e.Perception.Hitbox);
                var health = new HealthComponent[2]; var receivers = new PlayerDamageReceiver2D[2];
                for (int i = 0; i < 2; i++)
                {
                    // PreviewScene被全局Physics2D查询排除；仅迁移隔离副本的玩家到临时物理场景。
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(e.Targets[i].transform.root.gameObject, physicsScene);
                    health[i] = e.Targets[i].GetComponent<HealthComponent>(); receivers[i] = e.Targets[i].GetComponent<PlayerDamageReceiver2D>();
                    Awake(health[i]); Awake(receivers[i]); Awake(e.Targets[i]);
                }
                // 1000/.5 are test-only boss fixtures, not approved Claude HP.
                void Prepare()
                {
                    e.Cancel(); e.Boss.BeginAuthority(1000, .5f, null, null);
                    impacts = 0;
                    e.Muzzle.position = Vector2.zero;
                    for (int i = 0; i < 2; i++)
                    { health[i].ResetToMaximum(); receivers[i].ResetDamageGate(); e.Targets[i].transform.position = new Vector2(-8, i == 0 ? 4 : -4); }
                    Physics2D.SyncTransforms();
                }
                Prepare(); Check(!e.Shape.enabled && !e.Core.enabled, "No auto cast or default collision");
                Check(e.Begin(false, e.Targets[0].transform.position, 0), "Begin with live player identity");
                e.Simulate(1);
                e.Targets[0].transform.position = new Vector2(6, 3);
                Physics2D.SyncTransforms();
                Vector2 launchAim = ((Vector2)e.TargetShapes[0].bounds.center - (Vector2)e.Muzzle.position).normalized;
                e.Simulate(1);
                Check(e.State == ClaudeEnergyState.Flying && Vector2.Distance(e.Velocity.normalized, launchAim) < .0001f,
                    "Launch samples moved player's current position, not charge-start position");
                e.Targets[0].transform.position = new Vector2(-6, -3);
                Physics2D.SyncTransforms(); e.Simulate(.05f);
                Check(Vector2.Distance(e.Velocity.normalized, launchAim) < .0001f, "Live player aim freezes after launch");
                Prepare();
                Check(!e.Begin(false, Vector2.zero) && !e.Begin(false, new Vector2(float.NaN, 0)), "Reject invalid direction");
                Check(e.Begin(false, Vector2.left * 8), "Explicit begin");
                Check(e.CurrentHealth == 100 && !e.CanReceiveDamage && !e.Shape.enabled, "Charge is not an attack target");
                Check(!e.TryReceiveDamage(new DamagePacket(1, Vector2.zero, Vector2.right, null)), "No damage before flight");
                e.Simulate(0); e.Simulate(float.NaN); Check(e.State == ClaudeEnergyState.Charging, "Pause and invalid step");
                e.Simulate(1.99f); Check(e.State == ClaudeEnergyState.Charging, "Charge boundary");
                e.Simulate(.01f); Check(e.State == ClaudeEnergyState.Flying && e.Shape.enabled && e.CanReceiveDamage, "Fires at two seconds");
                Check(e.Registry.TryResolve(e.Shape, out var body) && body == e.Perception, "Shared attack perception");
                Check(e.Obstacles.RegisteredCount > 0, "AI obstacle registration");
                Check(e.Core.transform.localEulerAngles.z != e.Halo.transform.localEulerAngles.z, "Counter rotation");
                Check(!e.Begin(false, Vector2.right), "Cannot overwrite live flight");
                e.Simulate(.2f); Check(Vector2.Distance(e.Position, new Vector2(-.96f, 0)) < .001f, "Four second arena width speed");
                Check(e.Perception.Hitbox.TryReceiveDamage(new DamagePacket(99, e.Position, Vector2.right, null)) && e.CurrentHealth == 1, "Real hitbox route");
                Check(e.Perception.Hitbox.TryReceiveDamage(new DamagePacket(1, e.Position, Vector2.right, null)), "Break orb");
                Check(e.State == ClaudeEnergyState.Exploding && e.ExplosionCount == 1 && !e.Shape.enabled, "Immediate explosion at break point");
                Check(!e.Registry.TryResolve(e.Shape, out _) && e.Obstacles.RegisteredCount == 0, "Explosion unregisters attack and obstacle");
                Check(!e.TryReceiveDamage(new DamagePacket(1, e.Position, Vector2.right, null)), "No duplicate explosion");
                e.Simulate(2.49f); Check(e.Dissipation.enabled && e.Dissipation.color.a == 1 && e.RadialBurst.color.a == 0,
                    "Linger visible before fade");
                e.Simulate(.31f); Check(e.Dissipation.color.a > .49f && e.Dissipation.color.a < .51f, "Half fade");
                e.Simulate(.4f); Check(e.State == ClaudeEnergyState.Complete && !e.Dissipation.enabled && !e.Core.enabled, "Complete hides all layers");
                Prepare(); Check(e.Begin(true, Vector2.right * 8) && e.CurrentHealth == 999, "Phase two health restored to 999");
                e.Simulate(20); Check(e.State == ClaudeEnergyState.Complete && e.ExplosionCount == 1, "Large step crosses charge, boundary, linger and fade");
                Vector2 wallPoint = e.Position;
                Prepare(); e.Begin(true, Vector2.right * 8);
                for (int i = 0; i < 1000; i++) e.Simulate(.02f);
                Check(e.State == ClaudeEnergyState.Complete && Vector2.Distance(e.Position, wallPoint) < .001f, "Small and large steps explode at same boundary");
                Prepare(); e.Targets[0].transform.position = new Vector2(4, 0); Physics2D.SyncTransforms();
                e.Begin(false, Vector2.right * 8); e.Simulate(2);
                float sweepHealth = health[0].CurrentHealth;
                e.Simulate(2);
                Check(e.State == ClaudeEnergyState.Exploding && e.ExplosionCount == 1 && e.Position.x > 0 && e.Position.x < 4,
                    "Swept player contact detonates before tunnelling");
                Check(health[0].CurrentHealth == sweepHealth - 2, "Contact is explosion only, not contact plus explosion damage");
                Check(impacts == 1, "Explosion accepted hit plays Claude attack effect");
                Prepare(); e.Begin(false, Vector2.right * 8); e.Simulate(2); e.Targets[0].transform.position = new Vector2(0, 4);
                Physics2D.SyncTransforms(); e.Simulate(.1f);
                Check(e.State == ClaudeEnergyState.Flying && Mathf.Abs(e.Position.y) < .001f, "Flight does not retarget");
                Prepare(); e.Targets[0].transform.position = new Vector2(0, 2.05f); Physics2D.SyncTransforms();
                e.Begin(false, Vector2.right * 8); e.Simulate(2.1f);
                Check(e.State == ClaudeEnergyState.Flying && e.ExplosionCount == 0 && health[0].CurrentHealth == health[0].MaximumHealth,
                    "Touching visual halo outside inner sphere does not detonate or damage");
                Prepare(); e.Targets[0].transform.position = Vector2.zero; e.Targets[1].transform.position = new Vector2(7, 4); Physics2D.SyncTransforms();
                Check(e.Begin(false, Vector2.right * 8), "Explosion fixture"); e.Simulate(2);
                float before = health[0].CurrentHealth;
                Check(e.TryReceiveDamage(new DamagePacket(100, e.Position, Vector2.right, null)), "Player breaks orb nearby");
                Check(health[0].CurrentHealth == before - 2 && e.DamageApplications == 1, "Explosion deals two through shared player damage gate");
                e.Simulate(2.9f); Check(health[0].CurrentHealth == before - 2 && e.ExplosionCount == 1, "Linger has no repeated damage");
                Prepare(); e.Targets[0].transform.position = Vector2.zero; Physics2D.SyncTransforms(); receivers[0].BeginInvulnerability(10);
                e.Begin(false, Vector2.right * 8); e.Simulate(2); before = health[0].CurrentHealth;
                e.TryReceiveDamage(new DamagePacket(100, e.Position, Vector2.right, null));
                Check(health[0].CurrentHealth == before, "Explosion respects invulnerability");
                Prepare(); e.Targets[0].transform.position = Vector2.zero; Physics2D.SyncTransforms();
                var shield = new BlockableShield(); receivers[0].RegisterDamageInterceptor(shield);
                e.Begin(false, Vector2.right * 8); e.Simulate(2); before = health[0].CurrentHealth;
                e.TryReceiveDamage(new DamagePacket(100, e.Position, Vector2.right, null));
                Check(shield.Hits == 1 && health[0].CurrentHealth == before, "Explosion uses shared blockable shield interception");
                Check(impacts == 1, "Shield interception keeps Claude impact, without HP loss");
                receivers[0].UnregisterDamageInterceptor(shield);
                Prepare(); e.Targets[0].transform.position = new Vector2(0, -1); e.Targets[1].transform.position = new Vector2(0, 1);
                Physics2D.SyncTransforms(); e.Begin(false, Vector2.right * 8); e.Simulate(2);
                e.TryReceiveDamage(new DamagePacket(100, e.Position, Vector2.right, null));
                Check(e.DamageApplications == 2 && health[0].CurrentHealth == health[0].MaximumHealth - 2 &&
                    health[1].CurrentHealth == health[1].MaximumHealth - 2, "Each player receives exactly one explosion");
                Check(impacts == 2, "Each accepted explosion victim gets one Claude impact");
                Prepare(); e.Begin(false, Vector2.right * 8); e.Simulate(2); e.Boss.ResetEncounter(); e.Simulate(.02f);
                Check(e.State == ClaudeEnergyState.Idle && !e.Shape.enabled && e.Obstacles.RegisteredCount == 0, "Boss death cancels flight");
                for (int i = 0; i < 12; i++)
                { Prepare(); e.Begin((i & 1) != 0, Vector2.right * 8); e.Simulate(2.05f); e.Cancel(); Check(!e.Shape.enabled && e.Obstacles.RegisteredCount == 0, "Reuse has no stale collision or AI threat"); }
                return "Claude energy: " + checks + " checks passed (isolated Editor, not encounter/network/device verification).";
            }
            finally
            {
                EditorSceneManager.CloseScene(physicsScene, true);
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeScene);
            }
        }
        private sealed class BlockableShield : IPlayerDamageInterceptor
        {
            public int Hits;
            public bool TryIntercept(PlayerDamageReceiver2D receiver, in DamagePacket packet)
            {
                if (packet.InterceptionPolicy != DamageInterceptionPolicy.Blockable) return false;
                Hits++; return true;
            }
        }
    }
}
