using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Projectiles;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Doubao
{
    /// <summary>单个有独立耐久的豆包气泡；运动和回池由遭遇控制器负责。</summary>
    public sealed class DoubaoWordWallBlock2D :
        MonoBehaviour,
        IDamageReceiver,
        IEnemyProjectileBlocker2D
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private CircleCollider2D _bodyCollider;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TextMesh _label;
        [SerializeField] private DamageHitbox2D _damageHitbox;
        [SerializeField, Min(1)] private int _charactersPerLine = 3;
        private string _phrase;

        private float _currentHealth;
        private float _fallSpeed;
        private float _despawnY;
        private float _contactDamage;
        private bool _isActive;
        private bool _contactConsumed;
        private uint _replicationId;
        private Action<DoubaoWordWallBlock2D> _returnRequested;

        public bool CanReceiveDamage => _isActive && _currentHealth > 0f;
        public bool IsActive => _isActive;
        public uint ReplicationId => _replicationId;
        private Vector2 _size;
        public Vector2 Size => _size;
        public event Action<Vector2, bool> Popped;
        public event Action<Vector2> Impacted;
        public string Phrase => _phrase ?? string.Empty;

        private void Awake()
        {
            if (_body == null || _bodyCollider == null || _renderer == null || _label == null || _damageHitbox == null ||
                _body.bodyType != RigidbodyType2D.Kinematic || !_bodyCollider.isTrigger)
            {
                Debug.LogError($"[{nameof(DoubaoWordWallBlock2D)}] 必须显式配置 Kinematic Rigidbody2D、Trigger Collider2D、SpriteRenderer 与 TextMesh。", this);
                enabled = false;
            }
        }

        public void Activate(
            Vector2 position,
            uint replicationId,
            float width,
            float height,
            string phrase,
            float maximumHealth,
            float fallSpeed,
            float despawnY,
            float contactDamage,
            Action<DoubaoWordWallBlock2D> returnRequested)
        {
            _replicationId = replicationId;
            _currentHealth = Mathf.Max(1f, Mathf.Floor(maximumHealth));
            _fallSpeed = fallSpeed;
            _despawnY = despawnY;
            _contactDamage = contactDamage;
            _returnRequested = returnRequested;
            _contactConsumed = false;
            _isActive = true;
            transform.position = position;
            // 圆图只等比缩放视觉子节点；文字与物理根独立。
            transform.localScale = Vector3.one;
            _size = new Vector2(width, width);
            _renderer.transform.localScale = Vector3.one * (width / _renderer.sprite.bounds.size.x);
            _bodyCollider.radius = width * 0.5f;
            _phrase = phrase;
            _label.text = FormatPhrase(phrase, _charactersPerLine);
            gameObject.SetActive(true);
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            _bodyCollider.enabled = true;
        }

        public void Simulate(float deltaTime)
        {
            if (!_isActive || deltaTime <= 0f) return;
            Vector2 next = _body.position + Vector2.down * (_fallSpeed * deltaTime);
            _body.MovePosition(next);
            if (next.y <= _despawnY) RequestReturn();
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid) return false;
            _currentHealth = Mathf.Max(0f, _currentHealth - damage.Amount);
            if (_currentHealth <= 0f) { Popped?.Invoke(_body.position, false); RequestReturn(); }
            else Impacted?.Invoke(damage.HitPoint);
            return true;
        }

        public bool TryBlockEnemyProjectile(EnemyProjectile2D projectile, Vector2 hitPoint)
        {
            if (!_isActive || projectile == null) return false;
            Impacted?.Invoke(hitPoint);
            return true;
        }

        public void ReturnToPool()
        {
            _isActive = false;
            _contactConsumed = false;
            _currentHealth = 0f;
            _replicationId = 0;
            _fallSpeed = 0f;
            _returnRequested = null;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _bodyCollider.enabled = false;
            _label.text = string.Empty;
            _phrase = string.Empty;
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive || _contactConsumed || other == null ||
                other.TryGetComponent(out RiceProjectile _) ||
                other.TryGetComponent(out EnemyProjectile2D _) ||
                !other.TryGetComponent(out DamageHitbox2D hitbox) ||
                !hitbox.TryGetReceiver(out IDamageReceiver receiver) ||
                ReferenceEquals(receiver, this) ||
                receiver is DoubaoWordWallBlock2D ||
                receiver is DoubaoBoss2D)
            {
                return;
            }

            Vector2 point = other.ClosestPoint(_body.position);
            Vector2 direction = ((Vector2)other.bounds.center - _body.position).normalized;
            DamagePacket damage = new DamagePacket(
                _contactDamage,
                point,
                direction,
                gameObject,
                DamageAttackIdAllocator.Next(),
                DamageInterceptionPolicy.Blockable,
                suppressKillReward: true);
            bool accepted = hitbox.TryReceiveDamage(in damage);
            Popped?.Invoke(accepted ? point : _body.position, accepted);
            _contactConsumed = true;
            RequestReturn();
        }

        private void RequestReturn()
        {
            if (!_isActive) return;
            _isActive = false;
            _bodyCollider.enabled = false;
            _returnRequested?.Invoke(this);
        }

        public void ClearWithEffect()
        {
            if (!_isActive) return;
            Popped?.Invoke(_body.position, false);
            RequestReturn();
        }

        public static string FormatPhrase(string phrase, int charactersPerLine)
        {
            if (string.IsNullOrEmpty(phrase) || phrase.Length <= charactersPerLine) return phrase;
            var result = new System.Text.StringBuilder(phrase.Length + phrase.Length / charactersPerLine);
            for (int index = 0; index < phrase.Length; index++)
            {
                if (index > 0 && index % charactersPerLine == 0) result.Append('\n');
                result.Append(phrase[index]);
            }
            return result.ToString();
        }
    }
}
