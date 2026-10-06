using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Players.Companion;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    /// <summary>
    /// 棱光专用反射球：没有DamageHitbox/可清除敌弹组件，碰人保留。
    /// 分段扫掠同时用于移动与伤害，避免高速球穿人和反射折线被当成一条直线。
    /// </summary>
    public sealed class KimiPrismOrb2D : MonoBehaviour
    {
        public CircleCollider2D Shape;
        public SpriteRenderer Visual;
        private KimiPrismPattern2D _owner;
        private CompanionObstacleRegistry2D _obstacles;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        // 游戏固定双玩家；同一接收器多个碰撞体与同帧折返共用一次接触门。
        private readonly IDamageReceiver[] _victims = new IDamageReceiver[2];
        private readonly float[] _nextContact = new float[2];
        private float _age;
        public bool Active { get; private set; }
        public bool Escaped { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public int ReflectionCount { get; private set; }

        public void Launch(KimiPrismPattern2D owner, Vector2 position, Vector2 velocity, CompanionObstacleRegistry2D obstacles)
        {
            Clear(); _owner=owner; _obstacles=obstacles;
            Position=position; Velocity=velocity; Active=true; Escaped=false; ReflectionCount=0; _age=0;
            Shape.radius=owner.Config.OrbRadius;Shape.enabled=true;Visual.enabled=true;
            transform.position=position;
            _obstacles?.Register(Shape,Velocity,this);
        }

        public void Simulate(float dt)
        {
            if (!Active || dt<=0) return;
            _age+=dt;
            var bounds=_owner.ReflectionBounds;
            float remaining=dt;
            // 球心到边界的解析交点；有限非零速度且战区有正面积，因此每次反射必有正路程。
            while (remaining>0)
            {
                if (Escaped) { Move(Position+Velocity*remaining); break; }
                float tx=Velocity.x>0 ? (bounds.xMax-Position.x)/Velocity.x : Velocity.x<0 ? (bounds.xMin-Position.x)/Velocity.x : float.PositiveInfinity;
                float ty=Velocity.y>0 ? (bounds.yMax-Position.y)/Velocity.y : Velocity.y<0 ? (bounds.yMin-Position.y)/Velocity.y : float.PositiveInfinity;
                float travel=Mathf.Max(0,Mathf.Min(tx,ty));
                if(travel>remaining) { Move(Position+Velocity*remaining); break; }
                Move(Position+Velocity*travel); remaining-=travel;
                // epsilon仅区分同一个数学角点，不是可调技能参数。
                bool x=tx<=ty+.000001f, y=ty<=tx+.000001f;
                bool reflected=false;
                Vector2 velocity=Velocity;
                if(x) { if(_owner.Sides[velocity.x>0?1:0].IsIntact) {velocity.x=-velocity.x;reflected=true;} else Escaped=true; }
                if(y) { if(_owner.Sides[velocity.y>0?3:2].IsIntact) {velocity.y=-velocity.y;reflected=true;} else Escaped=true; }
                Velocity=velocity;
                if(reflected) { ReflectionCount++; _owner.NotifyReflection(Position); }
            }
            transform.position=Position;
            if(Escaped && !_owner.ExitBounds.Contains(Position)) Clear();
            else _obstacles?.Register(Shape,Velocity,this);
        }

        private void Move(Vector2 end)
        {
            Vector2 offset=end-Position;
            float distance=offset.magnitude;
            var filter=new ContactFilter2D {useTriggers=true};filter.SetLayerMask(_owner.Config.PlayerLayers);
            int count=Physics2D.CircleCast(Position,_owner.Config.OrbRadius,distance>0?offset/distance:Vector2.right,filter,_hits,distance);
            for(int i=0;i<count;i++)
            {
                if(!_hits[i].collider.TryGetComponent<DamageHitbox2D>(out var hitbox) || !hitbox.CanReceiveDamage ||
                    !hitbox.TryGetReceiver(out var receiver)) continue;
                int slot=-1;
                for(int j=0;j<_victims.Length;j++) if(ReferenceEquals(_victims[j],receiver)) {slot=j;break;}
                if(slot<0) for(int j=0;j<_victims.Length;j++) if(_victims[j]==null) {slot=j;_victims[j]=receiver;break;}
                if(slot<0 || _age<_nextContact[slot]) continue;
                var packet=new DamagePacket(_owner.Config.ContactDamage,_hits[i].point,Velocity,
                    _owner.Boss.gameObject,DamageAttackIdAllocator.Next(),DamageInterceptionPolicy.Blockable);
                if(hitbox.TryReceiveDamage(in packet))
                {
                    _nextContact[slot]=_age+_owner.Config.ContactInterval;
                    _owner.NotifyImpact(_hits[i].point);
                }
            }
            Position=end;
        }

        public void Clear()
        {
            _obstacles?.Unregister(Shape);_obstacles=null;
            Active=Escaped=false; Velocity=Vector2.zero;_age=0;
            for(int i=0;i<_victims.Length;i++){_victims[i]=null;_nextContact[i]=0;}
            if(Shape!=null) Shape.enabled=false;
            if(Visual!=null) Visual.enabled=false;
        }
        public void ApplyReplica(bool shown, Vector2 position)
        {
            // Active保持false，哪怕误调用Simulate也不能进行本地反射/伤害。
            Active = false; Shape.enabled = false; Visual.enabled = shown;
            Position = position; transform.position = position;
        }
        private void Awake()=>Clear();
        private void OnDisable()=>Clear();
    }
}
