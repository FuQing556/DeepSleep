namespace DeepSleep.Runtime.Progression.Run
{
    public enum ChapterCombatStopReason : byte
    {
        NaturalClear,
        Failure,
        Settlement,
        SceneExit,
        EncounterTakeover
    }

    /// <summary>由章节战斗域同步结束本段机制；不派发击败事件，也不结算奖励。</summary>
    public interface IChapterCombatLifecycle
    {
        void StopCombat(ChapterCombatStopReason reason);
    }
}
