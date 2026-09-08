using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 把场景级服务注入池化敌人。避免敌人使用 Find、静态单例或自建对象池。
    /// </summary>
    public abstract class EnemyActorRuntimeBinder2D : MonoBehaviour
    {
        public abstract bool TryBind(
            EnemyActor2D actor,
            out string reason);
    }
}
