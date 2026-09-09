using UnityEngine;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>
    /// 放在实际受击碰撞体旁，将命中显式转交给生命或其他伤害接收器。
    /// </summary>
    public sealed class DamageHitbox2D : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _receiverComponent;

        private IDamageReceiver _receiver;
        private bool _isInitialized;

        /// <summary>停用/入池前通知锁定方释放对象引用；已承诺的攻击可保留瞄准位置。</summary>
        public event System.Action<DamageHitbox2D> BecameUnavailable;

        private void OnDisable() => BecameUnavailable?.Invoke(this);

        public bool IsActiveTarget =>
            _isInitialized && isActiveAndEnabled && _receiver != null;

        public bool CanReceiveDamage =>
            IsActiveTarget && _receiver.CanReceiveDamage;

        public bool TryGetReceiver(out IDamageReceiver receiver)
        {
            receiver = _receiver;
            return _isInitialized && receiver != null;
        }

        private void Awake()
        {
            if (_receiverComponent is not IDamageReceiver receiver)
            {
                Debug.LogError(
                    $"[{nameof(DamageHitbox2D)}] " +
                    "接收器组件必须实现 IDamageReceiver。",
                    this);
                enabled = false;
                return;
            }

            _receiver = receiver;
            _isInitialized = true;
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            return CanReceiveDamage &&
                   _receiver.TryReceiveDamage(in damage);
        }
    }
}
