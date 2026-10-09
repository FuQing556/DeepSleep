using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters
{
    /// <summary>独立Boss护罩：球体决定可见边界，实际命中仅驱动变暗，不替代玩家武器的命中特效。</summary>
    [DefaultExecutionOrder(310)]
    public sealed class BossBarrierFeedback2D : MonoBehaviour
    {
        public BossDamageBody2D Boss;
        public SpriteRenderer Barrier;
        public float IdleAlpha, HitAlpha, RestoreSeconds;
        [System.NonSerialized] public float EntranceAlpha = 1;
        private float _hitAge = float.MaxValue;
        private uint _replicaSequence;
        private bool _received;
        public uint Sequence { get; private set; }
        public float NormalizedAge => Mathf.Clamp01(_hitAge / RestoreSeconds);
        public float Alpha => Mathf.Lerp(HitAlpha, IdleAlpha,
            Mathf.SmoothStep(0, 1, Mathf.Clamp01(_hitAge / RestoreSeconds)));

        public bool TryValidateConfiguration(out string reason)
        {
            if (Boss == null || Barrier == null || Barrier.sprite == null ||
                !float.IsFinite(RestoreSeconds) || RestoreSeconds <= 0 ||
                !float.IsFinite(IdleAlpha) || !float.IsFinite(HitAlpha) ||
                HitAlpha < 0 || HitAlpha > IdleAlpha || IdleAlpha > 1)
            { reason = "Boss、护罩与短闪参数须显式配置。"; return false; }
            return Boss.TryValidateConfiguration(out reason);
        }
        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[BossBarrier] " + reason, this); enabled = false; return; }
            Boss.DamageAccepted += OnHit;
            Boss.FeedbackReset += ResetFeedback;
            ResetFeedback();
        }
        private void OnHit(DamagePacket damage)
        {
            if (++Sequence == 0) ++Sequence;
            _hitAge = 0;
            Render();
        }
        public void Advance(float seconds)
        {
            if (float.IsFinite(seconds) && seconds > 0) _hitAge = Mathf.Min(RestoreSeconds, _hitAge + seconds);
            Render();
        }
        private void LateUpdate() => Advance(Time.deltaTime);
        private void Render()
        {
            Barrier.enabled = Boss.IsAlive && EntranceAlpha > 0;
            if (!Barrier.enabled) return;
            var sphere = Boss.Sphere;
            Barrier.transform.position = sphere.transform.TransformPoint(sphere.offset);
            Barrier.transform.localScale = Vector3.one * (sphere.radius * 2 * Mathf.Abs(sphere.transform.lossyScale.x) / Barrier.sprite.bounds.size.x);
            Color c = Barrier.color; c.a = Alpha * EntranceAlpha; Barrier.color = c;
        }
        public void ResetFeedback()
        {
            _hitAge = float.MaxValue;
            _received = false; _replicaSequence = 0;
            if (Barrier != null) Barrier.enabled = false;
        }
        public void ApplyReplica(uint sequence, float age)
        {
            if (!float.IsFinite(age) || age < 0 || age > 1) return;
            if (sequence == 0) { if (!_received) { _hitAge = RestoreSeconds; Render(); } return; }
            if (_received && sequence == _replicaSequence) _hitAge = Mathf.Max(_hitAge, age * RestoreSeconds);
            else
            {
                if (_received && unchecked((int)(sequence - _replicaSequence)) <= 0) return;
                _received = true; _replicaSequence = sequence; _hitAge = age * RestoreSeconds;
            }
            Render();
        }
        private void OnDisable()
        {
            if (Boss != null) { Boss.DamageAccepted -= OnHit; Boss.FeedbackReset -= ResetFeedback; }
            ResetFeedback();
        }
    }
}
