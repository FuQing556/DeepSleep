using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// 把本次瞄准方向、静态配置与逻辑区域冻结成唯一开火快照。
    /// 后续渲染器和伤害解析器只消费快照。
    /// </summary>
    public static class HarnessTerminalLaserSnapshotFactory
    {
        public static bool TryCreate(
            uint sequence,
            Vector2 sourceOrigin,
            Vector2 aimDirection,
            CombatPlayfieldConfig playfield,
            HarnessTerminalLaserConfig config,
            float damageMultiplier,
            float widthMultiplier,
            out BeamFireSnapshot snapshot,
            out string reason)
        {
            snapshot = null;

            if (playfield == null)
            {
                reason = "未配置逻辑战斗区域。";
                return false;
            }

            if (!playfield.TryValidate(out reason))
            {
                return false;
            }

            if (config == null)
            {
                reason = "未配置 Harness 终端激光参数。";
                return false;
            }

            if (!config.TryValidate(out reason))
            {
                return false;
            }

            if (aimDirection.sqrMagnitude <= 0f ||
                damageMultiplier <= 0f || widthMultiplier <= 0f)
            {
                reason = "瞄准方向不能为空。";
                return false;
            }

            Vector2 normalizedAimDirection = aimDirection.normalized;
            Vector2 lateralAxis = new Vector2(
                -normalizedAimDirection.y,
                normalizedAimDirection.x);
            var lanes = new BeamLaneSnapshot[config.BaseLaneCount];

            for (int index = 0; index < lanes.Length; index++)
            {
                BeamLaneDefinition definition = config.GetBaseLane(index);
                Vector2 laneOrigin =
                    sourceOrigin + lateralAxis * definition.LateralOffset;
                Vector2 laneDirection = Rotate(
                    normalizedAimDirection,
                    definition.AngleOffsetDegrees);

                if (!playfield.TryGetRayExitDistanceAfterIntersection(
                        laneOrigin,
                        laneDirection,
                        out float laneLength))
                {
                    reason =
                        $"束线 {index} 的起点不在逻辑区域内，或无法与区域边界相交。";
                    return false;
                }

                float primaryDamage =
                    config.PrimaryTargetDamage * damageMultiplier *
                    definition.DamageMultiplier;

                lanes[index] = new BeamLaneSnapshot(
                    index,
                    laneOrigin,
                    laneDirection,
                    laneLength * config.BeamLengthMultiplier,
                    config.BaseBeamWidth * widthMultiplier *
                    definition.WidthMultiplier,
                    primaryDamage,
                    primaryDamage * config.PiercingDamageMultiplier);
            }

            snapshot = new BeamFireSnapshot(
                sequence,
                sourceOrigin,
                normalizedAimDirection,
                config.TargetLayers,
                lanes);

            if (!snapshot.IsValid)
            {
                snapshot = null;
                reason = "生成的激光开火快照无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static Vector2 Rotate(
            Vector2 direction,
            float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);

            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }
    }
}
