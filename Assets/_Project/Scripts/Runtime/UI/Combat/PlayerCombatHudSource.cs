using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Revive;
using UnityEngine;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>本地玩法→HUD只读适配。计时权威仍在原玩法组件，UI没有第二套倒计时。</summary>
    public sealed class PlayerCombatHudSource : MonoBehaviour, IPlayerCombatHudSource
    {
        public PlayerRole Role;
        public PlayerControlAssignment Assignment;
        public HealthComponent Health;
        public PlayerLifeStateController2D Life;
        public PlayerDamageReceiver2D DamageReceiver;
        public PlayerReviveCoordinator2D Revive;
        public DeepSeekRiceGuardController Guard;
        public HarnessMeleeController Melee;
        public HarnessTerminalLaserController Laser;

        public bool IsValid => Assignment != null && Health != null && Life != null && DamageReceiver != null &&
            Revive != null && (Role == PlayerRole.DeepSeek ? Guard != null : Melee != null && Laser != null);

        public bool TryRead(out PlayerCombatHudSnapshot state)
        {
            state = default;
            if (!IsValid) return false;
            bool ds = Role == PlayerRole.DeepSeek;
            bool active = ds ? Guard.IsActive : Melee.IsMelee;
            float cooldown = ds ? (float)Guard.CooldownRemaining : Melee.CooldownRemaining;
            HudActionPhase phase = active ? HudActionPhase.Active :
                cooldown > 0 ? HudActionPhase.Cooldown : HudActionPhase.Ready;
            var weaponPhase = ds ? HudActionPhase.Ready : Melee.IsMelee ? HudActionPhase.Melee : Laser.State switch
            {
                HarnessTerminalLaserState.Calibrating => HudActionPhase.Calibrating,
                HarnessTerminalLaserState.Cooldown => HudActionPhase.Cooldown,
                _ => HudActionPhase.Ready
            };
            state = new PlayerCombatHudSnapshot(Health.CurrentHealth, Health.MaximumHealth,
                Life.State == PlayerLifeState.Downed, Revive.IsReviving, Revive.Progress01,
                DamageReceiver.RemainingInvulnerabilitySeconds, DamageReceiver.IsReviveProtected,
                Assignment.CurrentLocalPlayerRole == Role, phase,
                active ? ds ? (float)Guard.RemainingSeconds : Melee.ModeRemaining : cooldown,
                ds ? Guard.RemainingCharges : -1, weaponPhase, ds ? 0 : Laser.RemainingStateSeconds);
            return true;
        }
    }
}
