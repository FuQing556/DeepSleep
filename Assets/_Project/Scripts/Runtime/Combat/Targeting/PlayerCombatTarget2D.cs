using System.Collections.Generic;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Targeting
{
    /// <summary>
    /// 玩家可被敌方锁定的显式标记。登记发生在启用/禁用时，
    /// 敌人无需在场景中反复搜索 GameObject。
    /// </summary>
    public sealed class PlayerCombatTarget2D : MonoBehaviour
    {
        private static readonly HashSet<PlayerCombatTarget2D> Active = new();

        [SerializeField] private Transform _aimPoint;
        [SerializeField] private PlayerLifeStateController2D _lifeState;

        public Vector2 Position => _aimPoint.position;
        public bool IsTargetable =>
            isActiveAndEnabled &&
            _lifeState != null &&
            _lifeState.State == PlayerLifeState.Alive;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerCombatTarget2D)}] " +
                    $"玩家锁定目标装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_aimPoint == null)
            {
                reason = "未配置瞄准点。";
                return false;
            }

            if (_lifeState == null)
            {
                reason = "未配置玩家生命状态。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryFindNearest(
            Vector2 origin,
            out PlayerCombatTarget2D nearest)
        {
            nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;

            foreach (PlayerCombatTarget2D candidate in Active)
            {
                if (candidate == null || !candidate.IsTargetable)
                {
                    continue;
                }

                float sqrDistance =
                    (candidate.Position - origin).sqrMagnitude;

                if (sqrDistance >= nearestSqrDistance)
                {
                    continue;
                }

                nearest = candidate;
                nearestSqrDistance = sqrDistance;
            }

            return nearest != null;
        }
    }
}
