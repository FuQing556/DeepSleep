namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>生成前的显式容量约束，例如为分裂后代预留空间；不推进日程。</summary>
    public interface IEnemySpawnAdmission2D
    {
        bool CanSpawn { get; }
    }
}
