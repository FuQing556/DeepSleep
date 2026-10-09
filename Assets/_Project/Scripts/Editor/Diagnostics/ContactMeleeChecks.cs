using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Movement;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>显式执行的隔离Prefab检查，不打开对局，不写存档或正式资产。</summary>
    public static class ContactMeleeChecks
    {
        private static void Awake(object component) => component.GetType().GetMethod("Awake",
            BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
        private sealed class Shield : IPlayerDamageInterceptor
        {
            public bool TryIntercept(PlayerDamageReceiver2D receiver, in DamagePacket damage) => true;
        }

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int checks = 0;
            void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
            string root = "Assets/_Project/Prefabs/Combat/Enemies/";
            string[] paths = { "404Window/PF_Enemy_404Window.prefab", "Internet/PF_Enemy_Download.prefab",
                "Internet/PF_Enemy_SecurityGuard.prefab", "RecursiveJelly/PF_Enemy_Recursive_Large.prefab",
                "RecursiveJelly/PF_Enemy_Recursive_Medium.prefab", "RecursiveJelly/PF_Enemy_Recursive_Small.prefab",
                "QuickApp/PF_Enemy_QuickApp.prefab" };
            foreach (string role in new[] { "DeepSeek", "Harness" })
            {
                var player = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Players/PF_Player_" + role + ".prefab");
                try
                {
                    var health = player.GetComponent<HealthComponent>();
                    var receiver = player.GetComponent<PlayerDamageReceiver2D>();
                    var motor = receiver.MovementMotor;
                    var hitbox = player.GetComponent<DamageHitbox2D>();
                    var body = player.GetComponent<Rigidbody2D>();
                    var shape = player.GetComponent<Collider2D>();
                    Awake(health); Awake(motor); Awake(receiver); Awake(hitbox);
                    player.transform.position = Vector3.zero; body.position = Vector2.zero;
                    foreach (string path in paths)
                    {
                        var enemy = PrefabUtility.LoadPrefabContents(root + path);
                        try
                        {
                            var actor = enemy.GetComponent<EnemyActor2D>();
                            var contact = enemy.GetComponent<EnemyContactAttack2D>();
                            var cfg = (EnemyContactDamageConfig)new SerializedObject(contact).FindProperty("_config").objectReferenceValue;
                            Awake(actor); Awake(contact);
                            enemy.transform.position = Vector3.left;
                            Physics2D.SyncTransforms();
                            int despawns = 0, impacts = 0;
                            actor.DespawnRequested += (_, __) => despawns++;
                            actor.ContactImpacted += _ => impacts++;
                            health.ResetToMaximum(); receiver.ResetDamageGate();
                            Check(contact.TryImpact(shape), role + path + " contact");
                            Check(health.CurrentHealth == health.MaximumHealth - cfg.DamageAmount &&
                                motor.KnockbackRemaining == cfg.KnockbackSeconds && motor.KnockbackVelocity.x > 0, "Damage starts outward knockback");
                            Check(despawns == (cfg.DespawnOnImpact ? 1 : 0) && impacts == (cfg.DespawnOnImpact ? 0 : 1), "Disposable vs persistent event");
                            Check(!contact.TryImpact(shape), "No same-frame repeated hit");
                            contact.Simulate(cfg.RepeatIntervalSeconds);
                            health.ResetToMaximum(); receiver.ResetDamageGate();
                            if (!cfg.DespawnOnImpact)
                            {
                                Check(contact.TryImpact(shape) && impacts == 2 && despawns == 0, "Persistent repeats without exit");
                                contact.Simulate(cfg.RepeatIntervalSeconds);
                                health.ResetToMaximum(); receiver.ResetDamageGate();
                                var shield = new Shield(); receiver.RegisterDamageInterceptor(shield);
                                try { Check(contact.TryImpact(shape) && health.CurrentHealth == health.MaximumHealth && motor.KnockbackRemaining == 0, "Shield blocks damage and knockback"); }
                                finally { receiver.UnregisterDamageInterceptor(shield); }
                                contact.Simulate(cfg.RepeatIntervalSeconds);
                                receiver.BeginInvulnerability(1f);
                                Check(contact.TryImpact(shape) && health.CurrentHealth == health.MaximumHealth && motor.KnockbackRemaining == 0, "Invulnerability blocks knockback");
                                receiver.ResetDamageGate();
                                if (path.StartsWith("QuickApp/", StringComparison.Ordinal))
                                {
                                    foreach (Vector2 origin in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                                    {
                                        contact.Simulate(cfg.RepeatIntervalSeconds);
                                        health.ResetToMaximum(); receiver.ResetDamageGate();
                                        // 刚体仍在旧边，接触起点已经穿到对边；击退必须按新起点向外。
                                        Vector2 center = shape.bounds.center;
                                        Check(contact.TryImpact(shape, center + origin) &&
                                            Vector2.Dot(motor.KnockbackVelocity.normalized, -origin) > .99f,
                                            "Wrapped segment uses local contact origin " + origin);
                                    }
                                }
                            }
                            else Check(!contact.TryImpact(shape), "Disposable cannot attack twice");
                        }
                        finally { PrefabUtility.UnloadPrefabContents(enemy); }
                    }
                    var settings = (PlayerMotorConfig)new SerializedObject(motor).FindProperty("config").objectReferenceValue;
                    Vector2 position = Vector2.zero, velocity = Vector2.zero, kick = Vector2.right * (2f * 1.8f / .22f);
                    float remaining = .22f;
                    for (int i = 0; i < 12; i++)
                    {
                        PlayerMovementStep.Calculate(ref position, ref velocity, Vector2.left, settings, Vector2.zero,
                            Vector2.zero, .02f, ref kick, ref remaining);
                        position += velocity * .02f;
                        if (remaining <= 0) break;
                    }
                    Check(Mathf.Abs(position.x - 1.8f) < .001f && remaining == 0f, "Exact distance despite opposite input");
                    position = settings.MovementBounds.max - Vector2.one * .01f; velocity = Vector2.zero;
                    kick = Vector2.one * 20f; remaining = .22f;
                    PlayerMovementStep.Calculate(ref position, ref velocity, Vector2.zero, settings, Vector2.zero,
                        Vector2.zero, .22f, ref kick, ref remaining); position += velocity * .22f;
                    Check(position.x <= settings.MovementBounds.xMax && position.y <= settings.MovementBounds.yMax, "Arena clamp");
                    receiver.ResetDamageGate(); Check(motor.KnockbackRemaining == 0, "Checkpoint clears knockback");
                }
                finally { PrefabUtility.UnloadPrefabContents(player); }
            }
            return "CONTACT MELEE PASS " + checks;
        }
    }
}
