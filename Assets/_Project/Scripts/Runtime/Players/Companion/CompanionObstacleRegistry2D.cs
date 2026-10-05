using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>
    /// 接触危险的独立登记表，不把障碍塞进敌人索敌表。
    /// 生产者显式登记/撤销；固定容量存储及调用方缓冲让感知刷新不产生托管分配。
    /// </summary>
    public sealed class CompanionObstacleRegistry2D : MonoBehaviour
    {
        public const int Capacity = 512;
        public CoopSessionController Session;

        private struct Entry
        {
            public CircleCollider2D Collider;
            public Vector2 Velocity;
            public Behaviour Source;
        }

        private readonly Entry[] _entries = new Entry[Capacity];
        private int _count;
        private bool _capacityExceeded;

        public int RegisteredCount => _count;
        public bool CanObserve => Session == null || Session.Phase == SessionPhase.Offline || Session.IsAuthority;

        public bool Register(CircleCollider2D collider, Vector2 velocity, Behaviour source)
        {
            if (collider == null || source == null || !CanObserve) return false;
            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].Collider != collider) continue;
                _entries[i] = new Entry { Collider = collider, Velocity = velocity, Source = source };
                return true;
            }
            if (_count == Capacity)
            {
                if (!_capacityExceeded)
                    Debug.LogWarning("[CompanionObstacleRegistry] 接触危险登记表已满，AI 感知按不完整处理。", this);
                _capacityExceeded = true;
                return false;
            }
            _entries[_count++] = new Entry { Collider = collider, Velocity = velocity, Source = source };
            return true;
        }

        public void Unregister(CircleCollider2D collider)
        {
            if (collider == null) return;
            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].Collider != collider) continue;
                _entries[i] = _entries[--_count];
                _entries[_count] = default;
                return;
            }
        }

        /// <summary>
        /// 复制与可观察圆相交的活动危险。中心/半径读取碰撞体世界包围盒，
        /// 速度由其模拟生产者提供，避免 MovePosition 提交前的刚体速度滞后。
        /// 超出任何容量时 saturated 为真；不把截断后的快照误报成完整安全视野。
        /// </summary>
        public int CopyVisible(Vector2 origin, float range,
            CompanionObstacleSnapshot[] destination, out bool saturated)
        {
            saturated = false;
            if (!CanObserve) return 0;
            if (!isActiveAndEnabled || destination == null || range < 0f)
            {
                saturated = true;
                return 0;
            }
            saturated = _capacityExceeded;
            int copied = 0;
            for (int i = 0; i < _count; i++)
            {
                Entry entry = _entries[i];
                CircleCollider2D collider = entry.Collider;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    entry.Source == null || !entry.Source.isActiveAndEnabled) continue;
                Rigidbody2D body = collider.attachedRigidbody;
                if (body != null && !body.simulated) continue;

                Bounds bounds = collider.bounds;
                Vector2 center = bounds.center;
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                float reach = range + radius;
                if ((center - origin).sqrMagnitude > reach * reach) continue;
                if (copied == destination.Length)
                {
                    saturated = true;
                    break;
                }
                destination[copied++] = new CompanionObstacleSnapshot(center, radius, entry.Velocity);
            }
            return copied;
        }
    }
}
