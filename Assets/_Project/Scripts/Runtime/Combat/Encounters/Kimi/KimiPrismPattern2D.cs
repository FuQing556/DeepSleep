using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    /// <summary>固定战区棱光；四边独立HP，破边不补回，全破仍等原计时结束。</summary>
    public sealed class KimiPrismPattern2D : MonoBehaviour, IFixedSimulationStep
    {
        public KimiPrismConfig Config;
        public CombatPlayfieldConfig Playfield;
        public KimiBoss2D Boss;
        public Transform Muzzle;
        // 稳定语义顺序：左、右、下、上。不是按名称运行时查询。
        public KimiMirrorSide2D[] Sides;
        public SpriteRenderer[] Corners;
        public KimiPrismOrb2D[] Orbs;
        public OneShotSpriteEffectPool2D BreakEffects;
        public OneShotSpriteEffectPool2D HitEffects;
        public bool Active {get;private set;}
        public bool Complete {get;private set;}
        public int FiredCount {get;private set;}
        public float Elapsed {get;private set;}
        public Rect Arena {get;private set;}
        public Rect ReflectionBounds {get;private set;}
        public Rect ExitBounds {get;private set;}
        public event Action Completed;
        public event Action<Vector2> Reflected;
        public event Action<Vector2> OrbFired;
        private CoopSessionController _session;
        private CompanionObstacleRegistry2D _obstacles;
        private DamageHitbox2D[] _targets;
        private readonly DeepSleep.Runtime.Players.LifeCycle.PlayerLifeStateController2D[] _targetLives =
            new DeepSleep.Runtime.Players.LifeCycle.PlayerLifeStateController2D[2];
        private float _nextShot,_speed;
        private int _nextTarget;

        public bool TryValidateConfiguration(out string reason)
        {
            if(Config==null || Playfield==null || Boss==null || Muzzle==null || BreakEffects==null || HitEffects==null ||
                Sides==null || Sides.Length!=4 || Corners==null || Corners.Length!=4 || Orbs==null)
            {reason="棱光需要显式配置、本体/炮口、四边/四角、预热球和特效池。";return false;}
            if(!Config.TryValidate(out reason) || !Playfield.TryValidate(out reason)) return false;
            if(Orbs.Length<Mathf.CeilToInt(Config.Duration/Config.ShotInterval) ||
                Mathf.Min(Playfield.WorldBounds.width,Playfield.WorldBounds.height)<=2*(Config.ArenaInset+Config.OrbRadius))
            {reason="球容量不足或战区内缩后没有有效面积。";return false;}
            foreach(var side in Sides) if(side==null || side.Shape==null || side.Visual==null || side.Perception==null ||
                side.Thickness<=0 || side.FlashSeconds<=0) {reason="镜边装配不完整。";return false;}
            foreach(var corner in Corners) if(corner==null) {reason="镜角缺失。";return false;}
            foreach(var orb in Orbs) if(orb==null || orb.Shape==null || orb.Visual==null ||
                orb.Visual.transform==orb.transform || !float.IsFinite(orb.VisualSpinDegreesPerSecond))
                {reason="预热球装配缺失、自转数值无效或Visual未独立于碰撞根节点。";return false;}
            return BreakEffects.TryValidateConfiguration(out reason) && HitEffects.TryValidateConfiguration(out reason);
        }

        private void Awake()
        {
            if(!TryValidateConfiguration(out string reason)) {Debug.LogError("[KimiPrism] "+reason,this);enabled=false;return;}
            foreach(var side in Sides) side.Broken+=OnSideBroken;
            Cancel();
        }

        public bool Begin(bool phaseTwo, CoopSessionController session, DamageHitbox2D[] targets,
            CombatPerceptionRegistry2D perception, CompanionObstacleRegistry2D obstacles)
        {
            if(!enabled || !Boss.IsAlive || targets==null || targets.Length!=2 || targets[0]==null || targets[1]==null ||
                (session!=null && session.Phase!=SessionPhase.Offline && !session.IsAuthority)) return false;
            Cancel();_session=session;_targets=targets;_obstacles=obstacles;
            for(int i=0;i<targets.Length;i++) targets[i].TryGetComponent(out _targetLives[i]);
            Rect b=Playfield.WorldBounds;
            Arena=Rect.MinMaxRect(b.xMin+Config.ArenaInset,b.yMin+Config.ArenaInset,b.xMax-Config.ArenaInset,b.yMax-Config.ArenaInset);
            // 球心反射面=镜边内表面再内缩球半径，与实际镜体厚度共用同一数值。
            ReflectionBounds=Rect.MinMaxRect(Arena.xMin+Sides[0].Thickness*.5f+Config.OrbRadius,
                Arena.yMin+Sides[2].Thickness*.5f+Config.OrbRadius,
                Arena.xMax-Sides[1].Thickness*.5f-Config.OrbRadius,
                Arena.yMax-Sides[3].Thickness*.5f-Config.OrbRadius);
            ExitBounds=Rect.MinMaxRect(b.xMin-Config.ExitPadding,b.yMin-Config.ExitPadding,b.xMax+Config.ExitPadding,b.yMax+Config.ExitPadding);
            if(!ReflectionBounds.Contains(Muzzle.position)) {Debug.LogError("[KimiPrism] 炮口必须位于镜框内。",this);return false;}
            float health=phaseTwo?Config.PhaseTwoMirrorHealth:Config.PhaseOneMirrorHealth;
            for(int i=0;i<4;i++)
            {
                bool vertical=i<2;
                Vector2 pos=vertical ? new Vector2(i==0?Arena.xMin:Arena.xMax,Arena.center.y) : new Vector2(Arena.center.x,i==2?Arena.yMin:Arena.yMax);
                Sides[i].Show(pos,vertical?90:0,vertical?Arena.height:Arena.width,health,session,perception);
            }
            for(int i=0;i<4;i++)
            {
                Corners[i].transform.SetPositionAndRotation(new Vector2(i==0||i==3?Arena.xMin:Arena.xMax,i<2?Arena.yMax:Arena.yMin),Quaternion.Euler(0,0,-90*i));
                Corners[i].enabled=true;
            }
            Active=true;Complete=false;FiredCount=0;Elapsed=0;_nextShot=Config.FirstShotDelay;_nextTarget=0;
            _speed=phaseTwo?Config.PhaseTwoSpeed:Config.PhaseOneSpeed;
            Boss.SetPose(KimiPose.PrismCast);
            return true;
        }

        public void Simulate(float dt)
        {
            if(!Active || !isActiveAndEnabled || dt<=0 || (_session!=null && _session.Phase!=SessionPhase.Offline && !_session.IsAuthority)) return;
            if(!Boss.IsAlive) {Cancel();return;}
            Elapsed+=dt;
            if(Elapsed>=Config.Duration)
            {
                Cancel();Complete=true;Boss.SetPose(KimiPose.Idle);Completed?.Invoke();return;
            }
            foreach(var orb in Orbs) if(orb.Active) orb.Simulate(dt);
            if(Elapsed>=_nextShot)
            {
                _nextShot+=Config.ShotInterval;
                FireAtLivingTarget();
            }
        }

        private void FireAtLivingTarget()
        {
            for(int attempt=0;attempt<_targets.Length;attempt++)
            {
                int index=_nextTarget;
                var target=_targets[index];_nextTarget=(_nextTarget+1)%_targets.Length;
                // 受击无敌不代表倒地；IDamageReceiver为玩家门时仍可瞄准，只排除无效对象。
                if(!target.IsActiveTarget || (_targetLives[index]!=null &&
                    _targetLives[index].State!=DeepSleep.Runtime.Players.LifeCycle.PlayerLifeState.Alive)) continue;
                Vector2 direction=(Vector2)target.transform.position-(Vector2)Muzzle.position;
                if(direction.sqrMagnitude<.000001f) direction=Vector2.left;
                TryLaunch(Muzzle.position,direction);return;
            }
        }

        /// <summary>供技能和确定性诊断共用；仅当前权威遭遇可租球。</summary>
        public bool TryLaunch(Vector2 position,Vector2 direction)
        {
            if(!Active || !float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !ReflectionBounds.Contains(position) || direction.sqrMagnitude<.000001f ||
                (_session!=null && _session.Phase!=SessionPhase.Offline && !_session.IsAuthority)) return false;
            foreach(var orb in Orbs) if(!orb.Active)
            {orb.Launch(this,position,direction.normalized*_speed,_obstacles);FiredCount++;OrbFired?.Invoke(position);return true;}
            return false;
        }

        private void OnSideBroken(KimiMirrorSide2D side)
        {
            int index=Array.IndexOf(Sides,side);
            bool vertical=index<2;
            // 一整边消失时沿边分布碎片，而非把一个小碎片图拉长。
            for(int i=0;i<4;i++)
            {
                float t=(i+.5f)/4;
                Vector2 position=vertical ? new Vector2(side.transform.position.x,Mathf.Lerp(Arena.yMin,Arena.yMax,t)) :
                    new Vector2(Mathf.Lerp(Arena.xMin,Arena.xMax,t),side.transform.position.y);
                BreakEffects.TryPlay(position,vertical?90:0);
            }
            Corners[0].enabled=Sides[0].IsIntact && Sides[3].IsIntact;
            Corners[1].enabled=Sides[1].IsIntact && Sides[3].IsIntact;
            Corners[2].enabled=Sides[1].IsIntact && Sides[2].IsIntact;
            Corners[3].enabled=Sides[0].IsIntact && Sides[2].IsIntact;
        }

        public void NotifyReflection(Vector2 point)
        {
            HitEffects.TryPlay(point,0);
            Reflected?.Invoke(point);
        }
        public void NotifyImpact(Vector2 point)=>HitEffects.TryPlay(point,0);
        public void ApplyReplica(bool shown, Rect arena, float[] health)
        {
            Active = false; Arena = arena;
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2;
                Vector2 position = vertical ? new Vector2(i == 0 ? arena.xMin : arena.xMax, arena.center.y)
                    : new Vector2(arena.center.x, i == 2 ? arena.yMin : arena.yMax);
                Sides[i].ApplyReplica(position, vertical ? 90 : 0, vertical ? arena.height : arena.width, shown ? health[i] : 0);
                Corners[i].transform.SetPositionAndRotation(new Vector2(i == 0 || i == 3 ? arena.xMin : arena.xMax,
                    i < 2 ? arena.yMax : arena.yMin), Quaternion.Euler(0, 0, -90 * i));
            }
            Corners[0].enabled = shown && Sides[0].IsIntact && Sides[3].IsIntact;
            Corners[1].enabled = shown && Sides[1].IsIntact && Sides[3].IsIntact;
            Corners[2].enabled = shown && Sides[1].IsIntact && Sides[2].IsIntact;
            Corners[3].enabled = shown && Sides[0].IsIntact && Sides[2].IsIntact;
        }
        public void Cancel()
        {
            Active=false;Complete=false;
            if(Orbs!=null) foreach(var orb in Orbs) if(orb!=null) orb.Clear();
            if(Sides!=null) foreach(var side in Sides) if(side!=null) side.Clear();
            if(Corners!=null) foreach(var corner in Corners) if(corner!=null) corner.enabled=false;
        }
        private void OnDisable()=>Cancel();
        private void OnDestroy(){if(Sides!=null) foreach(var side in Sides) if(side!=null) side.Broken-=OnSideBroken;}
    }
}
