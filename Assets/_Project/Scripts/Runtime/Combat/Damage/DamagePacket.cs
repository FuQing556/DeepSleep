using UnityEngine;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>
    /// 一次已经由当前模拟权威确认、等待目标结算的运行时伤害数据。
    /// 它不是网络消息；联网层稍后用稳定 ID 构造相同的数据。
    /// </summary>
    public readonly struct DamagePacket
    {
        public DamagePacket(
            float amount,
            Vector2 hitPoint,
            Vector2 direction,
            GameObject source,
            ulong attackId = 0,
            DamageInterceptionPolicy interceptionPolicy =
                DamageInterceptionPolicy.Unspecified)
        {
            Amount = amount;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0f
                ? direction.normalized
                : Vector2.zero;
            Source = source;
            AttackId = attackId;
            InterceptionPolicy = interceptionPolicy;
        }

        public float Amount { get; }
        public Vector2 HitPoint { get; }
        public Vector2 Direction { get; }
        public GameObject Source { get; }
        public ulong AttackId { get; }
        public DamageInterceptionPolicy InterceptionPolicy { get; }

        public bool IsValid => Amount > 0f;
    }
}
