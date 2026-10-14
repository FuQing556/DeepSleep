using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    public sealed partial class HarnessTerminalLaserController
    {
        [Header("真人辅助选敌（场景显式绑定）")]
        public CombatPerceptionRegistry2D Perception;
        public ClaudePermissionModule2D Permissions;
        public ClaudeEnergyPattern2D[] EnergyBalls = Array.Empty<ClaudeEnergyPattern2D>();
        public PlayerReviveActionChannel ReviveChannel;
        private Func<Collider2D, int> _priority;
        private Predicate<Collider2D> _automaticEligibility;
        private Collider2D _manualPreference;
        private DamageHitbox2D _manualPreferenceHitbox;
        private float _scanWait;

        private void InitializeAssistance()
        { _priority = TargetPriority; _automaticEligibility = IsAutomaticCandidate; }

        private int TargetPriority(Collider2D target)
        {
            // 两档分类：明确的当前机制/高速威胁优先；同档仍按点击距离，不无限叠权重。
            if (Permissions != null)
                foreach (var book in Permissions.Books)
                    if (book.IsOpen && book.Shape == target) return 1;
            foreach (var ball in EnergyBalls)
                if (ball.State == ClaudeEnergyState.Flying && ball.Shape == target) return 1;
            return Perception != null && Perception.TryResolve(target, out var body) &&
                (body.IsCharging || body.QuickApp != null || body.DownloadCharge != null) ? 1 : 0;
        }

        private bool IsAutomaticCandidate(Collider2D target)
        {
            if (!IsSelectableDamageTarget(target) || !_playfield.WorldBounds.Contains(target.bounds.center)) return false;
            foreach (var ball in EnergyBalls)
            {
                if (ball.Shape != target) continue;
                // 自动操作不在队友脸上引爆球；手动明确点选仍由玩家决定。
                for (int i = 0; i < ball.TargetShapes.Length; i++)
                    if (ball.TargetLives[i].State == PlayerLifeState.Alive &&
                        Vector2.Distance(ball.TargetShapes[i].ClosestPoint(ball.Position), ball.Position) <= ball.ExplosionRadius) return false;
            }
            return true;
        }

        private void TryAutomaticSelection(float dt)
        {
            if (_burstActive || HasAimPoint || State != HarnessTerminalLaserState.Ready) return;
            // 站在倒地队友旁且无手动攻击意图时，让已有救援系统起步；不是新增自动救人。
            if (ReviveChannel != null && (ReviveChannel.IsChanneling || ReviveChannel.OfferedTarget != null)) return;
            _scanWait -= Mathf.Max(0, dt);
            if (_scanWait > 0) return;
            _scanWait = _config.AutomaticScanInterval;
            if (_manualPreference != null && IsSelectableDamageTarget(_manualPreference))
            { TrySelectTarget(_manualPreference); return; }
            ClearManualPreference();
            if (_targetFinder.TryFindNearPoint(BeamOriginPosition, BeamOriginPosition,
                _config.MaximumLockDistance, _config.MaximumLockDistance, _automaticEligibility, out var target, _priority))
                TrySelectTarget(target);
        }

        private void RememberManualTarget(Collider2D target)
        {
            ClearManualPreference();
            _manualPreference = target; _manualPreferenceHitbox = _selectedTargetHitbox;
            if (_manualPreferenceHitbox != null) _manualPreferenceHitbox.BecameUnavailable += ForgetUnavailableTarget;
        }
        private void ForgetUnavailableTarget(DamageHitbox2D ignored) => ClearManualPreference();
        private void ClearManualPreference()
        {
            if (_manualPreferenceHitbox != null) _manualPreferenceHitbox.BecameUnavailable -= ForgetUnavailableTarget;
            _manualPreferenceHitbox = null; _manualPreference = null;
        }
    }
}
