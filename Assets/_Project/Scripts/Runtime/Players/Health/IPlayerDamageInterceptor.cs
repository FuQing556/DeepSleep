using DeepSleep.Runtime.Combat.Damage;

namespace DeepSleep.Runtime.Players.Health
{
    /// <summary>在玩家生命值变化前处理伤害；返回true表示该伤害已被完整吸收。</summary>
    public interface IPlayerDamageInterceptor
    {
        bool TryIntercept(
            PlayerDamageReceiver2D target,
            in DamagePacket damage);
    }
}
