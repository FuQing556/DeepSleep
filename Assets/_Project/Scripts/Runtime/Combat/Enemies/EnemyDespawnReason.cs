namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 敌人请求离开战场的原因。对象池据此决定掉落与回收表现。
    /// </summary>
    public enum EnemyDespawnReason : byte
    {
        Defeated = 0,
        ContactImpact = 1,
        ExitedPlayfield = 2
    }
}
