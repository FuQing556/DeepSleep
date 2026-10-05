using System;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Doubao
{
    /// <summary>豆包本体的轻量生命入口；定位和生成节奏由遭遇控制器决定。</summary>
    public sealed class DoubaoBoss2D : MonoBehaviour, IDamageReceiver, IDamageFeedbackSource
    {
        [SerializeField] private Collider2D _hitCollider;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private DeepSleep.Runtime.Presentation.Poses.SpritePoseTransition2D _poseTransition;
        [SerializeField] private Sprite _idleSprite;
        [SerializeField] private Sprite _lectureSprite;
        [SerializeField] private Sprite _departureSprite;
        [SerializeField, Min(0.1f)] private float _poseSeconds = 0.7f;
        [SerializeField, Min(0.1f)] private float _departureSeconds = 1.2f;
        [SerializeField, Range(0f, 0.9f)] private float _departureHold = 0.3f;
        private float _visualElapsed;
        private bool _departing;
        public bool IsDeparting => _departing;
        public float VisualElapsed => _visualElapsed;

        private float _currentHealth;
        private bool _isActive;

        public event Action<DoubaoBoss2D> Defeated;
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public bool CanReceiveDamage => _isActive && _currentHealth > 0f;
        public bool IsActive => _isActive;
        public float CurrentHealth => _currentHealth;

        private void Awake()
        {
            if (_hitCollider == null || _renderer == null || !_hitCollider.isTrigger ||
                _poseTransition == null || _idleSprite == null || _lectureSprite == null || _departureSprite == null)
            {
                Debug.LogError($"[{nameof(DoubaoBoss2D)}] 必须显式配置 Trigger Collider2D 与 SpriteRenderer。", this);
                enabled = false;
            }
        }

        public void Activate(Vector2 position, float maximumHealth)
        {
            transform.position = position;
            _currentHealth = Mathf.Max(1f, Mathf.Floor(maximumHealth));
            _isActive = true;
            _departing = false;
            _visualElapsed = 0f;
            gameObject.SetActive(true);
            _renderer.enabled = true;
            _renderer.color = Color.white;
            _poseTransition.ResetTo(_idleSprite);
            _hitCollider.enabled = true;
            FeedbackReset?.Invoke();
        }

        public void PlaceAt(Vector2 position)
        {
            if (_isActive || _departing) transform.position = position;
        }

        public void ApplyReplica(bool shown, Vector2 position, float currentHealth, bool departing = false, float visualElapsed = 0f)
        {
            _isActive = shown;
            _departing = departing;
            _visualElapsed = visualElapsed;
            _currentHealth = shown ? Mathf.Max(0f, currentHealth) : 0f;
            transform.position = position;
            gameObject.SetActive(shown || departing);
            // 客户端只显示权威镜像，永远不开放本地伤害碰撞体。
            _hitCollider.enabled = false;
            _renderer.enabled = shown || departing;
            if (!shown && !departing) _poseTransition.ResetTo(_idleSprite);
            else RenderPose();
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid) return false;
            _currentHealth = Mathf.Max(0f, _currentHealth - damage.Amount);
            DamageAccepted?.Invoke(damage);
            if (_currentHealth <= 0f)
            {
                _isActive = false;
                _hitCollider.enabled = false;
                _departing = true;
                _visualElapsed = 0f;
                RenderPose();
                Defeated?.Invoke(this);
            }
            return true;
        }

        public void ResetEncounter()
        {
            FeedbackReset?.Invoke();
            _isActive = false;
            _departing = false;
            _visualElapsed = 0f;
            _currentHealth = 0f;
            _hitCollider.enabled = false;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_isActive && !_departing) return;
            _visualElapsed += Time.deltaTime;
            RenderPose();
            if (_departing && _visualElapsed >= _departureSeconds)
            {
                _departing = false;
                gameObject.SetActive(false);
            }
        }

        private void RenderPose()
        {
            Sprite pose = _departing ? _departureSprite :
                ((int)(_visualElapsed / _poseSeconds) % 2 == 0 ? _idleSprite : _lectureSprite);
            _poseTransition.TransitionTo(pose);
            float alpha = _departing ? 1f - Mathf.InverseLerp(_departureHold, 1f, _visualElapsed / _departureSeconds) : 1f;
            _renderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
