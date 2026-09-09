using DeepSleep.Runtime.Players.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive.Presentation
{
    /// <summary>复活保护的世界表现；只读伤害门，独立排序，不参与碰撞或命中特效倍率。</summary>
    public sealed class PlayerReviveProtectionView2D : MonoBehaviour
    {
        [SerializeField] private PlayerDamageReceiver2D _receiver;
        [SerializeField] private Transform _followTarget;
        [SerializeField] private SpriteRenderer _shield;
        [SerializeField] private Vector2 _offset;
        [SerializeField, Range(0, 1)] private float _opacity = 0.3f;
        private bool _remote, _remoteProtected;
        public void ApplyReplicaProtection(bool value) { _remote = true; _remoteProtected = value; }

        private void Awake()
        {
            if (_receiver != null && _followTarget != null && _shield != null) return;
            Debug.LogError("[ReviveProtection] 必须配置伤害门、跟随根和护盾SpriteRenderer。", this);
            enabled = false;
        }

        private void LateUpdate()
        {
            _shield.enabled = _remote ? _remoteProtected : _receiver.IsReviveProtected;
            if (!_shield.enabled) return;
            transform.position = _followTarget.position + (Vector3)_offset;
            var color = _shield.color;
            color.a = _opacity;
            _shield.color = color;
        }

        private void OnDisable()
        {
            if (_shield != null) _shield.enabled = false;
        }
    }
}
