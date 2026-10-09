using UnityEngine;

namespace DeepSleep.Runtime.Players.Movement
{
    /// <summary>主机物理步与客人移动预测共享的速度/边界计算，不做碰撞或伤害结算。</summary>
    public static class PlayerMovementStep
    {
        /// <summary>击退覆盖本步移动输入，速度线性衰减；主机与客人预测共用同一积分和边界。</summary>
        public static void Calculate(ref Vector2 position, ref Vector2 velocity, Vector2 intent,
            PlayerMotorConfig config, Vector2 extents, Vector2 offset, float dt,
            ref Vector2 knockbackVelocity, ref float knockbackRemaining)
        {
            if (knockbackRemaining <= 0f)
            { Calculate(ref position, ref velocity, intent, config, extents, offset, dt); return; }
            if (dt <= 0f) return;
            float used = Mathf.Min(dt, knockbackRemaining);
            Vector2 endVelocity = knockbackVelocity * (1f - used / knockbackRemaining);
            Vector2 min = config.MovementBounds.min + extents - offset;
            Vector2 max = config.MovementBounds.max - extents - offset;
            position = new Vector2(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y));
            Vector2 next = position + (knockbackVelocity + endVelocity) * (.5f * used);
            next = new Vector2(Mathf.Clamp(next.x, min.x, max.x), Mathf.Clamp(next.y, min.y, max.y));
            velocity = (next - position) / dt;
            knockbackVelocity = endVelocity;
            knockbackRemaining = Mathf.Max(0f, knockbackRemaining - used);
        }

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
