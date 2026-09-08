namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>
    /// 能接收由模拟层裁决的伤害。碰撞体通过 DamageHitbox2D
    /// 显式转发到该接口，避免弹体猜测目标层级结构。
    /// </summary>
    public interface IDamageReceiver
    {
        bool CanReceiveDamage { get; }

        bool TryReceiveDamage(in DamagePacket damage);
    }
}
