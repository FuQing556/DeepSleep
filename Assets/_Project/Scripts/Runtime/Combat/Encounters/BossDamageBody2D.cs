using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters
{
    /// <summary>Boss通用球体受击/阶段锁血核心；不持有姿态、技能、掉落或隐藏生命默认值。</summary>
    public sealed class BossDamageBody2D : MonoBehaviour, IDamageReceiver, IDamageFeedbackSource
    {
        public CircleCollider2D Sphere;
        public CombatPerceptionBody2D Perception;
        private CoopSessionController _session;
        private bool _authority, _vulnerable;
        private float _phaseFraction;
        public float MaximumHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsShown { get; private set; }
        public bool IsAlive => IsShown && CurrentHealth > 0;
        public bool PhaseTwo { get; private set; }
        public bool IsPhaseHealthLocked => !PhaseTwo && CurrentHealth <= MaximumHealth * _phaseFraction;
        public bool CanReceiveDamage => isActiveAndEnabled && _authority &&
            (_session == null || _session.Phase == SessionPhase.Offline || _session.IsAuthority) &&
            IsAlive && _vulnerable && !IsPhaseHealthLocked;
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public event Action Defeated;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[BossDamageBody] " + reason, this); enabled = false; return; }
            ResetEncounter();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Sphere == null || !Sphere.isTrigger || Perception == null || Perception.Shape != Sphere ||
                !float.IsFinite(Sphere.radius) || Sphere.radius <= 0)
            { reason = "球体Trigger、半径和感知形状必须显式一致。"; return false; }
            reason = string.Empty; return true;
        }

        /// <summary>生命与阶段阈值来自遭遇配置；未调用前不能受伤或参与接触。</summary>
        public bool BeginAuthority(float maximumHealth, float phaseTwoFraction,
            CoopSessionController session, CombatPerceptionRegistry2D registry)
        {
            ResetEncounter();
            if (!float.IsFinite(maximumHealth) || maximumHealth < 1 || !float.IsFinite(phaseTwoFraction) ||
                phaseTwoFraction <= 0 || phaseTwoFraction >= 1 ||
                (session != null && session.Phase != SessionPhase.Offline && !session.IsAuthority)) return false;
            _session = session; _authority = _vulnerable = IsShown = true;
            MaximumHealth = CurrentHealth = Mathf.Floor(maximumHealth);
            _phaseFraction = phaseTwoFraction;
            Sphere.enabled = true;
            if (registry != null) Perception.Register(registry);
            return true;
        }

        public void SetVulnerable(bool vulnerable) => _vulnerable = vulnerable;

        public bool CommitPhaseAtSkillBoundary()
        {
            if (!_authority || !IsAlive || PhaseTwo || !IsPhaseHealthLocked) return false;
            PhaseTwo = true;
            return true;
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid) return false;
            CurrentHealth = Mathf.Max(PhaseTwo ? 0 : MaximumHealth * _phaseFraction, CurrentHealth - damage.Amount);
            DamageAccepted?.Invoke(damage);
            if (CurrentHealth <= 0) { Sphere.enabled = false; Defeated?.Invoke(); }
            return true;
        }

        /// <summary>应用已验证权威状态；客机绝不开启伤害球体，不自行推断转阶段。</summary>
        public bool ApplyReplica(float maximumHealth, float health, bool shown, bool phaseTwo)
        {
            if (!float.IsFinite(maximumHealth) || maximumHealth < 1 || !float.IsFinite(health) || health < 0 || health > maximumHealth)
                return false;
            _authority = _vulnerable = false;
            MaximumHealth = maximumHealth; CurrentHealth = health; IsShown = shown; PhaseTwo = phaseTwo;
            Sphere.enabled = false;
            return true;
        }

        public void ResetEncounter()
        {
            _authority = _vulnerable = IsShown = PhaseTwo = false;
            _session = null; MaximumHealth = CurrentHealth = _phaseFraction = 0;
            if (Sphere != null) Sphere.enabled = false;
            FeedbackReset?.Invoke();
        }
        private void OnDisable() => ResetEncounter();
    }
}
