using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams
{
    /// <summary>
    /// 一次开火中某条激光最终采用的世界空间几何与伤害数据。
    /// 渲染和命中查询必须读取同一个实例，不能分别重新计算。
    /// </summary>
    public readonly struct BeamLaneSnapshot
    {
        public BeamLaneSnapshot(
            int laneIndex,
            Vector2 origin,
            Vector2 direction,
            float length,
            float width,
            float primaryTargetDamage,
            float piercingDamage,
            int visualLayers = 1,
            bool isBranch = false,
            DeepSleep.Runtime.Combat.Damage.IDamageReceiver branchSource = null)
        {
            LaneIndex = laneIndex;
            Origin = origin;
            Direction = direction.normalized;
            Length = length;
            Width = width;
            PrimaryTargetDamage = primaryTargetDamage;
            PiercingDamage = piercingDamage;
            VisualLayers = Mathf.Clamp(visualLayers, 1, 3);
            IsBranch = isBranch;
            BranchSource = branchSource;
        }

        public int LaneIndex { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public float Length { get; }
        public float Width { get; }
        public float PrimaryTargetDamage { get; }
        public float PiercingDamage { get; }
        public int VisualLayers { get; }
        public bool IsBranch { get; }
        public DeepSleep.Runtime.Combat.Damage.IDamageReceiver BranchSource { get; }

        public Vector2 End => Origin + Direction * Length;

        public Vector2 Center => Origin + Direction * (Length * 0.5f);

        public float RotationDegrees =>
            Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;

        public bool IsValid =>
            LaneIndex >= 0 &&
            Direction.sqrMagnitude > 0f &&
            Length > 0f &&
            Width > 0f &&
            PrimaryTargetDamage > 0f &&
            PiercingDamage > 0f;
    }
}
