using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Presentation;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>真实玩家伤害门的非致命 Play 检查；临时状态在 finally 恢复，不写偏好或存档。</summary>
    public static class PlayerHitFeedbackLocalChecks
    {
        private const BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class AbsorbDamage : IPlayerDamageInterceptor
        {
            public int Calls;
            public bool TryIntercept(PlayerDamageReceiver2D target, in DamagePacket damage)
            {
                Calls++;
                return true;
            }
        }

        private sealed class PlayerState
        {
            public readonly PlayerHitFeedbackPresenter2D Presenter;
            public readonly HealthComponent Health;
            private readonly float _health;
            private readonly object _suppressReward, _invulnerability, _reviveProtection;
            private readonly object _elapsed, _protection;

            public PlayerState(PlayerHitFeedbackPresenter2D presenter)
            {
                Presenter = presenter;
                Health = (HealthComponent)Get(presenter.Receiver, "_health");
                _health = Health.CurrentHealth;
                _suppressReward = Get(Health, "<LastDamageSuppressesKillReward>k__BackingField");
                _invulnerability = Get(presenter.Receiver, "_remainingInvulnerabilitySeconds");
                _reviveProtection = Get(presenter.Receiver, "_isReviveProtection");
                _elapsed = Get(presenter, "_elapsed");
                _protection = Get(presenter, "_protection");
            }

            public void Restore()
            {
                Presenter.ResetFeedback();
                Health.RestoreCheckpointHealth(_health);
                Set(Health, "<LastDamageSuppressesKillReward>k__BackingField", _suppressReward);
                Set(Presenter.Receiver, "_remainingInvulnerabilitySeconds", _invulnerability);
                Set(Presenter.Receiver, "_isReviveProtection", _reviveProtection);
                Set(Presenter, "_elapsed", _elapsed);
                Set(Presenter, "_protection", _protection);
                Presenter.RenderNow();
            }
        }

        public static string Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play through Boot/MainMenu first.");
            var network = UnityEngine.Object.FindFirstObjectByType<NetworkPlayerHitFeedbackChannel>();
            Require(network != null && network.TryValidateConfiguration(out _), "Missing installed player hit feedback");
            Require(network.Session.Phase == SessionPhase.Offline && !network.Session.HasPeer,
                "Run only in an offline scene");
            var presenters = new[] { network.DeepSeek, network.Harness };
            foreach (var presenter in presenters)
            {
                Require(presenter.isActiveAndEnabled && presenter.Receiver.isActiveAndEnabled,
                    "Both player presenters and receivers must be active");
                var health = (HealthComponent)Get(presenter.Receiver, "_health");
                Require(health.CanReceiveDamage && health.MaximumHealth >= 2f,
                    "Both players must be alive, with at least two maximum HP");
                var revive = presenter.Actor.GetComponent<PlayerReviveActionChannel>();
                Require(revive == null || !revive.IsChanneling, "Do not test during a revive channel");
            }
            Require(network.DeepSeek.Assignment == network.Harness.Assignment &&
                network.DeepSeek.CameraFeedback == network.Harness.CameraFeedback,
                "Both actors must share local assignment and camera feedback");

            var states = presenters.Select(p => new PlayerState(p)).ToArray();
            var assignment = network.DeepSeek.Assignment;
            var camera = network.DeepSeek.CameraFeedback;
            Require(camera.isActiveAndEnabled, "Camera presentation must be enabled");
            object oldRole = Get(assignment, "<CurrentLocalPlayerRole>k__BackingField");
            object oldActor = Get(assignment, "<CurrentLocalPlayerActor>k__BackingField");
            object[] oldCommands = presenters.Select(p => Get(p.Actor.CommandDispatcher, "_commandSource")).ToArray();
            string[] cameraFields = { "_restPosition", "_currentOffset", "_smoothVelocity", "_hitDirection",
                "_hitElapsed", "_hitActive", "_presentationOffset", "_hitShakeStrength" };
            object[] oldCamera = cameraFields.Select(field => Get(camera, field)).ToArray();
            Vector3 oldCameraPosition = camera.transform.position;
            float scale = Time.timeScale;
            float preferredShake = PlayerHitFeedbackOptions.ShakeStrength;
            int checks = 0;
            try
            {
                Time.timeScale = 0f;
                foreach (PlayerRole localRole in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    Require(assignment.TrySelectLocalPlayerRole(localRole), "Real local role selection failed");
                    foreach (var state in states)
                    {
                        var presenter = state.Presenter;
                        var receiver = presenter.Receiver;
                        var health = state.Health;
                        foreach (var other in presenters) other.ResetFeedback();
                        camera.ResetHit();
                        Require(health.RestoreCheckpointHealth(health.MaximumHealth), "Prepare non-lethal health");
                        receiver.ResetDamageGate();
                        uint before = presenter.PlayedCount;
                        var otherPresenter = presenter == network.DeepSeek ? network.Harness : network.DeepSeek;
                        uint otherBefore = otherPresenter.PlayedCount;
                        float hp = health.CurrentHealth;
                        var damage = new DamagePacket(1f, presenter.Actor.transform.position, Vector2.right,
                            presenter.Actor.gameObject, 0, DamageInterceptionPolicy.BypassesProtection);
                        Require(receiver.TryReceiveDamage(in damage), "Real damage rejected");
                        Require(Mathf.Approximately(health.CurrentHealth, hp - 1f) &&
                            presenter.PlayedCount == before + 1 && otherPresenter.PlayedCount == otherBefore,
                            "One accepted HP loss must trigger exactly its own presenter"); checks++;
                        Require(presenter.IsPlaying && presenter.HudFlash.enabled &&
                            Mathf.Approximately(presenter.RemainingProtection, receiver.RemainingInvulnerabilitySeconds),
                            "Accepted hit must display HUD and protection feedback"); checks++;
                        if (presenter.BodySprite.enabled && presenter.BodySprite.gameObject.activeInHierarchy)
                            Require(presenter.HitOverlay.enabled && presenter.HitOverlay.sprite == presenter.BodySprite.sprite,
                                "Visible body must receive the matching overlay");
                        bool expectShake = presenter.Actor.Definition.Role == localRole && preferredShake > 0f;
                        Require((bool)Get(camera, "_hitActive") == expectShake,
                            "Only the local role may start a permitted camera shake"); checks++;

                        Require(!receiver.TryReceiveDamage(in damage) &&
                            Mathf.Approximately(health.CurrentHealth, hp - 1f) && presenter.PlayedCount == before + 1,
                            "Invulnerable duplicate caused another HP loss/feedback"); checks++;

                        receiver.BeginInvulnerability(2f);
                        Require(receiver.IsReviveProtected && !presenter.IsPlaying && !presenter.HudFlash.enabled &&
                            !presenter.HitOverlay.enabled && !(bool)Get(camera, "_hitActive"),
                            "Revive protection must replace old hit feedback without shaking"); checks++;
                        Require(!receiver.TryReceiveDamage(in damage) &&
                            Mathf.Approximately(health.CurrentHealth, hp - 1f) && presenter.PlayedCount == before + 1,
                            "Revive-protected hit must not count as accepted damage"); checks++;
                        receiver.ResetDamageGate();
                        Require(!receiver.IsInvulnerable && !receiver.IsReviveProtected && !presenter.IsPlaying &&
                            !(bool)Get(camera, "_hitActive"), "Gate reset left protection or camera state"); checks++;

                        var interceptor = new AbsorbDamage();
                        Require(receiver.RegisterDamageInterceptor(interceptor), "Temporary interceptor registration");
                        try
                        {
                            Require(receiver.TryReceiveDamage(in damage) && interceptor.Calls == 1 &&
                                Mathf.Approximately(health.CurrentHealth, hp - 1f) &&
                                presenter.PlayedCount == before + 1 && !presenter.IsPlaying && !receiver.IsInvulnerable &&
                                !(bool)Get(camera, "_hitActive"),
                                "Absorbed damage must not trigger HP-hit flash, invulnerability, or camera shake"); checks++;
                        }
                        finally { receiver.UnregisterDamageInterceptor(interceptor); }

                        // 不消耗第二点生命：补回再验证 ResetDamageGate 对正在播放的真实受击立即清理。
                        Require(health.RestoreCheckpointHealth(health.MaximumHealth), "Restore non-lethal health");
                        Require(receiver.TryReceiveDamage(in damage) && presenter.IsPlaying, "Second real non-lethal hit");
                        receiver.ResetDamageGate();
                        Require(!presenter.IsPlaying && !presenter.HudFlash.enabled && !presenter.HitOverlay.enabled &&
                            !(bool)Get(camera, "_hitActive") && camera.PresentationOffset == Vector3.zero,
                            "ResetDamageGate must immediately clear visible hit feedback and camera"); checks++;

                        var invalid = new DamagePacket(0f, Vector2.zero, Vector2.zero, null);
                        uint beforeInvalid = presenter.PlayedCount;
                        float hpBeforeInvalid = health.CurrentHealth;
                        Require(!receiver.TryReceiveDamage(in invalid) && presenter.PlayedCount == beforeInvalid &&
                            Mathf.Approximately(health.CurrentHealth, hpBeforeInvalid), "Zero damage triggered feedback"); checks++;
                    }
                }
                return "PASS: " + checks + " real local-hit checks (DS/HS × both local roles). Actual non-lethal HP loss, exactly one presenter, HUD/overlay/protection, own-role-only camera, duplicate invulnerability, revive protection, absorbed/zero damage, immediate reset. " +
                    (preferredShake > 0f ? "Nonzero shake preference verified." : "Shake is disabled by existing user preference; no preference was modified.") +
                    " HP, damage-gate, role/commands, presentation, camera and time state restored; lethal/downed transitions not exercised.";
            }
            finally
            {
                Set(assignment, "<CurrentLocalPlayerRole>k__BackingField", oldRole);
                Set(assignment, "<CurrentLocalPlayerActor>k__BackingField", oldActor);
                for (int i = 0; i < presenters.Length; i++)
                    Set(presenters[i].Actor.CommandDispatcher, "_commandSource", oldCommands[i]);
                foreach (var state in states) state.Restore();
                for (int i = 0; i < cameraFields.Length; i++) Set(camera, cameraFields[i], oldCamera[i]);
                camera.transform.position = oldCameraPosition;
                Time.timeScale = scale;
            }
        }

        private static object Get(object owner, string name) => owner.GetType().GetField(name, PRIVATE).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, PRIVATE).SetValue(owner, value);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
