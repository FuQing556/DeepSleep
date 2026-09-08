using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 敌人生命周期只依赖这份运动契约，不了解横穿、追逐或驻停细节。
    /// </summary>
    public abstract class EnemyMotor2D : MonoBehaviour
    {
        public event Action<EnemyMotor2D> ExitedPlayfield;

        public abstract bool IsRunning { get; }
        public abstract Vector2 TravelDirection { get; }

        public abstract void Begin(
            Vector2 spawnPosition,
            in EnemySpawnVariation2D variation);

        public abstract void Stop();

        /// <summary>攻击决策之前更新目标；不提交位移。</summary>
        public virtual void PrepareSimulation(float deltaTime) { }

        /// <summary>攻击更新之后提交本刻位移，保证蓄力锁移动当刻生效。</summary>
        public abstract void Simulate(float deltaTime);

        public abstract bool TryValidateConfiguration(out string reason);

        protected void NotifyExitedPlayfield()
        {
            ExitedPlayfield?.Invoke(this);
        }
    }
}
