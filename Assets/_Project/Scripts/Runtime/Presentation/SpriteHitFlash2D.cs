using System;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>
    /// 实际扣血驱动的单主体短闪。Sources 显式保留全部基础视觉，首项固定为身体；
    /// 网络只复制 Sources，Overlay 永不进入基础快照。该组件不影响判定、动作或生命。
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class SpriteHitFlash2D : MonoBehaviour
    {
        public MonoBehaviour DamageSource;
        public bool ReplicaOnly;
        public SpriteRenderer[] Sources = Array.Empty<SpriteRenderer>();
        public SpriteRenderer Overlay;
        [SerializeField, Min(.01f)] private float _duration = .16f;
        [SerializeField] private Color _tint = new(1f, .72f, .65f, 1f);
        [SerializeField, Range(0f, 1f)] private float _peakAlpha = .65f;
        [SerializeField, Range(0f, 1f)] private float _reducedAlpha = .18f;
        private IDamageFeedbackSource _source;
        private float _elapsed = float.MaxValue;
        private int _lastFrame;
        private uint _lastReplicaSequence;
        private bool _hasReplicaSequence;

        public uint Sequence { get; private set; }
        public uint PlayedCount { get; private set; }
        public float NormalizedAge => _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
        public bool IsPlaying => isActiveAndEnabled && NormalizedAge < 1f;

        public bool TryValidateConfiguration(out string reason)
        {
            if ((!ReplicaOnly && DamageSource is not IDamageFeedbackSource) ||
                (ReplicaOnly && DamageSource != null) || Sources == null || Sources.Length == 0 ||
                Overlay == null || Overlay.sharedMaterial == null || !Finite(_duration) || _duration <= 0f ||
                !Finite(_peakAlpha) || !Finite(_reducedAlpha) ||
                _peakAlpha < 0f || _peakAlpha > 1f || _reducedAlpha < 0f || _reducedAlpha > _peakAlpha ||
                !Finite(_tint.r) || !Finite(_tint.g) || !Finite(_tint.b))
            { reason = "受击短闪的事实源、基础精灵、独立覆盖层或参数不完整。"; return false; }
            for (int i = 0; i < Sources.Length; i++)
            {
                if (Sources[i] == null || Sources[i] == Overlay || Array.IndexOf(Sources, Sources[i]) != i)
                { reason = "基础视觉不能为空、重复或包含受击覆盖层。"; return false; }
            }
            Transform subject = Sources[0].transform;
            if (Overlay.transform.parent != subject.parent && Overlay.transform.parent != subject)
            { reason = "覆盖层必须与身体同父节点，或作为根身体的直接子节点。"; return false; }
            reason = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[SpriteHitFlash] " + reason, this); enabled = false; return; }
            ResetFeedback();
            _source = DamageSource as IDamageFeedbackSource;
            if (_source == null) return;
            _source.DamageAccepted += OnDamageAccepted;
            _source.FeedbackReset += ResetFeedback;
        }

        private void OnDisable()
        {
            if (_source != null)
            {
                _source.DamageAccepted -= OnDamageAccepted;
                _source.FeedbackReset -= ResetFeedback;
                _source = null;
            }
            ResetFeedback();
        }

        private void OnDamageAccepted(DamagePacket damage)
        {
            if (!isActiveAndEnabled) return;
            // 序号跨回池/重试保持单调；0 专用于没有命中事实的镜像。
            unchecked { Sequence++; if (Sequence == 0) Sequence++; }
            Begin(0f);
        }

        private void Begin(float normalizedAge)
        {
            _elapsed = normalizedAge * _duration;
            _lastFrame = Time.frameCount;
            if (normalizedAge < 1f) PlayedCount++;
            RenderNow();
        }

        /// <summary>仅由已验证的实体/遭遇快照调用；重复序号只能推进龄期，不能重新点亮。</summary>
        public void ApplyReplica(uint sequence, float normalizedAge)
        {
            if (!isActiveAndEnabled || !Finite(normalizedAge) || normalizedAge < 0f || normalizedAge > 1f) return;
            if (sequence == 0)
            {
                // 已收过命中后，迟到的“未命中”帧不能抹掉较新的表现。
                if (!_hasReplicaSequence) ResetFeedback();
                return;
            }
            if (_hasReplicaSequence)
            {
                if (sequence == _lastReplicaSequence)
                {
                    _elapsed = Mathf.Max(_elapsed, normalizedAge * _duration);
                    RenderNow();
                    return;
                }
                if (unchecked((int)(sequence - _lastReplicaSequence)) <= 0) return;
            }
            _hasReplicaSequence = true;
            _lastReplicaSequence = sequence;
            Begin(normalizedAge);
        }

        /// <summary>清理动画与镜像接收基线，不重置本实例的权威命中序号。</summary>
        public void ResetFeedback()
        {
            _elapsed = float.MaxValue;
            _hasReplicaSequence = false;
            _lastReplicaSequence = 0;
            _lastFrame = Time.frameCount;
            if (Overlay != null) Overlay.enabled = false;
        }

        private void LateUpdate() => AdvanceFrame(Time.frameCount, Time.deltaTime);

        private void AdvanceFrame(int frame, float deltaTime)
        {
            // 出生帧不扣掉整帧 deltaTime；低帧率下也至少有一次可渲染机会。
            if (_lastFrame != frame)
            {
                _lastFrame = frame;
                if (_elapsed < _duration) _elapsed += deltaTime;
            }
            RenderNow();
        }

        public void RenderNow()
        {
            if (Overlay == null || Sources == null || Sources.Length == 0 || Sources[0] == null) return;
            if (NormalizedAge >= 1f || !isActiveAndEnabled)
            { Overlay.enabled = false; return; }
            SpriteRenderer body = Sources[0];
            float alpha = (1f - NormalizedAge) *
                (PlayerHitFeedbackOptions.ReduceFlash ? _reducedAlpha : _peakAlpha);
            Overlay.enabled = isActiveAndEnabled && alpha > 0f && body.enabled &&
                body.sprite != null && body.gameObject.activeInHierarchy;
            if (!Overlay.enabled) return;
            Overlay.sprite = body.sprite;
            Overlay.flipX = body.flipX;
            Overlay.flipY = body.flipY;
            if (Overlay.transform.parent == body.transform)
            {
                Overlay.transform.localPosition = Vector3.zero;
                Overlay.transform.localRotation = Quaternion.identity;
                Overlay.transform.localScale = Vector3.one;
            }
            else
            {
                Overlay.transform.localPosition = body.transform.localPosition;
                Overlay.transform.localRotation = body.transform.localRotation;
                Overlay.transform.localScale = body.transform.localScale;
            }
            Overlay.sortingLayerID = body.sortingLayerID;
            Overlay.sortingOrder = body.sortingOrder + 2;
            Overlay.color = new Color(_tint.r, _tint.g, _tint.b, alpha * body.color.a);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
