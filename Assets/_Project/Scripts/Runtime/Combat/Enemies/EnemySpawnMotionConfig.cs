using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 刷怪器只向运动配置索取一次可复现的出生快照。
    /// 具体随机哪些参数由敌人的运动类型决定。
    /// </summary>
    public abstract class EnemySpawnMotionConfig : ScriptableObject
    {
        public abstract EnemySpawnVariation2D SampleVariation(
            System.Random random,
            Vector2 travelDirection);

        public abstract bool TryValidate(out string reason);
    }
}
