namespace DeepSleep.Runtime.UI.Combat
{
    public enum HudActionPhase { Ready, Active, Cooldown, Calibrating, Melee }

    /// <summary>视图读取的数据值；未来远端状态适配器可提供同样的快照，不把网络SDK引入UI。</summary>
    public readonly struct PlayerCombatHudSnapshot
    {
        public readonly float Health, MaximumHealth, ProtectionSeconds, ReviveProgress, SkillSeconds, WeaponSeconds;
        public readonly bool Downed, BeingRevived, ReviveProtection, IsLocal;
        public readonly int Charges;
        public readonly HudActionPhase SkillPhase, WeaponPhase;

        public PlayerCombatHudSnapshot(float health, float maximumHealth, bool downed, bool beingRevived,
            float reviveProgress, float protectionSeconds, bool reviveProtection, bool isLocal,
            HudActionPhase skillPhase, float skillSeconds, int charges, HudActionPhase weaponPhase, float weaponSeconds)
        {
            Health = health; MaximumHealth = maximumHealth; Downed = downed; BeingRevived = beingRevived;
            ReviveProgress = reviveProgress; ProtectionSeconds = protectionSeconds; ReviveProtection = reviveProtection;
            IsLocal = isLocal; SkillPhase = skillPhase; SkillSeconds = skillSeconds; Charges = charges;
            WeaponPhase = weaponPhase; WeaponSeconds = weaponSeconds;
        }
    }

    public interface IPlayerCombatHudSource
    {
        bool TryRead(out PlayerCombatHudSnapshot state);
    }
}
