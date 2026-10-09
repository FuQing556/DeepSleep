using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Presentation.Effects;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    /// <summary>本体姿态与球罩接触；生命只存在Body中，由遭遇显式启动/推进。</summary>
    public sealed class ClaudeBossActor2D : MonoBehaviour
    {
        public ClaudeBossConfig Config;
        public SpriteRenderer FacingVisual;
        public Transform EnergyMuzzle;
        private Vector3 _leftMuzzle;

        public void FaceAt(Vector2 target) => SetFacing(target.x > transform.position.x);
        public void FaceDefault() => SetFacing(transform.position.x < 0);
        private void SetFacing(bool right)
        {
            if (FacingVisual != null) FacingVisual.flipX = right;
            if (EnergyMuzzle != null)
                EnergyMuzzle.localPosition = new Vector3(right ? Mathf.Abs(_leftMuzzle.x) : -Mathf.Abs(_leftMuzzle.x), _leftMuzzle.y, _leftMuzzle.z);
        }
        public BossDamageBody2D Body;
        public SpritePoseTransition2D PoseTransition;
        public OneShotSpriteEffectPool2D HitEffects;
        public DamageHitbox2D[] Targets;
        public Collider2D[] TargetShapes;
        public PlayerLifeStateController2D[] TargetLives;
        private readonly float[] _contactCooldowns = new float[2];
        private CoopSessionController _session;
        public ClaudePose Pose { get; private set; }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config == null || Body == null || PoseTransition == null || HitEffects == null ||
                Targets == null || TargetShapes == null || TargetLives == null ||
                Targets.Length != 2 || TargetShapes.Length != 2 || TargetLives.Length != 2)
            { reason = "Claude本体及两个玩家须显式装配。"; return false; }
            for (int i = 0; i < 2; i++)
                if (Targets[i] == null || TargetShapes[i] == null || TargetLives[i] == null)
                { reason = "Claude接触目标引用缺失。"; return false; }
            return Config.TryValidate(out reason) && Body.TryValidateConfiguration(out reason) &&
                PoseTransition.TryValidateConfiguration(out reason);
        }

        private void Awake()
        {
            if (EnergyMuzzle != null) _leftMuzzle = EnergyMuzzle.localPosition;
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[ClaudeBoss] " + reason, this); enabled = false; return; }
            Body.Defeated += OnDefeated;
            ResetActor();
        }

        public bool BeginAuthority(CoopSessionController session, CombatPerceptionRegistry2D registry)
        {
            ResetActor();
            _session = session;
            return Body.BeginAuthority(Config.MaximumHealth, Config.PhaseTwoHealthFraction, session, registry);
        }

        public void SetPose(ClaudePose pose)
        {
            Pose = pose;
            // 结算暂停后普通动作残影不再推进；胜利退场由独立表现按现实时间渐隐。
            if (pose == ClaudePose.Defeated) PoseTransition.ResetTo(Config.Poses[(int)pose]);
            else PoseTransition.TransitionTo(Config.Poses[(int)pose]);
        }

        public void BeginPhaseTransition()
        { Body.SetVulnerable(false); SetPose(ClaudePose.PhaseChange); }

        public bool CompletePhaseTransition()
        {
            if (!Body.CommitPhaseAtSkillBoundary()) return false;
            SetPose(ClaudePose.Idle); Body.SetVulnerable(true); return true;
        }

        public void SimulateContact(float seconds)
        {
            if (!isActiveAndEnabled || !Body.IsAlive || !Body.Sphere.enabled ||
                !float.IsFinite(seconds) || seconds <= 0 ||
                (_session != null && _session.Phase != SessionPhase.Offline && !_session.IsAuthority)) return;
            for (int i = 0; i < 2; i++)
            {
                _contactCooldowns[i] = Mathf.Max(0, _contactCooldowns[i] - seconds);
                if (_contactCooldowns[i] > 0 || !Targets[i].IsActiveTarget || !TargetShapes[i].enabled ||
                    TargetLives[i].State != PlayerLifeState.Alive || !Body.Sphere.Distance(TargetShapes[i]).isOverlapped) continue;
                _contactCooldowns[i] = Config.ContactIntervalSeconds;
                Vector2 center = Body.Sphere.bounds.center;
                Vector2 point = TargetShapes[i].ClosestPoint(center);
                Vector2 direction = (Vector2)Targets[i].transform.position - center;
                var damage = new DamagePacket(Config.ContactDamage, point, direction, gameObject,
                    DamageAttackIdAllocator.Next(), DamageInterceptionPolicy.Blockable,
                    knockbackDistance: Config.ContactKnockbackDistance, knockbackSeconds: Config.ContactKnockbackSeconds);
                if (Targets[i].TryReceiveDamage(in damage))
                    HitEffects.TryPlay(point, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }

        public void ResetActor()
        {
            _session = null;
            for (int i = 0; i < 2; i++) _contactCooldowns[i] = 0;
            Body.ResetEncounter(); Pose = ClaudePose.Idle; PoseTransition.ResetTo(Config.Poses[0]);
            FaceDefault();
        }
        private void OnDefeated() => SetPose(ClaudePose.Defeated);
        private void OnDestroy() { if (Body != null) Body.Defeated -= OnDefeated; }
    }
}
