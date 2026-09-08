using UnityEngine;

namespace DeepSleep.Runtime.Combat.Health
{
    /// <summary>
    /// 最简单的耗尽处理策略：生命归零时停用对象。
    /// 正式敌人以后可替换为掉落、动画或对象池回收策略。
    /// </summary>
    public sealed class DeactivateOnHealthDepleted : MonoBehaviour
    {
        [SerializeField] private HealthComponent _health;

        private void OnEnable()
        {
            if (_health == null)
            {
                Debug.LogError(
                    $"[{nameof(DeactivateOnHealthDepleted)}] " +
                    "必须显式配置生命组件。",
                    this);
                enabled = false;
                return;
            }

            _health.Depleted += HandleDepleted;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Depleted -= HandleDepleted;
            }
        }

        private static void HandleDepleted(HealthComponent health)
        {
            health.gameObject.SetActive(false);
        }
    }
}
