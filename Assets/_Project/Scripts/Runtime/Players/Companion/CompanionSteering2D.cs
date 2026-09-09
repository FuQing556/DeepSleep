using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>局部避险：候选为目标方向、停下及八方向。不是寻路器；目前场地无实体迷宫。</summary>
    public static class CompanionSteering2D
    {
        public static Vector2 Choose(CompanionBattleSensor2D sensor, CompanionTacticsConfig tactics,
            PlayerMotorConfig motor, Vector2 position, Vector2 velocity, Vector2 extent, Vector2 destination,
            Vector2 previousMove, Vector2 colliderOffset)
        {
            Vector2 offset = destination - position;
            Vector2 desired = offset.magnitude <= tactics.ArrivalRadius ? Vector2.zero : offset.normalized;
            Vector2 result = Vector2.zero;
            float best = float.PositiveInfinity;
            // 2个特殊候选 + 八方向是固定离散算法，不是隐藏玩法数值。
            for (int i = 0; i < 10; i++)
            {
                Vector2 candidate = i == 0 ? desired : i == 1 ? Vector2.zero :
                    new Vector2(Mathf.Cos((i - 2) * Mathf.PI / 4), Mathf.Sin((i - 2) * Mathf.PI / 4));
                float acceleration = candidate == Vector2.zero ? motor.Deceleration : motor.Acceleration;
                Vector2 nextVelocity = Vector2.MoveTowards(velocity, candidate * motor.MaximumSpeed,
                    acceleration * tactics.PredictionSeconds);
                Vector2 averageVelocity = (velocity + nextVelocity) * 0.5f;
                Vector2 end = position + averageVelocity * tactics.PredictionSeconds;
                Vector2 clamped = CompanionThreatMath.ClampPoint(end + colliderOffset, motor.MovementBounds, extent) - colliderOffset;
                if ((end - clamped).sqrMagnitude > 0.0001f && candidate != Vector2.zero) continue;
                float danger = sensor.Danger(position + colliderOffset, averageVelocity, extent.magnitude);
                float score = Vector2.Distance(clamped, destination) + tactics.DangerCost * danger +
                    tactics.DirectionChangeCost * (candidate - previousMove).sqrMagnitude;
                if (score >= best) continue;
                best = score;
                result = candidate;
            }
            return result;
        }
    }
}
