using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake;
using DeepSleep.Runtime.Combat.Projectiles;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Perception
{
    /// <summary>池内对象的只读感知适配器；引用由预制体明确装配，不改变碰撞体。</summary>
    public sealed class CombatPerceptionBody2D : MonoBehaviour
    {
        public Collider2D Shape;
        public Rigidbody2D Body;
        public EnemyActor2D Enemy;
        public DamageHitbox2D Hitbox;
        public EnemyProjectile2D Projectile;
        public DataCrawlerSnakeAttackController2D SnakeAttack;
        public DownloadChargeMotor2D DownloadCharge;
        [Min(0)] public float TargetValue;
        public bool ThreatTrackedAsObstacle;
        private CombatPerceptionRegistry2D _registry;

        public bool IsEnemy => Enemy != null;
        public bool IsAttackTarget => Hitbox != null;
        public bool IsObservable => isActiveAndEnabled && Shape != null && Shape.enabled &&
            (IsAttackTarget ? Hitbox.CanReceiveDamage : Projectile != null && Projectile.IsRented);
        public Vector2 Position => Shape.bounds.center;
        public Vector2 Velocity => DownloadCharge != null ? DownloadCharge.PerceivedVelocity : Body != null ? Body.linearVelocity : Vector2.zero;
        public float Radius => ((Vector2)Shape.bounds.extents).magnitude;
        public bool IsCharging => (SnakeAttack != null && SnakeAttack.State == DataCrawlerSnakeAttackState.Charging) ||
            (DownloadCharge != null && DownloadCharge.IsRunning && DownloadCharge.State == DownloadChargeState.Charging);

        public void Register(CombatPerceptionRegistry2D registry)
        {
            if (_registry != null) _registry.Unregister(this);
            _registry = registry;
            if (Shape == null || (Hitbox == null && Projectile == null))
            {
                Debug.LogError("[CombatPerception] 感知对象缺少明确的碰撞体或伤害/投射物引用。", this);
                return;
            }
            _registry.Register(this);
        }

        private void OnDestroy()
        {
            if (_registry != null) _registry.Unregister(this);
        }
    }
}
