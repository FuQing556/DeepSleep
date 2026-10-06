using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    public enum KimiUltimateState : byte { Idle, Charging, Releasing, Draining, Stagger, Complete }

    /// <summary>吹笛→次数盾打断或五轮潮汐；仅管理专属增援池，不能清走关卡其他敌人。</summary>
    public sealed class KimiUltimatePattern2D : MonoBehaviour, IFixedSimulationStep
    {
        public KimiUltimateConfig Config;
        public KimiBoss2D Boss;
        public KimiHitCurtain2D Curtain;
        public EnemyProjectilePool2D Blades;
        public EnemySpawnDirector2D Reinforcements;
        public EnemyActorPool2D ReinforcementPool;
        public OneShotSpriteEffectPool2D BreakEffects;
        public KimiUltimateState State { get; private set; }
        public int FiredVolleys { get; private set; }
        public int FiredBlades { get; private set; }
        public float ChargeRemaining => State==KimiUltimateState.Charging ? Mathf.Max(0,_chargeSeconds-_elapsed) : 0;
        public event Action Completed;
        public event Action VolleyFired;
        private CoopSessionController _session;
        private DamageHitbox2D[] _targets;
        private readonly PlayerLifeStateController2D[] _lives = new PlayerLifeStateController2D[2];
        private float _elapsed, _chargeSeconds, _nextVolley;
        private bool _phaseTwo;
        private int _nextTarget;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config==null || Boss==null || Curtain==null || Curtain.Shape==null || Curtain.Visual==null ||
                Curtain.Perception==null || Blades==null || Reinforcements==null || ReinforcementPool==null || BreakEffects==null)
            { reason="大招需要本体/光幕、独立巨刃池、360刷怪器/池和碎裂特效。"; return false; }
            return Config.TryValidate(out reason) && Blades.TryValidateConfiguration(out reason) &&
                Reinforcements.TryValidateConfiguration(out reason) && BreakEffects.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason)) { Debug.LogError("[KimiUltimate] "+reason,this); enabled=false; return; }
            Curtain.Broken+=OnBroken; Cancel();
        }
        public bool Begin(bool phaseTwo, CoopSessionController session, DamageHitbox2D[] targets, CombatPerceptionRegistry2D perception)
        {
            if (!isActiveAndEnabled || !Boss.IsAlive || targets==null || targets.Length!=2 || targets[0]==null || targets[1]==null ||
                (session!=null && session.Phase!=SessionPhase.Offline && !session.IsAuthority)) return false;
            Cancel(); _session=session; _targets=targets; _phaseTwo=phaseTwo; _nextTarget=0;
            for(int i=0;i<2;i++) targets[i].TryGetComponent(out _lives[i]);
            FiredVolleys=FiredBlades=0; _elapsed=0;
            _chargeSeconds=phaseTwo?Config.PhaseTwoChargeSeconds:Config.PhaseOneChargeSeconds;
            State=KimiUltimateState.Charging;
            Curtain.Show((Vector2)Boss.transform.position+Config.CurtainOffset,
                phaseTwo?Config.PhaseTwoHits:Config.PhaseOneHits,session,perception);
            Boss.SetVulnerable(false); Boss.SetPose(KimiPose.FluteCharge);
            Reinforcements.ApplyRuntimeTuning(true,0,1,Config.ReinforcementLimit,Config.ReinforcementHealthMultiplier);
            return true;
        }

        public void Simulate(float dt)
        {
            if (!isActiveAndEnabled || dt<=0 || !float.IsFinite(dt) || State==KimiUltimateState.Idle || State==KimiUltimateState.Complete) return;
            if (!Boss.IsAlive || (_session!=null && _session.Phase!=SessionPhase.Offline && !_session.IsAuthority)) { Cancel(); return; }
            _elapsed+=dt;
            // 专属子池只由此技能推进，不再重复注册到章节固定步列表。
            ReinforcementPool.Simulate(dt); Blades.Simulate(dt);
            switch(State)
            {
                case KimiUltimateState.Charging:
                    if (_elapsed<_chargeSeconds) { Reinforcements.Simulate(dt); break; }
                    Curtain.Clear(); Reinforcements.Stop(); Boss.SetVulnerable(true);
                    State=KimiUltimateState.Releasing; _elapsed=0; _nextVolley=Config.ReleaseDelay;
                    Boss.SetPose(KimiPose.TideRelease); break;
                case KimiUltimateState.Releasing:
                    if (_elapsed>=_nextVolley)
                    {
                        FireVolley(); _nextVolley+=Config.VolleyInterval;
                        if(FiredVolleys>=Config.VolleyCount) State=KimiUltimateState.Draining;
                    }
                    break;
                case KimiUltimateState.Draining:
                    if(Blades.ActiveCount==0) Finish(); break;
                case KimiUltimateState.Stagger:
                    if(_elapsed>=Config.StaggerSeconds) Finish(); break;
            }
        }
        private void FireVolley()
        {
            Vector2 origin=(Vector2)Boss.transform.position+Config.MuzzleOffset;
            Vector2 direction=Vector2.left;
            for(int attempt=0;attempt<2;attempt++)
            {
                int i=_nextTarget; _nextTarget=(_nextTarget+1)%2;
                if(!_targets[i].IsActiveTarget || (_lives[i]!=null && _lives[i].State!=PlayerLifeState.Alive)) continue;
                Vector2 aim=(Vector2)_targets[i].transform.position-origin;
                if(aim.sqrMagnitude>.000001f) direction=aim.normalized; break;
            }
            int count=_phaseTwo?Config.PhaseTwoBlades:1;
            for(int i=0;i<count;i++)
            {
                float angle=(i-(count-1)*.5f)*Config.SpreadDegrees;
                Vector2 shot=Quaternion.Euler(0,0,angle)*direction;
                if(Blades.TryRent(origin,shot,Boss.gameObject,out _)) FiredBlades++;
            }
            FiredVolleys++;
            VolleyFired?.Invoke();
        }
        private void OnBroken()
        {
            if(State!=KimiUltimateState.Charging) return;
            Reinforcements.Stop(); Boss.SetVulnerable(true); Boss.SetPose(KimiPose.Stagger);
            State=KimiUltimateState.Stagger; _elapsed=0;
            // 沿光幕分布既有碎镜图，保留每张图等比尺寸。
            for(int i=0;i<4;i++) BreakEffects.TryPlay((Vector2)Curtain.transform.position+Vector2.up*((i-1.5f)*Curtain.Shape.size.y/4),0);
        }
        private void Finish()
        {
            Cancel(); State=KimiUltimateState.Complete; Boss.SetPose(KimiPose.Idle); Completed?.Invoke();
        }
        public void Cancel()
        {
            bool wasActive=State!=KimiUltimateState.Idle && State!=KimiUltimateState.Complete;
            State=KimiUltimateState.Idle; _elapsed=0;
            if(Curtain!=null) Curtain.Clear(); if(Blades!=null) Blades.ReturnAllActive();
            if(Reinforcements!=null) Reinforcements.Stop();
            if(ReinforcementPool!=null) ReinforcementPool.DespawnAll(EnemyDespawnReason.RunReset);
            if(wasActive && Boss!=null && Boss.IsAlive) Boss.SetVulnerable(true);
        }
        private void OnDisable()=>Cancel();
        private void OnDestroy(){if(Curtain!=null) Curtain.Broken-=OnBroken;}
    }
}
