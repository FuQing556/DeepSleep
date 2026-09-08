namespace DeepSleep.Runtime.Presentation.Effects
{
    /// <summary>
    /// 战斗表现由哪一方的行为产生。
    /// 以来源而不是命中对象分类，避免治疗等非伤害表现无处归属。
    /// </summary>
    public enum CombatEffectSource
    {
        None = 0,
        Player = 1,
        Enemy = 2
    }
}
