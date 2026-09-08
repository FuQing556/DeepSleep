namespace DeepSleep.Runtime.Simulation
{
    /// <summary>由所属调度器每个固定刻调用一次，不自行读取帧时钟。</summary>
    public interface IFixedSimulationStep
    {
        void Simulate(float deltaTime);
    }
}
