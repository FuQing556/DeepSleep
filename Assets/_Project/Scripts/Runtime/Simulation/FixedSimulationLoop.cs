using DeepSleep.Runtime.Players.Commands;
using UnityEngine;

namespace DeepSleep.Runtime.Simulation
{
    /// <summary>
    /// 驱动一局游戏的固定模拟刻。该组件每个对局场景只配置一个。
    /// </summary>
    public sealed class FixedSimulationLoop : MonoBehaviour
    {
        [SerializeField] private PlayerCommandDispatcher[] playerDispatchers;

        private uint currentTick;
        private bool isInitialized;

        public uint CurrentTick => currentTick;

        private void Awake()
        {
            if (playerDispatchers == null || playerDispatchers.Length == 0)
            {
                Debug.LogError(
                    $"[{nameof(FixedSimulationLoop)}] 至少需要一个玩家命令分发器。",
                    this);
                enabled = false;
                return;
            }

            for (int index = 0; index < playerDispatchers.Length; index++)
            {
                if (playerDispatchers[index] != null)
                {
                    continue;
                }

                Debug.LogError(
                    $"[{nameof(FixedSimulationLoop)}] " +
                    $"第 {index} 个玩家命令分发器为空。",
                    this);
                enabled = false;
                return;
            }

            isInitialized = true;
        }

        private void FixedUpdate()
        {
            if (!isInitialized)
            {
                return;
            }

            unchecked
            {
                currentTick++;
            }

            float deltaTime = Time.fixedDeltaTime;

            for (int index = 0; index < playerDispatchers.Length; index++)
            {
                playerDispatchers[index].Simulate(currentTick, deltaTime);
            }
        }
    }
}
