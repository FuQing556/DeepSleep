namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>接收命中产生的短暂运动反馈，不参与伤害和生命结算。</summary>
    public interface IHitMotionReceiver2D
    {
        void ApplyHitMotion(HitMotionKind kind);
    }
}
