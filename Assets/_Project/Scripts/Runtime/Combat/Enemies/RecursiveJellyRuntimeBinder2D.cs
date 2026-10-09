using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>注入后代池，并为大体生成预留完整分裂树容量，避免后代被吞或无限堆积。</summary>
    public sealed class RecursiveJellyRuntimeBinder2D : EnemyActorRuntimeBinder2D, IEnemySpawnAdmission2D
    {
        public EnemyActorPool2D Children;
        public CoopSessionController Session;
        [Header("仅大体刷怪入口配置")]
        public EnemyActorPool2D Large, Medium, Small;
        public EnemyPoolConfig MediumCapacity, SmallCapacity;
        public int MediumPerLarge, SmallPerMedium;

        public bool CanSpawn => Large != null && Medium != null && Small != null &&
            Medium.ActiveCount + (Large.ActiveCount + 1) * MediumPerLarge <= Medium.MaximumCapacity &&
            Small.ActiveCount + (Medium.ActiveCount + (Large.ActiveCount + 1) * MediumPerLarge) * SmallPerMedium <= Small.MaximumCapacity;

        public override bool TryBind(EnemyActor2D actor, out string reason)
        {
            if (actor == null || !actor.TryGetComponent(out RecursiveJelly2D jelly) || Session == null ||
                (jelly.Config.SplitDirections.Length > 0 && Children == null))
            { reason = "递归池缺少专属运动、会话或后代池。"; return false; }
            jelly.Bind(Children, Session);
            reason = string.Empty; return true;
        }
    }
}
