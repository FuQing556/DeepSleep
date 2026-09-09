using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Perception
{
    /// <summary>场景级、显式注入的碰撞体索引。查询走物理宽阶段，不遍历全场敌人。</summary>
    public sealed class CombatPerceptionRegistry2D : MonoBehaviour
    {
        private readonly Dictionary<Collider2D, CombatPerceptionBody2D> _bodies = new();
        public void Register(CombatPerceptionBody2D body) => _bodies[body.Shape] = body;
        public void Unregister(CombatPerceptionBody2D body)
        {
            if (body.Shape != null) _bodies.Remove(body.Shape);
        }
        public bool TryResolve(Collider2D shape, out CombatPerceptionBody2D body) =>
            _bodies.TryGetValue(shape, out body);
    }
}
