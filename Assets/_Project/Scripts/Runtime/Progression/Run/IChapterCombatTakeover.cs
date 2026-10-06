namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>遭遇接管后替代普通计时/击杀目标，但不接管玩家倒地失败和章节结算。</summary>
    public interface IChapterCombatTakeover : IChapterCombatObjective
    {
        bool HasTakenOver { get; }
        int DisplaySeconds { get; }
        string DisplayTitle { get; }
        string ObjectiveText { get; }
    }
}
