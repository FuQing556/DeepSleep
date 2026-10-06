using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    /// <summary>次数光幕：一次有效命中扣一次，不把伤害值当成次数。</summary>
    public sealed class KimiHitCurtain2D : MonoBehaviour, IDamageReceiver, IDamageFeedbackSource
    {
        public BoxCollider2D Shape;
        public SpriteRenderer Visual;
        public CombatPerceptionBody2D Perception;
        public int Remaining { get; private set; }
        public int Maximum { get; private set; }
        private CoopSessionController _session;
        private bool _replica;
        private readonly HashSet<ulong> _attackIds = new();
        public bool CanReceiveDamage => !_replica && Remaining > 0 && isActiveAndEnabled &&
            (_session == null || _session.Phase == SessionPhase.Offline || _session.IsAuthority);
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public event Action Broken;

        public void Show(Vector2 position, int hits, CoopSessionController session, CombatPerceptionRegistry2D registry)
        {
            Clear(); _session = session;
            if (session != null && session.Phase != SessionPhase.Offline && !session.IsAuthority) return;
            transform.position = position; Maximum = Remaining = hits;
            _attackIds.EnsureCapacity(hits);
            Shape.enabled = Visual.enabled = hits > 0;
            if (registry != null) Perception.Register(registry);
        }
        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid || (damage.AttackId != 0 && !_attackIds.Add(damage.AttackId))) return false;
            // 旧玩家攻击由各执行器去重且可能不携带ID；不能把所有0号攻击合并成一次。
            Remaining--;
            var feedback = new DamagePacket(1, damage.HitPoint, damage.Direction, damage.Source, damage.AttackId);
            DamageAccepted?.Invoke(feedback);
            if (Remaining == 0) { Shape.enabled = Visual.enabled = false; Broken?.Invoke(); }
            return true;
        }
        public void Clear()
        {
            _replica = false;
            Remaining = Maximum = 0; _attackIds.Clear();
            if (Shape != null) Shape.enabled = false;
            if (Visual != null) Visual.enabled = false;
            FeedbackReset?.Invoke();
        }
        public void ApplyReplica(Vector2 position, int remaining, int maximum)
        {
            _replica = true; Remaining = remaining; Maximum = maximum;
            transform.position = position; Shape.enabled = false; Visual.enabled = remaining > 0;
        }
        private void Awake() => Clear();
        private void OnDisable() => Clear();
    }
}
