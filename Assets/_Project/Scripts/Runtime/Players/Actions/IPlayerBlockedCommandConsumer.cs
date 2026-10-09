namespace DeepSleep.Runtime.Players.Actions
{
    /// <summary>阻止新攻击时取消未发射的意图；不取消已经提交的弹体或技能。</summary>
    public interface IPlayerBlockedCommandConsumer
    {
        void ConsumeBlockedCommand(float deltaTime);
    }
}
