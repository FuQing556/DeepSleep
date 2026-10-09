using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>路线末端控制：按真实加减速预测本次输入与制动，危险体不可用目标距离抵消。</summary>
    public static class CompanionSteering2D
    {
        private static readonly Vector2[] FixedCandidates = CreateFixedCandidates();

        public static Vector2 Choose(CompanionBattleSensor2D sensor, CompanionTacticsConfig tactics,
            PlayerMotorConfig motor, Vector2 position, Vector2 velocity, Vector2 extent, Vector2 destination,
            Vector2 previousMove, Vector2 colliderOffset)
        {
            Vector2 offset = destination - position;
            float distance = offset.magnitude;
            Vector2 desired = distance <= Mathf.Min(.12f, tactics.ArrivalRadius) ? Vector2.zero :
                offset.normalized * Mathf.Clamp01(distance / (motor.MaximumSpeed * .24f));
            Vector2 result = Vector2.zero;
            float best = float.PositiveInfinity;
            float fixedDelta = Time.fixedDeltaTime;
            float hold = tactics.DecisionInterval + fixedDelta;
            float horizon = Mathf.Max(tactics.PredictionSeconds, hold + motor.MaximumSpeed / motor.Deceleration);
            float ownerRadius = extent.magnitude;
            bool chargeThreat = sensor.HasImminentCharge;
            float bestChargeDanger = float.PositiveInfinity;
            float bestClaudeDanger = float.PositiveInfinity;
            // 目标、制动、八方向全速/半速；慢行允许通过转角而非只在全速和停车间跳变。
            for (int i = 0; i < 18; i++)
            {
                Vector2 candidate = i == 0 ? desired : i == 1 ? Vector2.zero : FixedCandidates[i - 2];
                if (sensor.ObstacleSaturated && candidate != Vector2.zero) continue;
                Vector2 end = position, nextVelocity = velocity;
                float elapsed = 0;
                bool safe = true;
                while (elapsed < horizon)
                {
                    float step = Mathf.Min(fixedDelta, horizon - elapsed);
                    Vector2 intent = elapsed < hold ? candidate : Vector2.zero;
                    Vector2 previous = end;
                    PlayerMovementStep.Calculate(ref end, ref nextVelocity, intent, motor, extent, colliderOffset, step);
                    end += nextVelocity * step;
                    if (!SafeStep(previous + colliderOffset, end + colliderOffset, elapsed, step, extent,
                        tactics.NavigationPadding, sensor.Obstacles, sensor.ObstacleCount)) { safe = false; break; }
                    elapsed += step;
                }
                if (!safe) continue;
                Vector2 averageVelocity = (end - position) / horizon;
                float danger = sensor.Danger(position + colliderOffset, averageVelocity, ownerRadius);
                float chargeDanger = 0;
                if (chargeThreat)
                {
                    chargeDanger = sensor.PredictChargeRisk(position,velocity,extent,colliderOffset,candidate,motor);
                }
                float score = Vector2.Distance(end, destination) + tactics.DangerCost * danger +
                    tactics.DirectionChangeCost * (candidate - previousMove).sqrMagnitude;
                float claudeDanger = sensor.PredictClaudeRisk(position, velocity, extent, colliderOffset, candidate, motor);
                if (sensor.HasClaudeWarning && claudeDanger > bestClaudeDanger + .000001f) continue;
                bool betterClaude = sensor.HasClaudeWarning && claudeDanger < bestClaudeDanger - .000001f;
                // 直线冲撞的碰撞风险优先级高于编队/救援距离，不能为了靠近目的地换取一次撞击。
                if (!betterClaude && chargeThreat && chargeDanger > bestChargeDanger + .000001f) continue;
                if (!betterClaude && (!chargeThreat || Mathf.Abs(chargeDanger - bestChargeDanger) <= .000001f) && score >= best) continue;
                bestClaudeDanger = claudeDanger;
                bestChargeDanger = chargeDanger;
                best = score;
                result = candidate;
            }
            return result;
        }

        private static Vector2[] CreateFixedCandidates()
        {
            var candidates = new Vector2[16];
            for (int i = 0; i < candidates.Length; i++)
            {
                float angle = i % 8 * Mathf.PI / 4;
                candidates[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i >= 8 ? .5f : 1f);
            }
            return candidates;
        }

        private static bool SafeStep(Vector2 from, Vector2 to, float time, float duration, Vector2 extent,
            float padding, CompanionObstacleSnapshot[] obstacles, int count)
        {
            if (CompanionNavigation2D.IsSegmentSafe(from, to, time, duration, extent, padding, obstacles, count)) return true;
            // 已经被移动墙覆盖时只接受逐步脱离，不允许把“穿过另一侧”当作逃生。
            for (int i = 0; i < count; i++)
            {
                var h = obstacles[i];
                float start = Clearance(from, h.Center + h.Velocity * time, extent, h.Radius + padding);
                float finish = Clearance(to, h.Center + h.Velocity * (time + duration), extent, h.Radius + padding);
                if (start <= 0) { if (finish <= start + .0001f) return false; }
                else if (!CompanionNavigation2D.IsObstacleSegmentSafe(from, to, time, duration, extent, padding,
                    h)) return false;
            }
            return true;
        }
        private static float Clearance(Vector2 point, Vector2 center, Vector2 extent, float radius)
        {
            Vector2 d = point - center;
            Vector2 q = new Vector2(Mathf.Abs(d.x), Mathf.Abs(d.y)) - extent;
            return new Vector2(Mathf.Max(0, q.x), Mathf.Max(0, q.y)).magnitude +
                Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
        }
    }
}
