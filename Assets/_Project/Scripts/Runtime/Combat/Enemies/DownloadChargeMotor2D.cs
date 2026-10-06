using DeepSleep.Runtime.Combat.Targeting;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    public enum DownloadChargeState : byte { Entering, Charging, Dashing, Recovery }

    /// <summary>进入战区后原地蓄力；只在蓄力期间追踪，冲刺方向在起冲那刻冻结。</summary>
    public sealed class DownloadChargeMotor2D : EnemyMotor2D
    {
        public Rigidbody2D Body;
        public CombatPlayfieldConfig Playfield;
        public DownloadChargeConfig Config;
        public EnemyContactAttack2D Contact;
        private readonly RaycastHit2D[] _contacts = new RaycastHit2D[8];
        private Vector2 _direction;
        private float _entrySpeed, _remaining;
        private bool _running;
        public DownloadChargeState State { get; private set; }
        public float ChargeProgress => State == DownloadChargeState.Charging
            ? 1f - _remaining / Config.ChargeSeconds : 0f;
        public override bool IsRunning => _running;
        public override Vector2 TravelDirection => _direction;

        public override void Begin(Vector2 position, in EnemySpawnVariation2D variation)
        {
            Body.position = position; Body.rotation = 0;
            _direction = variation.TravelDirection; _entrySpeed = variation.TravelSpeed;
            _remaining = 0; State = DownloadChargeState.Entering; _running = true;
        }
        public override void Stop()
        {
            _running = false; _remaining = 0;
            Body.linearVelocity = Vector2.zero; Body.angularVelocity = 0;
        }
        private void OnDisable() { if (Body != null) Stop(); }

        public override void Simulate(float deltaTime)
        {
            if (!_running || deltaTime <= 0) return;
            Vector2 position = Body.position;
            if (State == DownloadChargeState.Entering)
            {
                if (Playfield.WorldBounds.Contains(position)) Enter(DownloadChargeState.Charging, Config.ChargeSeconds);
                else { Body.MovePosition(position + _direction * (_entrySpeed * deltaTime)); return; }
            }
            if (State == DownloadChargeState.Charging && PlayerCombatTarget2D.TryFindNearest(position, out var target))
            {
                Vector2 offset = target.Position - position;
                if (offset.sqrMagnitude > .0001f) _direction = offset.normalized;
            }
            if (State == DownloadChargeState.Dashing)
            {
                float distance = Config.DashSpeed * Mathf.Min(deltaTime, _remaining);
                var filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(Config.PlayerLayers);
                int count = Physics2D.CircleCast(position, Config.ContactSweepRadius, _direction, filter, _contacts, distance);
                for (int i = 0; i < count; i++)
                    if (Contact.TryImpact(_contacts[i].collider)) return;
                position += _direction * distance;
                Body.MovePosition(position);
                Rect bounds = Playfield.WorldBounds;
                if (position.x < bounds.xMin - Config.DespawnMargin || position.x > bounds.xMax + Config.DespawnMargin ||
                    position.y < bounds.yMin - Config.DespawnMargin || position.y > bounds.yMax + Config.DespawnMargin)
                { _running = false; NotifyExitedPlayfield(); return; }
            }
            _remaining -= deltaTime;
            if (_remaining > 0) return;
            switch (State)
            {
                case DownloadChargeState.Charging: Enter(DownloadChargeState.Dashing, Config.DashSeconds); break;
                case DownloadChargeState.Dashing: Enter(DownloadChargeState.Recovery, Config.RecoverySeconds); break;
                case DownloadChargeState.Recovery: Enter(DownloadChargeState.Charging, Config.ChargeSeconds); break;
            }
        }
        private void Enter(DownloadChargeState state, float seconds) { State = state; _remaining = seconds; }
        public override bool TryValidateConfiguration(out string reason)
        {
            reason = Body == null || Body.bodyType != RigidbodyType2D.Kinematic || Playfield == null ||
                Config == null || !Config.IsValid || Contact == null ? "下载冲撞缺少刚体、战区、有效参数或接触攻击。" : string.Empty;
            return reason.Length == 0;
        }
    }
}
