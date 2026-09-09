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

        public bool IsValid => QueryCapacity > 0 && DecisionInterval > 0 &&
            PerceptionLayers.value != 0 && PerceptionRadius > 0 && PredictionSeconds > 0 &&
            NearbyRadius > 0 && AttackRange > 0 && ArrivalRadius > 0 && SkillRetrySeconds > 0 &&
            MeleeAttackRange > 0 && MeleeEnterRange > 0;
    }
}
