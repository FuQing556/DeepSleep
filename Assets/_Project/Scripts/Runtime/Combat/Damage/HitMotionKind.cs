namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>伤害成功结算后，可选提交给目标运动层的命中反馈语义。</summary>
    public enum HitMotionKind : byte
    {
        DeepSeekSlow,
        HarnessStop
    }
}
