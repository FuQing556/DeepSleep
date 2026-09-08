using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 敌人对象池容量。容量与刷怪节奏分离，避免调关卡密度时
    /// 意外改变内存预算。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_EnemyPool_",
        menuName = "DeepSleep/配置/敌人/对象池")]
    public sealed class EnemyPoolConfig : ScriptableObject
    {
        [SerializeField, Min(0)] private int _initialCapacity = 6;
        [SerializeField, Min(1)] private int _maximumCapacity = 12;

        public int InitialCapacity => _initialCapacity;
        public int MaximumCapacity => _maximumCapacity;

        public bool TryValidate(out string reason)
        {
            if (_initialCapacity < 0)
            {
                reason = "初始容量不能小于 0。";
                return false;
            }

            if (_maximumCapacity <= 0)
            {
                reason = "最大容量必须大于 0。";
                return false;
            }

            if (_initialCapacity > _maximumCapacity)
            {
                reason = "初始容量不能大于最大容量。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
