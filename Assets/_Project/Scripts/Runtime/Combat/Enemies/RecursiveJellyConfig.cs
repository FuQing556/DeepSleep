using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    [CreateAssetMenu(fileName = "CFG_RecursiveJelly_", menuName = "DeepSleep/配置/敌人/递归果冻")]
    public sealed class RecursiveJellyConfig : EnemySpawnMotionConfig
    {
        [Header("沉重追逐")]
        [Min(.01f)] public float Speed;
        [Min(.02f)] public float RetargetSeconds;
        [Min(0)] public float DespawnMargin;
        [Header("一步蠕动：前伸 → 后部收回 → 停顿")]
        [Min(.01f)] public float StrideDistance;
        [Min(.01f)] public float StrideSeconds;
        [Range(.05f, .8f)] public float ReachPhase01;
        [Range(.05f, .8f)] public float PullPhase01;
        [Header("分裂：大四向，中左右，小空数组")]
        public Vector2[] SplitDirections = System.Array.Empty<Vector2>();
        [Min(0)] public float SplitOffset;
        [Min(0)] public float SeparationSpeed;
        [Min(.01f)] public float SeparationSeconds;
        [Min(0)] public float BirthProtectionSeconds;
        [Header("仅贴图变形，不改变物理根")]
        [Range(0, .3f)] public float MoveStretch;
        [Range(0, .3f)] public float HitSquash;
        [Min(.01f)] public float HitRecoverSeconds;
        [Range(0, .3f)] public float BirthSquash;
        [Range(.1f, 1f)] public float Opacity;

        public override EnemySpawnVariation2D SampleVariation(System.Random random, Vector2 travelDirection)
            => new(travelDirection, Speed, 0, 0);

        public override bool TryValidate(out string reason)
        {
            if (!float.IsFinite(Speed) || Speed <= 0 || RetargetSeconds <= 0 || DespawnMargin < 0 ||
                SplitDirections == null || SplitOffset < 0 || SeparationSpeed < 0 || SeparationSeconds <= 0 ||
                BirthProtectionSeconds < 0 || HitRecoverSeconds <= 0 ||
                StrideDistance <= 0 || StrideSeconds <= 0 || ReachPhase01 < .05f || PullPhase01 < .05f ||
                ReachPhase01 + PullPhase01 >= 1 ||
                MoveStretch < 0 || MoveStretch > .3f || HitSquash < 0 || HitSquash > .3f ||
                BirthSquash < 0 || BirthSquash > .3f || Opacity < .1f || Opacity > 1)
            { reason = "递归运动、分裂和变形参数无效。"; return false; }
            foreach (var direction in SplitDirections)
                if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || Mathf.Abs(direction.x) < .001f)
                { reason = "分裂方向须为有限、非零且有横向分量的方向。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
