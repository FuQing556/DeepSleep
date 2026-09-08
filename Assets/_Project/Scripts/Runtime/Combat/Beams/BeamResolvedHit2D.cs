using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams
{
    /// <summary>
    /// 一条束线完成空间查询和接收者去重后得到的单次命中。
    /// </summary>
    public readonly struct BeamResolvedHit2D
    {
        public BeamResolvedHit2D(
            DamageHitbox2D hitbox,
            Vector2 hitPoint,
            bool isPrimaryTarget)
        {
            Hitbox = hitbox;
            HitPoint = hitPoint;
            IsPrimaryTarget = isPrimaryTarget;
        }

        public DamageHitbox2D Hitbox { get; }
        public Vector2 HitPoint { get; }
        public bool IsPrimaryTarget { get; }

        public bool IsValid => Hitbox != null;
    }
}
