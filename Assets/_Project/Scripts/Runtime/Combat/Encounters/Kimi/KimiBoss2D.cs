using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    /// <summary>本体生命/姿态。技能控制器只在招式边界提交阶段，客机镜像永不开放受击体。</summary>
    public sealed class KimiBoss2D : MonoBehaviour, IDamageReceiver, IDamageFeedbackSource
    {
        public KimiBossConfig Config;
        public Collider2D HitCollider;
        public SpriteRenderer Body;
        public SpriteRenderer Cloud;
        public SpritePoseTransition2D PoseTransition;
        public CombatPerceptionBody2D Perception;

        private CoopSessionController _session;
        private bool _authoring;
        private bool _vulnerable;
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public event Action Defeated;
        public float CurrentHealth { get; private set; }
        public float MaximumHealth => Config.MaximumHealth;
        public bool IsShown { get; private set; }
        public bool IsAlive => IsShown && CurrentHealth > 0;
        public bool PhaseTwo { get; private set; }
        public bool IsPhaseHealthLocked => !PhaseTwo && CurrentHealth <= MaximumHealth * Config.PhaseTwoHealthFraction;
        public KimiPose Pose { get; private set; }
        public bool CanReceiveDamage => isActiveAndEnabled && _authoring &&
            (_session == null || _session.Phase == SessionPhase.Offline || _session.IsAuthority) &&
            IsAlive && _vulnerable && !IsPhaseHealthLocked;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[KimiBoss] " + reason, this); enabled = false; return; }
            ResetEncounter();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config == null || HitCollider == null || !HitCollider.isTrigger || Body == null ||
                Cloud == null || PoseTransition == null || Perception == null || Perception.Shape != HitCollider)
            { reason = "本体需要显式的配置、Trigger、身体/云、残影和感知组件。"; return false; }
            return Config.TryValidate(out reason) && PoseTransition.TryValidateConfiguration(out reason);
        }

        /// <summary>章节/遭遇入口传入本局上下文；空 Session 仅供离线测试场。</summary>
        public void BeginAuthority(Vector2 position, CoopSessionController session, CombatPerceptionRegistry2D registry)
        {
            ResetEncounter();
            _session = session;
            if (session != null && session.Phase != SessionPhase.Offline && !session.IsAuthority) return;
            _authoring = true;
            CurrentHealth = MaximumHealth;
            IsShown = true;
            _vulnerable = true;
            transform.position = position;
            if (registry != null) Perception.Register(registry);
            SetPose(KimiPose.Idle);
            Render();
        }

        public void SetVulnerable(bool vulnerable)
        {
            _vulnerable = vulnerable;
            // 锁血/转阶段只禁止扣血，球体仍用于接触伤害和可见边界。
            HitCollider.enabled = IsAlive && _authoring;
        }

        public bool CommitPhaseAtSkillBoundary()
        {
            if (!IsAlive || !_authoring || PhaseTwo || CurrentHealth > MaximumHealth * Config.PhaseTwoHealthFraction)
                return false;
            PhaseTwo = true;
            return true;
        }

        public void SetPose(KimiPose pose)
        {
            Pose = pose;
            PoseTransition.TransitionTo(Config.Poses[(int)pose]);
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid) return false;
            float floor = PhaseTwo ? 0 : MaximumHealth * Config.PhaseTwoHealthFraction;
            CurrentHealth = Mathf.Max(floor, CurrentHealth - damage.Amount);
            DamageAccepted?.Invoke(damage);
            if (CurrentHealth <= 0)
            {
                HitCollider.enabled = false;
                SetPose(KimiPose.Bow);
                Defeated?.Invoke();
            }
            return true;
        }

        /// <summary>只应用已验证的权威表现；阶段不能通过本地血量自行推演。</summary>
        public void ApplyReplica(bool shown, Vector2 position, float health, bool phaseTwo, KimiPose pose)
        {
            if (!float.IsFinite(health) || health < 0 || health > MaximumHealth ||
                !float.IsFinite(position.x) || !float.IsFinite(position.y) || (int)pose >= Config.Poses.Length) return;
            _authoring = false;
            IsShown = shown;
            CurrentHealth = health;
            PhaseTwo = phaseTwo;
            transform.position = position;
            SetPose(pose);
            Render();
        }

        public void ResetEncounter(bool preserveDefeat = false)
        {
            bool defeated = preserveDefeat && Pose == KimiPose.Bow;
            _authoring = _vulnerable = IsShown = PhaseTwo = false;
            CurrentHealth = 0;
            // 保留致死姿态作为已有快照内的退场事实，不让客机把胜利与普通取消混淆。
            Pose = defeated ? KimiPose.Bow : KimiPose.Idle;
            if (HitCollider != null) HitCollider.enabled = false;
            if (Body != null) Body.enabled = false;
            if (Cloud != null) Cloud.enabled = false;
            if (Config != null && Config.Poses != null && Config.Poses.Length > 0 && PoseTransition != null)
                PoseTransition.ResetTo(Config.Poses[0]);
            FeedbackReset?.Invoke();
        }

        private void Render()
        {
            Body.enabled = Cloud.enabled = IsShown;
            HitCollider.enabled = IsAlive && _authoring;
        }

        private void OnDisable() => ResetEncounter();
    }
}
