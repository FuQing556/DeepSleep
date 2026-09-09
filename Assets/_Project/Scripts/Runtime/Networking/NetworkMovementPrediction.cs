using System.Collections.Generic;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>仅本机角色的位置预测。收到主机 ACK 后丢弃已执行输入并重放剩余输入。</summary>
    public sealed class NetworkMovementPrediction : MonoBehaviour
    {
        public NetworkPlayerReplica Replica;
        public PlayerMotorConfig Motor;
        public Collider2D BodyCollider;
        private struct Pending { public uint Sequence; public Vector2 Move; public float Dt; }
        private readonly List<Pending> _pending = new(128);
        private Vector2 _position, _velocity, _extents, _offset;
        private bool _ready, _blocked;
        public Vector2 Position => _position;
        public bool Active => _ready && !Replica.Session.IsAuthority && !Replica.Session.LocalAi &&
            Replica.Session.LocalRole == Replica.Role && Replica.Session.Phase == SessionPhase.Playing;
        private void Awake()
        {
            if (Replica == null || Motor == null || BodyCollider == null)
            { Debug.LogError("[NetworkMovementPrediction] 配置不完整", this); enabled = false; return; }
            // 在客人关闭物理模拟之前缓存原碰撞体范围，不改用户数据。
            _extents = BodyCollider.bounds.extents;
            _offset = (Vector2)BodyCollider.bounds.center - Replica.Body.position;
        }
        private void OnEnable()
        { Replica.Session.LocalCommandSent += Predict; Replica.Session.SessionOpened += Reset; }
        private void OnDisable()
        { Replica.Session.LocalCommandSent -= Predict; Replica.Session.SessionOpened -= Reset; }
        private void Reset(bool authority) { _ready = false; _pending.Clear(); }
        private void Predict(PlayerCommand command)
        {
            if (!Active || _blocked) return;
            if (_pending.Count >= 128) { _ready = false; _pending.Clear(); return; }
            var step = new Pending { Sequence = command.Sequence, Move = command.Move, Dt = Time.fixedDeltaTime };
            _pending.Add(step); Step(step);
        }
        private void Step(Pending step)
        {
            if (_blocked) { _velocity = Vector2.zero; return; }
            PlayerMovementStep.Calculate(ref _position, ref _velocity, step.Move, Motor, _extents, _offset, step.Dt);
            _position += _velocity * step.Dt;
        }
        public void Reconcile(uint ack, Vector2 position, Vector2 velocity, bool blocked)
        {
            _position = position; _velocity = velocity; _blocked = blocked; _ready = true;
            if (!Active || _blocked) { _pending.Clear(); return; }
            int completed = 0;
            while (completed < _pending.Count && !RemoteCommandSource.IsNewer(_pending[completed].Sequence, ack)) completed++;
            if (completed > 0) _pending.RemoveRange(0, completed);
            foreach (var step in _pending) Step(step);
        }
    }
}
