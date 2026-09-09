using DeepSleep.Runtime.Combat.Damage;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard
{
    public sealed partial class DeepSeekRiceGuardController
    {
        private bool _replica, _replicaActive, _replicaWarning;
        private int _replicaCharges;
        private float _replicaSeconds, _replicaCooldown;
        public void ApplyReplicaState(bool active, bool warning, int charges, float seconds, float cooldown)
        {
            if (enabled) return;
            bool wasActive = IsActive;
            _replica = true; _replicaActive = active; _replicaWarning = warning;
            _replicaCharges = charges; _replicaSeconds = seconds; _replicaCooldown = cooldown;
            if (!wasActive && active) Activated?.Invoke();
            if (wasActive && !active) Ended?.Invoke();
        }
        /// <summary>只重建破碗表现，不提交 TryIntercept，不再次格挡或扣血。</summary>
        public void PresentReplicaBlock(in DamagePacket packet, int remaining)
        {
            if (!enabled) ChargeConsumed?.Invoke(null, packet, remaining);
        }
    }
}
