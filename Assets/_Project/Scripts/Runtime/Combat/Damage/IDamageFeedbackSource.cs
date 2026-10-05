using System;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>实际扣血与生命重置事实；表现不从生命快照差值推测命中。</summary>
    public interface IDamageFeedbackSource
    {
        event Action<DamagePacket> DamageAccepted;
        event Action FeedbackReset;
    }
}
