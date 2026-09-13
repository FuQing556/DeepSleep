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
            out string reason,
            int additionalPorts = 0,
            Vector2? convergencePoint = null,
            int visualLayers = 1)
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

            if (!IsFinite(sourceOrigin.x) || !IsFinite(sourceOrigin.y) ||
                !IsFinite(aimDirection.x) || !IsFinite(aimDirection.y) ||
                !IsFinite(damageMultiplier) || !IsFinite(widthMultiplier) ||
                (convergencePoint.HasValue && (!IsFinite(convergencePoint.Value.x) || !IsFinite(convergencePoint.Value.y))) ||
                additionalPorts < 0 || additionalPorts > 127 - config.BaseLaneCount ||
                aimDirection.sqrMagnitude <= 0f ||
                damageMultiplier <= 0f || widthMultiplier <= 0f)
            {
                reason = "瞄准方向不能为空。";
                return false;
            }

            Vector2 normalizedAimDirection = aimDirection.normalized;
            Vector2 lateralAxis = new Vector2(
                -normalizedAimDirection.y,
                normalizedAimDirection.x);
            var lanes = new BeamLaneSnapshot[config.BaseLaneCount + additionalPorts];

            for (int index = 0; index < lanes.Length; index++)
            {
                BeamLaneDefinition definition = config.GetBaseLane(Mathf.Min(index, config.BaseLaneCount - 1));
                Vector2 laneOrigin =
                    sourceOrigin + lateralAxis * (additionalPorts > 0
                        ? (index - (lanes.Length - 1) * .5f) * config.PortSpacing
                        : definition.LateralOffset);
                Vector2 laneDirection = Rotate(
                    normalizedAimDirection,
                    definition.AngleOffsetDegrees);
                if (convergencePoint.HasValue && (convergencePoint.Value - laneOrigin).sqrMagnitude > .000001f)
                    laneDirection = (convergencePoint.Value - laneOrigin).normalized;

                if (!playfield.TryGetRayExitDistanceAfterIntersection(
                        laneOrigin,
                        laneDirection,
                        out float laneLength))
                {
                    // Front/extra muzzles can legitimately sit beyond a movement boundary.
                    // An outward or parallel shot has no rectangle intersection, but is still
                    // a valid finite shot. Keep its real origin/direction for both VFX and hits.
                    // Use the playfield diagonal as its range, without moving any boundary.
                    laneLength = playfield.WorldBounds.size.magnitude;
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
                    primaryDamage * config.PiercingDamageMultiplier, visualLayers);
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

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
