namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>战斗段除击杀数之外的显式完成条件。</summary>
    public interface IChapterCombatObjective
    {
        bool IsRequiredForSegment(int segmentNumber);
        bool IsComplete { get; }
        void ResetForSegment(int segmentNumber);
    }
}
