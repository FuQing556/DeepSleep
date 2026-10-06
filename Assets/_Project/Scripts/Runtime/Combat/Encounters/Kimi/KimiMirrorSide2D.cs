using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    /// <summary>一整边一个受击入口。镜条用Tiled重复纹理，长度不靠拉伸原图实现。</summary>
    public sealed class KimiMirrorSide2D : MonoBehaviour, IDamageReceiver, IDamageFeedbackSource
    {
        public BoxCollider2D Shape;
        public SpriteRenderer Visual;
        public CombatPerceptionBody2D Perception;
        public float Thickness;
        public float FlashSeconds;
        private CoopSessionController _session;
        private bool _authoring;
        private float _flash;
        public float CurrentHealth { get; private set; }
        public bool IsIntact { get; private set; }
        public bool CanReceiveDamage => IsIntact && _authoring && isActiveAndEnabled &&
            (_session == null || _session.Phase == SessionPhase.Offline || _session.IsAuthority);
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public event Action<KimiMirrorSide2D> Broken;

        public void Show(Vector2 position, float angle, float length, float health,
            CoopSessionController session, CombatPerceptionRegistry2D registry)
        {
            _session = session;
            _authoring = session == null || session.Phase == SessionPhase.Offline || session.IsAuthority;
            CurrentHealth = health; IsIntact = true; _flash = 0;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0,0,angle));
            Shape.size = new Vector2(length, Thickness);
            Shape.enabled = _authoring;
            Visual.drawMode = SpriteDrawMode.Tiled;
            Visual.size = new Vector2(length, Visual.sprite.rect.height / Visual.sprite.pixelsPerUnit);
            Visual.color = Color.white; Visual.enabled = true;
            if (registry != null) Perception.Register(registry);
            FeedbackReset?.Invoke();
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth-damage.Amount);
            _flash = FlashSeconds;
            DamageAccepted?.Invoke(damage);
            if (CurrentHealth <= 0)
            {
                IsIntact = false; Shape.enabled = false; Visual.enabled = false;
                Broken?.Invoke(this);
            }
            return true;
        }

        private void LateUpdate()
        {
            if (_flash <= 0) return;
            _flash = Mathf.Max(0,_flash-Time.deltaTime);
            Visual.color = Color.Lerp(Color.white,new Color(.7f,.8f,1),_flash/FlashSeconds);
        }

        public void Clear()
        {
            _authoring = IsIntact = false; CurrentHealth = _flash = 0;
            if (Shape != null) Shape.enabled = false;
            if (Visual != null) { Visual.enabled = false; Visual.color = Color.white; }
            FeedbackReset?.Invoke();
        }

        public void ApplyReplica(Vector2 position, float angle, float length, float health)
        {
            if (health < CurrentHealth && health > 0) _flash = FlashSeconds;
            _authoring = false; CurrentHealth = health; IsIntact = health > 0;
            Shape.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
            Visual.drawMode = SpriteDrawMode.Tiled;
            Visual.size = new Vector2(length, Visual.sprite.rect.height / Visual.sprite.pixelsPerUnit);
            Visual.enabled = IsIntact;
        }
        private void Awake() => Clear();
        private void OnDisable() => Clear();
    }
}
