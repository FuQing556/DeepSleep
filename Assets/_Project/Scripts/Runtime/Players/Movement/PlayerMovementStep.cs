using UnityEngine;

namespace DeepSleep.Runtime.Players.Movement
{
    /// <summary>主机物理步与客人移动预测共享的速度/边界计算，不做碰撞或伤害结算。</summary>
    public static class PlayerMovementStep
    {
        public static void Calculate(ref Vector2 position, ref Vector2 velocity, Vector2 intent,
            PlayerMotorConfig config, Vector2 extents, Vector2 offset, float dt)
        {
            if (dt <= 0) return;
            intent = Vector2.ClampMagnitude(intent, 1);
            velocity = Vector2.MoveTowards(velocity, intent * config.MaximumSpeed,
                (intent.sqrMagnitude > 0 ? config.Acceleration : config.Deceleration) * dt);
            Vector2 min = config.MovementBounds.min + extents - offset;
            Vector2 max = config.MovementBounds.max - extents - offset;
            position = new Vector2(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y));
            Vector2 next = position + velocity * dt;
            next = new Vector2(Mathf.Clamp(next.x, min.x, max.x), Mathf.Clamp(next.y, min.y, max.y));
            velocity = (next - position) / dt;
        }
    }
}
