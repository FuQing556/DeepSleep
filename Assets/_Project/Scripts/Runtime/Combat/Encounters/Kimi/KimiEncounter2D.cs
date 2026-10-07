using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    public enum KimiSkill : byte { Moon, Prism, Laser, Ultimate }
    public enum KimiEncounterState : byte { Idle, Prelude, Entering, Casting, Gap, PhaseChange, Complete }

    /// <summary>仅Kimi的遭遇编排。四技能及其子池只从这里推进，章节/挑战入口复用同一实例。</summary>
    public sealed class KimiEncounter2D : MonoBehaviour, IFixedSimulationStep
    {
        public KimiEncounterConfig Config;
        public KimiBoss2D Boss;
        public KimiMoonBladePattern2D Moon;
        public KimiPrismPattern2D Prism;
        public KimiLaserPattern2D Laser;
        public KimiUltimatePattern2D Ultimate;
        public KimiEncounterState State {get;private set;}
        public KimiSkill CurrentSkill {get;private set;}
        public int CastsStarted {get;private set;}
        public int CastsFinished {get;private set;}
        public int CycleNumber {get;private set;}
        public float StateElapsed {get;private set;}
        // 章节适配器在此事件的同一模拟刻清场、切夜景、改任务/倒计时；不让技能查询关卡对象。
        public event Action TakeoverRequested;
        public event Action<KimiSkill> CastStarted;
        public event Action Completed;
        public event Action PhaseChanged;
        private CoopSessionController _session;
        private CombatPerceptionRegistry2D _perception;
        private CompanionObstacleRegistry2D _obstacles;
        private DamageHitbox2D[] _targets;
        private readonly PlayerLifeStateController2D[] _lives=new PlayerLifeStateController2D[2];
        private readonly Collider2D[] _targetShapes = new Collider2D[2];
        private readonly float[] _contactCooldowns = new float[2];
        private System.Random _random;
        private readonly KimiSkill[] _choices=new KimiSkill[4];
        private int _cycleCasts, _nextTarget;
        private bool _hasPrevious, _ultimateUsed;
        private float _gap;
        private float _preludeSeconds;

        public bool TryValidateConfiguration(out string reason)
        {
            if(Config==null || Boss==null || Moon==null || Prism==null || Laser==null || Ultimate==null ||
                Moon.Boss!=Boss || Prism.Boss!=Boss || Laser.Boss!=Boss || Ultimate.Boss!=Boss)
            {reason="遭遇须显式引用同一本体的四技能。";return false;}
            return Config.TryValidate(out reason) && Moon.TryValidateConfiguration(out reason) &&
                Prism.TryValidateConfiguration(out reason) && Laser.TryValidateConfiguration(out reason) && Ultimate.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if(!TryValidateConfiguration(out string reason)){Debug.LogError("[KimiEncounter] "+reason,this);enabled=false;return;}
            Moon.Completed+=OnCastCompleted;Prism.Completed+=OnCastCompleted;Laser.Completed+=OnCastCompleted;
            Ultimate.Completed+=OnCastCompleted;Boss.Defeated+=OnDefeated;
        }
        /// <summary>章节传waitForPrelude=true；独立挑战可以false，跳过50秒普通战斗而保留出场。</summary>
        public bool Begin(bool waitForPrelude,int seed,CoopSessionController session,DamageHitbox2D[] targets,
            CombatPerceptionRegistry2D perception,CompanionObstacleRegistry2D obstacles, float? preludeSeconds = null)
        {
            if(!isActiveAndEnabled || targets==null || targets.Length!=2 || targets[0]==null || targets[1]==null ||
                (session!=null && session.Phase!=SessionPhase.Offline && !session.IsAuthority) ||
                (preludeSeconds.HasValue && (!float.IsFinite(preludeSeconds.Value) || preludeSeconds.Value <= 0))) return false;
            Cancel();_session=session;_targets=targets;_perception=perception;_obstacles=obstacles;_random=new System.Random(seed);
            _preludeSeconds = preludeSeconds ?? Config.PreludeSeconds;
            for(int i=0;i<2;i++)
            {
                targets[i].TryGetComponent(out _lives[i]);
                if (!targets[i].TryGetComponent(out _targetShapes[i]))
                { Debug.LogError("[KimiEncounter] 玩家受击体缺少显式Collider2D。", this); Cancel(); return false; }
                _contactCooldowns[i] = 0;
            }
            CastsStarted=CastsFinished=0;CycleNumber=1;_cycleCasts=0;_nextTarget=0;_hasPrevious=_ultimateUsed=false;
            if(waitForPrelude) State=KimiEncounterState.Prelude;else Enter();
            return true;
        }
        private void Enter()
        {
            State=KimiEncounterState.Entering;StateElapsed=0;
            TakeoverRequested?.Invoke();
            // 允许章节在接管回调中取消（例如同刻队伍失败）；不能取消后又偷偷生成Boss。
            if(State!=KimiEncounterState.Entering) return;
            Boss.BeginAuthority(Config.BossPosition,_session,_perception);Boss.SetVulnerable(false);
            Boss.SetPose(KimiPose.Idle);
        }
        public void Simulate(float dt)
        {
            if(!isActiveAndEnabled || !float.IsFinite(dt) || dt<=0 || State==KimiEncounterState.Idle || State==KimiEncounterState.Complete) return;
            if(_session!=null && _session.Phase!=SessionPhase.Offline && !_session.IsAuthority){Cancel();return;}
            if(State!=KimiEncounterState.Prelude && !Boss.IsAlive){Cancel();return;}
            StateElapsed+=dt;
            if (State == KimiEncounterState.Casting || State == KimiEncounterState.Gap || State == KimiEncounterState.PhaseChange)
                SimulateContact(dt);
            switch(State)
            {
                case KimiEncounterState.Prelude:
                    if(StateElapsed>=_preludeSeconds) Enter();break;
                case KimiEncounterState.Entering:
                    if(StateElapsed>=Config.EntrySeconds){Boss.SetVulnerable(true);StartCast();}break;
                case KimiEncounterState.PhaseChange:
                    if(StateElapsed>=Config.PhaseChangeSeconds){Boss.SetVulnerable(true);State=KimiEncounterState.Gap;StateElapsed=0;Boss.SetPose(KimiPose.Idle);}break;
                case KimiEncounterState.Gap:
                    if(Boss.CommitPhaseAtSkillBoundary()) BeginPhaseChange();
                    else if(StateElapsed>=_gap) StartCast();break;
                case KimiEncounterState.Casting:
                    switch(CurrentSkill)
                    {
                        case KimiSkill.Moon:Moon.Projectiles.Simulate(dt);Moon.Simulate(dt);break;
                        case KimiSkill.Prism:Prism.Simulate(dt);break;
                        case KimiSkill.Laser:Laser.Simulate(dt);break;
                        case KimiSkill.Ultimate:Ultimate.Simulate(dt);break;
                    }
                    break;
            }
        }
        private void SimulateContact(float dt)
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                _contactCooldowns[i] = Mathf.Max(0, _contactCooldowns[i] - dt);
                if (_contactCooldowns[i] > 0 || !_targets[i].IsActiveTarget || !_targetShapes[i].enabled ||
                    (_lives[i] != null && _lives[i].State != PlayerLifeState.Alive) ||
                    !Boss.HitCollider.Distance(_targetShapes[i]).isOverlapped) continue;
                _contactCooldowns[i] = Boss.Config.ContactIntervalSeconds;
                Vector2 center = Boss.HitCollider.bounds.center;
                Vector2 point = _targetShapes[i].ClosestPoint(center);
                var packet = new DamagePacket(Boss.Config.ContactDamageAmount, point,
                    (Vector2)_targets[i].transform.position - center, Boss.gameObject,
                    DamageAttackIdAllocator.Next(), DamageInterceptionPolicy.Blockable);
                _targets[i].TryReceiveDamage(in packet);
            }
        }
        private void StartCast()
        {
            int target=-1;
            for(int j=0;j<2;j++)
            {
                int i=(_nextTarget+j)%2;
                if(_targets[i].IsActiveTarget && (_lives[i]==null || _lives[i].State==PlayerLifeState.Alive)){target=i;break;}
            }
            // 没有站立玩家时等原救援/失败规则，不以随机方向继续施法。
            if(target<0) return;
            if(_cycleCasts==Config.CastsPerCycle){_cycleCasts=0;_ultimateUsed=false;CycleNumber++;}
            int count=0;
            for(int i=0;i<4;i++)
            {
                var skill=(KimiSkill)i;
                if((_hasPrevious && skill==CurrentSkill) || (skill==KimiSkill.Ultimate && _ultimateUsed)) continue;
                _choices[count++]=skill;
            }
            KimiSkill next=_choices[_random.Next(count)];
            bool began=next switch
            {
                KimiSkill.Moon=>Moon.Begin(Boss.PhaseTwo,_random.Next(),_session),
                KimiSkill.Prism=>Prism.Begin(Boss.PhaseTwo,_session,_targets,_perception,_obstacles),
                KimiSkill.Laser=>Laser.Begin(_targets[target].transform.position,_session,_targets,target),
                _=>Ultimate.Begin(Boss.PhaseTwo,_session,_targets,_perception)
            };
            if(!began){Debug.LogError("[KimiEncounter] 技能无法启动："+next,this);Cancel();return;}
            CurrentSkill=next;_hasPrevious=true;_ultimateUsed|=next==KimiSkill.Ultimate;
            _nextTarget=(target+1)%2;_cycleCasts++;CastsStarted++;StateElapsed=0;State=KimiEncounterState.Casting;
            CastStarted?.Invoke(next);
        }
        private void OnCastCompleted()
        {
            if(State!=KimiEncounterState.Casting) return;
            CastsFinished++;StateElapsed=0;_gap=_cycleCasts==Config.CastsPerCycle?Config.CycleGapSeconds:Config.CastGapSeconds;
            if(Boss.CommitPhaseAtSkillBoundary()) BeginPhaseChange();else State=KimiEncounterState.Gap;
        }
        private void BeginPhaseChange()
        {State=KimiEncounterState.PhaseChange;StateElapsed=0;Boss.SetVulnerable(false);Boss.SetPose(KimiPose.PhaseChange);PhaseChanged?.Invoke();}
        private void OnDefeated()
        {
            if(State==KimiEncounterState.Idle || State==KimiEncounterState.Complete) return;
            ClearSkills();State=KimiEncounterState.Complete;StateElapsed=0;Completed?.Invoke();
        }
        private void ClearSkills(){Moon.Cancel();Prism.Cancel();Laser.Cancel();Ultimate.Cancel();}
        public void Cancel(bool preserveDefeat = false)
        {
            State=KimiEncounterState.Idle;StateElapsed=0;
            if(Moon!=null && Prism!=null && Laser!=null && Ultimate!=null) ClearSkills();
            if(Boss!=null)Boss.ResetEncounter(preserveDefeat);
        }
        private void OnDisable()=>Cancel();
        private void OnDestroy()
        {
            if(Moon!=null)Moon.Completed-=OnCastCompleted;if(Prism!=null)Prism.Completed-=OnCastCompleted;
            if(Laser!=null)Laser.Completed-=OnCastCompleted;if(Ultimate!=null)Ultimate.Completed-=OnCastCompleted;
            if(Boss!=null)Boss.Defeated-=OnDefeated;
        }
    }
}
