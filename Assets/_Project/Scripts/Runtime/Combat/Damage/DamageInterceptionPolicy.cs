namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>明确一次伤害能否被护盾类系统拦截；未声明时按不可拦截处理。</summary>
    public enum DamageInterceptionPolicy
    {
        Unspecified = 0,
        Blockable = 1,
        BypassesProtection = 2
    }
}
