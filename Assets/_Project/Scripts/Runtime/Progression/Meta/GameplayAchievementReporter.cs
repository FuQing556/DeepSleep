using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Revive;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    /// <summary>把场景内稀疏玩法事件转交给常驻成就服务。</summary>
    public sealed class GameplayAchievementReporter : MonoBehaviour
    {
        [SerializeField] private PlayerReviveCoordinator2D[] _reviveSystems;

        private void Awake()
        {
            if (_reviveSystems == null || _reviveSystems.Length == 0)
            {
                Debug.LogError("[Achievements] 玩法场景未配置复活事件桥。", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            for (int index = 0; index < _reviveSystems.Length; index++)
                _reviveSystems[index].ReviveCompleted += OnReviveCompleted;
        }

        private void OnDisable()
        {
            if (_reviveSystems == null) return;
            for (int index = 0; index < _reviveSystems.Length; index++)
                if (_reviveSystems[index] != null)
                    _reviveSystems[index].ReviveCompleted -= OnReviveCompleted;
        }

        private void OnReviveCompleted(PlayerActor rescuer)
        {
            GameAppRoot.Instance.Achievements.Report(
                AchievementTriggerIds.TeammateRevived);
        }
    }
}
