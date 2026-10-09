using System;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    public enum ClaudeEncounterState : byte { Idle, Prelude, Entering, Casting, Gap, PhaseChange, Complete }
    public enum ClaudeSkill : byte { SpatialCut, Energy, TrackingCut, PermissionBooks }

    /// <summary>四技能共用施法轴；已召出的书独立持续。本体由章节适配器唯一推进。</summary>
    public sealed class ClaudeEncounter2D : MonoBehaviour
    {
        public ClaudeEncounterConfig Config;
        public ClaudeBossActor2D Actor;
        public ClaudeEncounterPresentation2D Presentation;
        public ClaudeSpatialCutPattern2D SpatialCut;
        public ClaudeEnergyPattern2D Energy;
        public ClaudeEnergyPattern2D SecondaryEnergy;
        public ClaudeTrackingCutPattern2D TrackingCut;
        public ClaudePermissionModule2D Permissions;
        public CombatPlayfieldConfig Playfield;
        public CoopSessionController Session;
        public CombatPerceptionRegistry2D Perception;
        public ClaudeEncounterState State {get; private set;}
        public ClaudeSkill Skill {get; private set;}
        public int CastsStarted {get; private set;}
        public float BattleRealSeconds {get; private set;}
        public event Action TakeoverRequested, BattleStarted, Completed;
        public event Action<ClaudeSkill> CastStarted;
        public event Action PhaseChanged;
        private System.Random _random;
        private float _age, _gap, _steeringAge, _preludeSeconds;
        private int _cycleCasts;
        private bool _hasPrevious;
        private bool _gapUsesRealSeconds;
        private bool _stationaryGap;
        private bool _secondEnergyStarted;
        private float _energyReleaseRemaining;
        private const float EnergyReleaseHoldSeconds = .25f;
        private Vector2 _velocity;
        private bool MovesDuringBattle => (State == ClaudeEncounterState.Casting || State == ClaudeEncounterState.Gap) &&
            !(State == ClaudeEncounterState.Gap && _stationaryGap) &&
            (State != ClaudeEncounterState.Casting || Skill != ClaudeSkill.Energy ||
             (Energy.State != ClaudeEnergyState.Charging && SecondaryEnergy.State != ClaudeEnergyState.Charging && _energyReleaseRemaining <= 0));
        public Vector2 ContactVelocity => MovesDuringBattle ? _velocity : Vector2.zero;
        public Vector2 PredictContactCenter(float seconds) => QuickAppMotor2D.Wrap(
            (Vector2)Actor.Body.Sphere.bounds.center + ContactVelocity * seconds, Playfield.WorldBounds);
        private readonly ClaudeSkill[] _choices=new ClaudeSkill[4];
        private bool CanAuthor => Session==null || Session.Phase==SessionPhase.Offline || Session.IsAuthority;

        public bool TryValidateConfiguration(out string reason)
        {
            if(Config==null || Actor==null || Presentation==null || SpatialCut==null || Energy==null ||
                TrackingCut==null || SecondaryEnergy==null || SecondaryEnergy==Energy || Permissions==null || Playfield==null || Perception==null ||
                SpatialCut.Boss!=Actor.Body || Energy.Boss!=Actor.Body || TrackingCut.Boss!=Actor.Body)
            {reason="Claude遭遇须显式绑定同一本体的模块。";return false;}
            return Config.TryValidate(out reason) && Actor.TryValidateConfiguration(out reason) &&
                Presentation.TryValidateConfiguration(out reason) && SpatialCut.TryValidateConfiguration(out reason) &&
                Energy.TryValidateConfiguration(out reason) && SecondaryEnergy.TryValidateConfiguration(out reason) && TrackingCut.TryValidateConfiguration(out reason) &&
                Permissions.TryValidateConfiguration(out reason) && Playfield.TryValidate(out reason);
        }
        private void Awake()
        {
            if(!TryValidateConfiguration(out string reason)){Debug.LogError("[ClaudeEncounter] "+reason,this);enabled=false;return;}
            SpatialCut.Completed+=OnCastCompleted;Energy.Completed+=OnEnergyCompleted;SecondaryEnergy.Completed+=OnEnergyCompleted;TrackingCut.Completed+=OnCastCompleted;
            Energy.Fired+=OnEnergyFired;
            SecondaryEnergy.Fired+=OnSecondaryEnergyFired;
            Actor.Body.Defeated+=OnDefeated;
        }
        public bool Begin(int seed, float? preludeSeconds = null)
        {
            if(!isActiveAndEnabled || !CanAuthor || !TryValidateConfiguration(out _)) return false;
            if(preludeSeconds.HasValue && (!float.IsFinite(preludeSeconds.Value) || preludeSeconds.Value<=0)) return false;
            Cancel();_preludeSeconds=preludeSeconds ?? Config.PreludeSeconds;
            _random=new System.Random(seed);State=ClaudeEncounterState.Prelude;
            Actor.transform.position=Config.EntryPosition;Actor.FaceDefault();return true;
        }
        public void Simulate(float gameSeconds,float realSeconds)
        {
            if(!isActiveAndEnabled || !CanAuthor || !float.IsFinite(gameSeconds) || gameSeconds<=0 ||
                !float.IsFinite(realSeconds) || realSeconds<=0 || State==ClaudeEncounterState.Idle || State==ClaudeEncounterState.Complete) return;
            if(State==ClaudeEncounterState.Prelude)
            {
                _age+=realSeconds;
                if(_age>=_preludeSeconds)
                {
                    State=ClaudeEncounterState.Entering;_age=0;TakeoverRequested?.Invoke();
                    if(State==ClaudeEncounterState.Entering)
                    {
                        if(!Actor.BeginAuthority(Session,Perception)){Cancel();return;}
                        Actor.Body.SetVulnerable(false);
                        Actor.FaceDefault(); Presentation.BeginEntrance();
                    }
                }
                return;
            }
            if(State==ClaudeEncounterState.Entering)
            {
                if(!Presentation.IsEntranceComplete) return;
                Actor.Body.SetVulnerable(true);
                _age=0;
                State=ClaudeEncounterState.Gap;_gap=0;_gapUsesRealSeconds=false;BattleStarted?.Invoke();StartCast();return;
            }
            if(!Actor.Body.IsAlive) return;
            BattleRealSeconds+=realSeconds;
            Actor.SimulateContact(gameSeconds);
            if(State==ClaudeEncounterState.PhaseChange)
            {
                _age+=realSeconds;
                if(_age>=Config.PhaseChangeSeconds)
                {
                    Actor.CompletePhaseTransition();State=ClaudeEncounterState.Gap;_age=0;
                    _gap=Config.CastGapTwo;
                    _gapUsesRealSeconds=false;
                }
                return;
            }
            Permissions.Advance(realSeconds);
            _energyReleaseRemaining = Mathf.Max(0, _energyReleaseRemaining - realSeconds);
            // 能量蓄力时驻足，避免发射点穿边导致弹体模块取消却不完成。
            if(MovesDuringBattle)
                Move(gameSeconds);
            if(State==ClaudeEncounterState.Gap)
            {
                Actor.FaceDefault();
                if(Actor.Body.IsPhaseHealthLocked){BeginPhaseChange();return;}
                _age+=_gapUsesRealSeconds?realSeconds:gameSeconds;if(_age>=_gap) StartCast();
            }
            else if(State==ClaudeEncounterState.Casting)
            {
                switch(Skill)
                {
                    case ClaudeSkill.SpatialCut:SpatialCut.Simulate(gameSeconds);break;
                    case ClaudeSkill.Energy:
                        bool secondWasActive = _secondEnergyStarted;
                        Energy.Simulate(gameSeconds);
                        if(secondWasActive) SecondaryEnergy.Simulate(gameSeconds);
                        if(State==ClaudeEncounterState.Casting)
                            Actor.SetPose(Energy.State==ClaudeEnergyState.Charging || SecondaryEnergy.State==ClaudeEnergyState.Charging
                                ? ClaudePose.EnergyCharge : _energyReleaseRemaining>0 ? ClaudePose.EnergyRelease : ClaudePose.Move);
                        break;
                    case ClaudeSkill.TrackingCut:TrackingCut.Simulate(gameSeconds);break;
                    case ClaudeSkill.PermissionBooks:
                        _age+=realSeconds;
                        if(_age>=Config.PermissionCastSeconds) OnCastCompleted();
                        break;
                }
            }
        }
        private int LiveTarget()
        {
            int first=_random.Next(2);
            for(int n=0;n<2;n++){int i=(first+n)%2;if(Actor.Targets[i].IsActiveTarget && Actor.TargetLives[i].State==PlayerLifeState.Alive)return i;}
            return -1;
        }
        private void StartCast()
        {
            int target=LiveTarget();if(target<0)return;
            int count=0;
            for(int i=0;i<_choices.Length;i++)
            {
                var candidate=(ClaudeSkill)i;
                if((_hasPrevious && candidate==Skill) ||
                    (candidate==ClaudeSkill.TrackingCut && !TrackingCut.HasEligibleTarget()) ||
                    (candidate==ClaudeSkill.PermissionBooks && !Permissions.CanBeginBatch(Actor.Body.PhaseTwo))) continue;
                _choices[count++]=candidate;
            }
            if(count==0)return;
            var next=_choices[_random.Next(count)];
            if(next==ClaudeSkill.Energy) { _secondEnergyStarted=false; SecondaryEnergy.Cancel(); }
            if (next == ClaudeSkill.SpatialCut || next == ClaudeSkill.PermissionBooks) Actor.FaceDefault();
            else Actor.FaceAt(Actor.Targets[target].transform.position);
            // 起手能量球必须从合法飞行区域内发射；只挪本体，不改球碰撞。
            if(next==ClaudeSkill.Energy)
            {
                // Rect.Contains excludes the upper edge. Keep the muzzle just inside
                // both bounds, including transform round-off after world/local conversion.
                var b=Playfield.WorldBounds;float r=Energy.Config.CollisionRadius + .001f;
                Vector2 muzzle=Energy.Muzzle.position;
                Vector2 clamped=new Vector2(Mathf.Clamp(muzzle.x,b.xMin+r,b.xMax-r),Mathf.Clamp(muzzle.y,b.yMin+r,b.yMax-r));
                Actor.transform.position+=(Vector3)(clamped-muzzle);
            }
            bool began=next switch
            {
                ClaudeSkill.SpatialCut=>SpatialCut.Begin(Actor.Body.PhaseTwo,_random.Next()),
                ClaudeSkill.Energy=>Energy.Begin(Actor.Body.PhaseTwo,Actor.Targets[target].transform.position,target),
                ClaudeSkill.PermissionBooks=>Permissions.BeginBatch(Actor.Body.PhaseTwo,_random.Next()),
                _=>TrackingCut.Begin(Actor.Body.PhaseTwo,_random.Next())
            };
            if(!began){Debug.LogError("[ClaudeEncounter] 技能无法启动："+next,this);Cancel();return;}
            Skill=next;_hasPrevious=true;_cycleCasts++;CastsStarted++;_age=0;_stationaryGap=false;State=ClaudeEncounterState.Casting;
            Actor.SetPose(next==ClaudeSkill.Energy?ClaudePose.EnergyCharge:
                next==ClaudeSkill.PermissionBooks?ClaudePose.Seal:ClaudePose.Cut);
            CastStarted?.Invoke(next);
        }
        private void OnEnergyFired()
        {
            if(State!=ClaudeEncounterState.Casting || Skill!=ClaudeSkill.Energy)return;
            _energyReleaseRemaining=EnergyReleaseHoldSeconds;
            Actor.SetPose(ClaudePose.EnergyRelease);
            if(!Actor.Body.PhaseTwo || _secondEnergyStarted)return;
            int target=LiveTarget();
            if(target<0)return;
            Actor.FaceAt(Actor.Targets[target].transform.position);
            var bounds=Playfield.WorldBounds;float radius=SecondaryEnergy.Config.CollisionRadius+.001f;
            Vector2 muzzle=SecondaryEnergy.Muzzle.position;
            Vector2 inside=new(Mathf.Clamp(muzzle.x,bounds.xMin+radius,bounds.xMax-radius),
                Mathf.Clamp(muzzle.y,bounds.yMin+radius,bounds.yMax-radius));
            Actor.transform.position+=(Vector3)(inside-muzzle);
            _secondEnergyStarted=SecondaryEnergy.Begin(true,Actor.Targets[target].transform.position,target);
            if(!_secondEnergyStarted) { Debug.LogError("[ClaudeEncounter] 第二枚能量弹启动失败。",this); Cancel(); }
            else Actor.SetPose(ClaudePose.EnergyCharge);
        }
        private void OnSecondaryEnergyFired()
        {
            if(State==ClaudeEncounterState.Casting && Skill==ClaudeSkill.Energy)
                _energyReleaseRemaining=EnergyReleaseHoldSeconds;
        }
        private void OnEnergyCompleted()
        {
            if(Energy.State!=ClaudeEnergyState.Complete ||
                (_secondEnergyStarted && SecondaryEnergy.State!=ClaudeEnergyState.Complete)) return;
            OnCastCompleted();
        }
        private void OnCastCompleted()
        {
            if(State!=ClaudeEncounterState.Casting)return;
            if(Actor.Body.IsPhaseHealthLocked){BeginPhaseChange();return;}
            _gap=_cycleCasts>=Config.CastsPerCycle?Config.CycleGapSeconds:
                Actor.Body.PhaseTwo?Config.CastGapTwo:Config.CastGapOne;
            _gapUsesRealSeconds=Skill==ClaudeSkill.PermissionBooks;
            if(_gapUsesRealSeconds) _gap=Config.PermissionRecoverySeconds;
            // Keep the existing cycle duration and book-specific override; only the
            // ordinary five-cast cycle recovery switches from drifting to standing idle.
            _stationaryGap=_cycleCasts>=Config.CastsPerCycle && !_gapUsesRealSeconds;
            if(_cycleCasts>=Config.CastsPerCycle)_cycleCasts=0;
            State=ClaudeEncounterState.Gap;_age=0;Actor.SetPose(_stationaryGap?ClaudePose.Idle:ClaudePose.Move);
        }
        private void BeginPhaseChange()
        {
            ClearModules();Actor.BeginPhaseTransition();
            _stationaryGap=false;
            State=ClaudeEncounterState.PhaseChange;_age=0;_cycleCasts=0;
            PhaseChanged?.Invoke();
        }
        private void Move(float seconds)
        {
            _steeringAge-=seconds;
            if(_steeringAge<=0)
            {
                _steeringAge=Config.SteeringIntervalSeconds;
                float angle=(float)_random.NextDouble()*Mathf.PI*2;
                Vector2 drift=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                int target=LiveTarget();
                Vector2 away=target<0?Vector2.zero:((Vector2)Actor.transform.position-(Vector2)Actor.Targets[target].transform.position).normalized;
                _velocity=(drift+away*.55f).normalized*Config.MoveSpeed;
            }
            Vector2 offset=Actor.Body.Sphere.transform.TransformVector(Actor.Body.Sphere.offset);
            Vector2 center=(Vector2)Actor.transform.position+offset+_velocity*seconds;
            Actor.transform.position=QuickAppMotor2D.Wrap(center,Playfield.WorldBounds)-offset;
            if (State != ClaudeEncounterState.Casting || Skill == ClaudeSkill.SpatialCut || Skill == ClaudeSkill.PermissionBooks) Actor.FaceDefault();
            else if (Skill == ClaudeSkill.TrackingCut) Actor.FaceAt(TrackingCut.MarkerPosition);
            if(State==ClaudeEncounterState.Gap)Actor.SetPose(ClaudePose.Move);
        }
        private void ClearModules(){SpatialCut.Cancel();Energy.Cancel();if(SecondaryEnergy!=null)SecondaryEnergy.Cancel();TrackingCut.Cancel();Permissions.Clear();_secondEnergyStarted=false;}
        private void OnDefeated()
        {
            if(State==ClaudeEncounterState.Idle || State==ClaudeEncounterState.Complete)return;
            ClearModules();Actor.SetPose(ClaudePose.Defeated);State=ClaudeEncounterState.Complete;Completed?.Invoke();
        }
        public void Cancel(bool preserveDefeat=false)
        {
            State=ClaudeEncounterState.Idle;_age=BattleRealSeconds=0;_cycleCasts=CastsStarted=0;
            _hasPrevious=false;_steeringAge=0;_velocity=Vector2.zero;
            _gapUsesRealSeconds=false;
            _stationaryGap=false;
            _energyReleaseRemaining=0;
            if(SpatialCut!=null && Energy!=null && TrackingCut!=null && Permissions!=null)ClearModules();
            if(!preserveDefeat && Actor!=null)Actor.ResetActor();
        }
        private void OnDisable()=>Cancel();
        public void ApplyReplicaState(ClaudeEncounterState state) => State = state;
        private void OnDestroy()
        {
            if(SpatialCut!=null)SpatialCut.Completed-=OnCastCompleted;if(Energy!=null)Energy.Completed-=OnEnergyCompleted;
            if(SecondaryEnergy!=null)SecondaryEnergy.Completed-=OnEnergyCompleted;
            if(TrackingCut!=null)TrackingCut.Completed-=OnCastCompleted;if(Actor!=null)Actor.Body.Defeated-=OnDefeated;
            if(Energy!=null)Energy.Fired-=OnEnergyFired;
            if(SecondaryEnergy!=null)SecondaryEnergy.Fired-=OnSecondaryEnergyFired;
        }
    }
}
