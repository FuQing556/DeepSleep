using DeepSleep.Runtime.Combat.Projectiles;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    /// <summary>
    /// 给对象池创建的数据蛇注入当前场景唯一的敌方弹体池。
    /// </summary>
    public sealed class DataCrawlerSnakeRuntimeBinder2D :
        EnemyActorRuntimeBinder2D
    {
        [SerializeField] private EnemyProjectilePool2D _projectilePool;

        public override bool TryBind(
            EnemyActor2D actor,
            out string reason)
        {
            if (_projectilePool == null)
            {
                reason = "未配置场景级敌方弹体池。";
                return false;
            }

            if (actor == null ||
                !actor.TryGetComponent(
                    out DataCrawlerSnakeAttackController2D attack))
            {
                reason = "目标敌人没有数据蛇攻击控制器。";
                return false;
            }

            attack.BindProjectilePool(_projectilePool);
            reason = string.Empty;
            return true;
        }
    }
}
