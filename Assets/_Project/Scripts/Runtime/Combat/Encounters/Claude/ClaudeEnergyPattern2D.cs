using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    public enum ClaudeEnergyState : byte { Idle, Charging, Flying, Exploding, Complete }

    /// <summary>单球显式复用：游戏时间蓄力/扫掠/爆炸，仅飞行可受击；未来遭遇轴调用Begin。</summary>
    public sealed class ClaudeEnergyPattern2D : MonoBehaviour, IFixedSimulationStep, IDamageReceiver, IDamageFeedbackSource
    {
        public ClaudeEnergyConfig Config;
        public CombatPlayfieldConfig Playfield;
        public BossDamageBody2D Boss;
        public Transform Muzzle;
        public SpritePoseTransition2D PoseTransition;
        public Sprite IdlePose, ChargePose, ReleasePose;
        public CircleCollider2D Shape;
        public Rigidbody2D Body;
        public CombatPerceptionBody2D Perception;
        public CombatPerceptionRegistry2D Registry;
        public CompanionObstacleRegistry2D Obstacles;
        public CoopSessionController Session;
        public DamageHitbox2D[] Targets;
        public Collider2D[] TargetShapes;
        public PlayerLifeStateController2D[] TargetLives;
        public SpriteRenderer Core, Halo, BrokenCore, RadialBurst, Dissipation;
        public OneShotSpriteEffectPool2D HitEffects;
        private readonly RaycastHit2D[] _sweepHits = new RaycastHit2D[16];
        private readonly IDamageReceiver[] _victims = new IDamageReceiver[2];
        private Vector2 _direction;
        private int _aimTarget = -1;
        private float _age, _spinAge;
        private bool _registered;
        private bool _replica;
        public ClaudeEnergyState State { get; private set; }
        public Vector2 Position => transform.position;
        public Vector2 Velocity => State == ClaudeEnergyState.Flying ? _direction * Speed : Vector2.zero;
        public float Speed => Playfield.WorldBounds.width / Config.CrossArenaSeconds;
        public float ExplosionRadius => Config.ExplosionRadius;
        public float CurrentHealth { get; private set; }
        public int ExplosionCount { get; private set; }
        public int DamageApplications { get; private set; }
        public bool CanReceiveDamage => !_replica && State == ClaudeEnergyState.Flying && HasAuthority && isActiveAndEnabled;
        private bool HasAuthority => Session == null || Session.Phase == SessionPhase.Offline || Session.IsAuthority;
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public event Action Fired, Exploded, Completed;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config == null || !Config.TryValidate(out reason) || Playfield == null || !Playfield.TryValidate(out reason) ||
                Boss == null || Muzzle == null || PoseTransition == null || IdlePose == null || ChargePose == null || ReleasePose == null ||
                Shape == null || !Shape.isTrigger || Body == null || Body.bodyType != RigidbodyType2D.Kinematic ||
                Perception == null || Perception.Shape != Shape || Registry == null || Obstacles == null ||
                Core == null || Halo == null || BrokenCore == null || RadialBurst == null || Dissipation == null || HitEffects == null ||
                Targets == null || Targets.Length != 2 || TargetShapes == null || TargetShapes.Length != 2 ||
                TargetLives == null || TargetLives.Length != 2)
            { reason = "能量弹配置、显式伤害/感知/姿态及两玩家引用缺失。"; return false; }
            for (int i = 0; i < 2; i++)
                if (Targets[i] == null || TargetShapes[i] == null || TargetLives[i] == null ||
                    (Config.PlayerLayers.value & (1 << TargetShapes[i].gameObject.layer)) == 0)
                { reason = "能量弹玩家碰撞层或目标引用不匹配。"; return false; }
            if (2 * Config.CollisionRadius >= Mathf.Min(Playfield.WorldBounds.width, Playfield.WorldBounds.height))
            { reason = "能量球大于战区，无法飞行。"; return false; }
            foreach (var v in new[] { Core, Halo, BrokenCore, RadialBurst, Dissipation })
                if (v.sprite == null || v.transform == transform)
                { reason = "能量层必须有Sprite并独立于碰撞根节点。"; return false; }
            return PoseTransition.TryValidateConfiguration(out reason) && HitEffects.TryValidateConfiguration(out reason);
        }

        public bool Begin(bool phaseTwo, Vector2 target, int aimTarget = -1)
        {
            if (!isActiveAndEnabled || !HasAuthority || State == ClaudeEnergyState.Charging || State == ClaudeEnergyState.Flying ||
                State == ClaudeEnergyState.Exploding || !Boss.IsAlive || !TryValidateConfiguration(out _) ||
                !float.IsFinite(target.x) || !float.IsFinite(target.y) || aimTarget < -1 || aimTarget > 1) return false;
            Vector2 origin = Muzzle.position;
            if (!FlightBounds.Contains(origin) || (target - origin).sqrMagnitude < .000001f) return false;
            Cancel();
            _direction = (target - origin).normalized;
            _aimTarget = aimTarget;
            CurrentHealth = Mathf.Floor(phaseTwo ? Config.PhaseTwoHealth : Config.PhaseOneHealth);
            State = ClaudeEnergyState.Charging;
            transform.position = origin;
            Shape.radius = Config.CollisionRadius;
            PoseTransition.TransitionTo(ChargePose);
            Render(); return true;
        }

        private Rect FlightBounds => Rect.MinMaxRect(Playfield.WorldBounds.xMin + Config.CollisionRadius,
            Playfield.WorldBounds.yMin + Config.CollisionRadius, Playfield.WorldBounds.xMax - Config.CollisionRadius,
            Playfield.WorldBounds.yMax - Config.CollisionRadius);

        public void Simulate(float dt)
        {
            if (_replica || dt <= 0 || !float.IsFinite(dt) || !isActiveAndEnabled || State == ClaudeEnergyState.Idle || State == ClaudeEnergyState.Complete) return;
            if (!HasAuthority || !Boss.IsAlive) { Cancel(); return; }
            float remaining = dt;
            while (remaining > 0 && State != ClaudeEnergyState.Complete)
            {
                if (State == ClaudeEnergyState.Charging)
                {
                    transform.position = Muzzle.position;
                    float step = Mathf.Min(remaining, Mathf.Max(0, Config.ChargeSeconds - _age));
                    _age += step; _spinAge += step; remaining -= step;
                    if (_age < Config.ChargeSeconds) break;
                    if (!FlightBounds.Contains(Position)) { Cancel(); return; }
                    // Select the player at charge start, but sample their position only at launch.
                    // Once flying, this direction stays fixed (the ball does not home).
                    if (_aimTarget >= 0)
                    {
                        int target = _aimTarget;
                        if (TargetLives[target].State != PlayerLifeState.Alive || !Targets[target].IsActiveTarget)
                            target = 1 - target;
                        if (TargetLives[target].State == PlayerLifeState.Alive && Targets[target].IsActiveTarget)
                        {
                            Vector2 aim = (Vector2)TargetShapes[target].bounds.center - Position;
                            if (aim.sqrMagnitude > .000001f) _direction = aim.normalized;
                        }
                    }
                    State = ClaudeEnergyState.Flying; _age = 0; Shape.enabled = true;
                    Perception.Register(Registry); _registered = true;
                    Obstacles.Register(Shape, Velocity, this);
                    PoseTransition.TransitionTo(ReleasePose); Fired?.Invoke();
                }
                else if (State == ClaudeEnergyState.Flying)
                {
                    float step = Fly(remaining);
                    _spinAge += step; remaining -= step;
                    if (State == ClaudeEnergyState.Flying) break;
                }
                else
                {
                    float duration = Config.LingerSeconds + Config.FadeSeconds;
                    float step = Mathf.Min(remaining, Mathf.Max(0, duration - _age));
                    _age += step; _spinAge += step; remaining -= step;
                    if (_age < duration) break;
                    State = ClaudeEnergyState.Complete; Render(); Completed?.Invoke(); return;
                }
            }
            Render();
        }

        private float Fly(float dt)
        {
            var bounds = FlightBounds;
            Vector2 start = Position;
            float tx = _direction.x > 0 ? (bounds.xMax - start.x) / _direction.x :
                _direction.x < 0 ? (bounds.xMin - start.x) / _direction.x : float.PositiveInfinity;
            float ty = _direction.y > 0 ? (bounds.yMax - start.y) / _direction.y :
                _direction.y < 0 ? (bounds.yMin - start.y) / _direction.y : float.PositiveInfinity;
            float wall = Mathf.Max(0, Mathf.Min(tx, ty));
            float distance = Mathf.Min(dt * Speed, wall);
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(Config.PlayerLayers);
            int count = Physics2D.CircleCast(start, Config.CollisionRadius, _direction, filter, _sweepHits, distance);
            bool hit = false;
            for (int i = 0; i < count; i++)
                for (int t = 0; t < 2; t++)
                    if (_sweepHits[i].collider == TargetShapes[t] && TargetLives[t].State == PlayerLifeState.Alive &&
                        _sweepHits[i].distance <= distance)
                    { distance = _sweepHits[i].distance; hit = true; }
            transform.position = start + _direction * distance;
            float consumed = distance / Speed;
            if (hit || distance >= wall) Explode();
            else Obstacles.Register(Shape, Velocity, this);
            return consumed;
        }

        public bool TryReceiveDamage(in DamagePacket packet)
        {
            if (!CanReceiveDamage || !packet.IsValid) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - packet.Amount);
            DamageAccepted?.Invoke(packet);
            if (CurrentHealth <= 0) Explode();
            return true;
        }

        private void Explode()
        {
            if (State != ClaudeEnergyState.Flying) return;
            State = ClaudeEnergyState.Exploding; _age = 0; ExplosionCount++;
            Unregister(); Shape.enabled = false; CurrentHealth = 0;
            Array.Clear(_victims, 0, _victims.Length);
            ulong attackId = DamageAttackIdAllocator.Next(); int used = 0;
            for (int i = 0; i < 2; i++)
            {
                if (TargetLives[i].State != PlayerLifeState.Alive || !TargetShapes[i].enabled || !Targets[i].CanReceiveDamage ||
                    !Playfield.WorldBounds.Contains(TargetShapes[i].bounds.center) ||
                    !Targets[i].TryGetReceiver(out var receiver)) continue;
                Vector2 closest = TargetShapes[i].ClosestPoint(Position);
                if ((closest - Position).sqrMagnitude > ExplosionRadius * ExplosionRadius) continue;
                bool duplicate = false;
                for (int j = 0; j < used; j++) if (ReferenceEquals(_victims[j], receiver)) duplicate = true;
                if (duplicate) continue;
                _victims[used++] = receiver;
                var packet = new DamagePacket(Config.Damage, closest, (Vector2)TargetShapes[i].bounds.center - Position,
                    Boss.gameObject, attackId, DamageInterceptionPolicy.Blockable);
                if (Targets[i].TryReceiveDamage(in packet))
                {
                    DamageApplications++;
                    HitEffects.TryPlay(closest, Mathf.Atan2(packet.Direction.y, packet.Direction.x) * Mathf.Rad2Deg);
                }
            }
            PoseTransition.TransitionTo(IdlePose);
            Render(); Exploded?.Invoke();
        }

        private void Unregister()
        {
            if (_registered) Registry.Unregister(Perception);
            _registered = false;
            if (Obstacles != null && Shape != null) Obstacles.Unregister(Shape);
        }
        private void Render()
        {
            bool ball = State == ClaudeEnergyState.Charging || State == ClaudeEnergyState.Flying;
            bool blast = State == ClaudeEnergyState.Exploding;
            float charge = State == ClaudeEnergyState.Charging ? Mathf.Lerp(.18f, 1, Mathf.Clamp01(_age / Config.ChargeSeconds)) : 1;
            float fade = blast ? 1 - Mathf.Clamp01((_age - Config.LingerSeconds) / Config.FadeSeconds) : 0;
            float shell = blast ? 1 - Mathf.Clamp01(_age / Config.FadeSeconds) : ball ? 1 : 0;
            Set(Core, ball || blast, shell);
            Set(Halo, ball || blast, shell * .42f);
            Set(BrokenCore, blast, shell * .8f);
            Set(RadialBurst, blast, (1 - Mathf.Clamp01(_age / Config.FadeSeconds)) * fade);
            Set(Dissipation, blast, fade);
            Core.transform.localScale = Vector3.one * (Config.VisibleDiameter / 2.14f * charge);
            Halo.transform.localScale = Vector3.one * (Config.HaloVisibleDiameter / 2.10f * charge);
            float diameter = ExplosionRadius * 2;
            BrokenCore.transform.localScale = Vector3.one * (Config.VisibleDiameter / 2.09f);
            RadialBurst.transform.localScale = Vector3.one * (diameter / 2.32f);
            Dissipation.transform.localScale = Vector3.one * (diameter / 1.64f);
            Core.transform.localRotation = Quaternion.Euler(0, 0, _spinAge * Config.CoreSpin);
            Halo.transform.localRotation = Quaternion.Euler(0, 0, _spinAge * Config.HaloSpin);
        }
        private static void Set(SpriteRenderer visual, bool shown, float alpha)
        { if (visual == null) return; visual.enabled = shown; visual.color = new Color(1, 1, 1, alpha); }

        public void Cancel()
        {
            bool active = !_replica && (State == ClaudeEnergyState.Charging || State == ClaudeEnergyState.Flying || State == ClaudeEnergyState.Exploding);
            Unregister(); _replica = false; State = ClaudeEnergyState.Idle; _age = _spinAge = CurrentHealth = 0;
            _aimTarget = -1;
            ExplosionCount = DamageApplications = 0;
            if (Shape != null) Shape.enabled = false;
            Set(Core, false, 0); Set(Halo, false, 0); Set(BrokenCore, false, 0); Set(RadialBurst, false, 0); Set(Dissipation, false, 0);
            if (active && PoseTransition != null) PoseTransition.ResetTo(IdlePose);
            FeedbackReset?.Invoke();
        }
        public void CaptureSnapshot(ClaudeEncounterSnapshot f, bool secondary = false)
        {
            if(secondary)
            {
                f.SecondEnergyState=State;f.SecondEnergyPosition=Position;f.SecondEnergyDirection=_direction;
                f.SecondEnergyAge=_age;f.SecondEnergySpinAge=_spinAge;f.SecondEnergyHealth=CurrentHealth;return;
            }
            f.EnergyState = State; f.EnergyPosition = Position; f.EnergyDirection = _direction;
            f.EnergyAge = _age; f.EnergySpinAge = _spinAge; f.EnergyHealth = CurrentHealth;
        }
        public void ApplyReplica(ClaudeEncounterSnapshot f, bool secondary = false)
        {
            Unregister(); _replica = true; Shape.enabled = false;
            State = secondary ? f.SecondEnergyState : f.EnergyState;
            transform.position = secondary ? f.SecondEnergyPosition : f.EnergyPosition;
            _direction = secondary ? f.SecondEnergyDirection : f.EnergyDirection;
            _age = secondary ? f.SecondEnergyAge : f.EnergyAge;
            _spinAge = secondary ? f.SecondEnergySpinAge : f.EnergySpinAge;
            CurrentHealth = secondary ? f.SecondEnergyHealth : f.EnergyHealth;
            Render();
        }
        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason)) { Debug.LogError("[ClaudeEnergy] " + reason, this); enabled = false; return; }
            Cancel();
        }
        private void OnDisable() => Cancel();
    }
}
