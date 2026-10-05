using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>同伴行为调参；与角色自身的伤害、冷却和碰撞配置分离。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/AI/Companion Tactics")]
    public sealed class CompanionTacticsConfig : ScriptableObject
    {
        [Header("感知与反应")]
        public LayerMask PerceptionLayers;
        [Min(1)] public int QueryCapacity;
        [Min(0.01f)] public float DecisionInterval;
        [Min(0.1f)] public float PerceptionRadius;
        [Min(0.01f)] public float PredictionSeconds;
        [Min(0)] public float SafetyPadding;
        [Header("目标评分")]
        public float ChargingBonus;
        public float NearbyThreatWeight;
        public float WoundedBonus;
        public float DistanceCost;
        public float TargetStickiness;
        [Min(0.1f)] public float NearbyRadius;
        [Min(0.1f)] public float AttackRange;
        [Header("移动与风险")]
        public Vector2 FormationOffset;
        [Min(0.01f)] public float ArrivalRadius;
        public float DangerCost;
        public float DirectionChangeCost;
        public float RescueDangerLimit;
        public float EmergencyDanger;
        [Range(0, 1)] public float LowHealthFraction;
        [Header("角色策略")]
        public float GuardDangerThreshold;
        public float MeleeEnterRange;
        public float MeleeAttackRange;
        [Min(1)] public int MeleeClusterCount;
        public float MeleeIdleExitSeconds;
        public float SkillRetrySeconds;
        [Min(0), Tooltip("HS冷却结束后，额外等待多久再开始下一轮点选。只影响人机。")]
        public float HarnessAimRestSeconds;

        [Header("动态障碍导航（不读取迷宫生成路线）")]
        [Min(1)] public int ObstacleCapacity = 256;
        [Min(0.1f)] public float NavigationCellSize = 0.36f;
        [Min(0)] public float NavigationPadding = 0.06f;
        [Min(0.1f)] public float NavigationPredictionSeconds = 1.2f;
        [Min(0.05f)] public float NavigationReplanSeconds = 0.35f;
        [Min(0)] public float NavigationGoalHysteresis = 0.35f;
        [Min(0.1f)] public float NavigationStuckSeconds = 1.2f;
        [Min(16)] public int NavigationMaxExpandedNodes = 768;

        public bool IsValid => QueryCapacity > 0 && DecisionInterval > 0 &&
            PerceptionLayers.value != 0 && PerceptionRadius > 0 && PredictionSeconds > 0 &&
            NearbyRadius > 0 && AttackRange > 0 && ArrivalRadius > 0 && SkillRetrySeconds > 0 &&
            MeleeAttackRange > 0 && MeleeEnterRange > 0 && ObstacleCapacity > 0 &&
            NavigationCellSize >= 0.1f && NavigationPadding >= 0 && NavigationPredictionSeconds > 0 &&
            NavigationReplanSeconds > 0 && NavigationGoalHysteresis >= 0 &&
            NavigationStuckSeconds > 0 && NavigationMaxExpandedNodes >= 16;
    }
}
