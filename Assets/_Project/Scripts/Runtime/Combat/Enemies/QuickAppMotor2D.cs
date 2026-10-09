using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>大尺度横向正弦曲线；运动相位连续，位置映射到战区四边，不选取玩家目标。</summary>
    public sealed class QuickAppMotor2D : EnemyMotor2D
    {
        public Rigidbody2D Body;
        public BoxCollider2D Shape;
        public CombatPlayfieldConfig Playfield;
        public QuickAppMotionConfig Config;
        public EnemyContactAttack2D Contact;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private Vector2 _axis, _normal, _direction, _velocity;
        private float _speed, _amplitude, _wavelength, _phase;
        private bool _running;
        public override bool IsRunning => _running;
        public override Vector2 TravelDirection => _direction;
        public Vector2 PerceivedVelocity => _running ? _velocity : Vector2.zero;
        public uint WrapSequence { get; private set; }
        public float CurvePhase => _phase;

        public override void Begin(Vector2 position, in EnemySpawnVariation2D variation)
        {
            _axis = variation.TravelDirection; _normal = new Vector2(-_axis.y, _axis.x);
            _speed = variation.TravelSpeed; _amplitude = variation.CurveAmplitude;
            _wavelength = variation.CurveWavelength; _phase = variation.InitialRotationDegrees * Mathf.Deg2Rad;
            Rect bounds = Playfield.WorldBounds;
            Body.position = new Vector2(Mathf.Clamp(position.x, bounds.xMin, bounds.xMax), Mathf.Clamp(position.y, bounds.yMin, bounds.yMax)); Body.rotation = 0;
            transform.position = Body.position;
            _running = true; WrapSequence = 0; UpdateVelocity();
        }
        public override void Stop()
        { _running = false; _velocity = Vector2.zero; Body.linearVelocity = Vector2.zero; Body.angularVelocity = 0; }
        private void OnDisable() { if (Body != null) Stop(); }
        private void UpdateVelocity()
        {
            _velocity = _axis * _speed + _normal * (_amplitude * Mathf.Cos(_phase) * _speed * 2f * Mathf.PI / _wavelength);
            _direction = _velocity.normalized;
        }
        public override void Simulate(float deltaTime)
        {
            if (!_running || deltaTime <= 0) return;
            float speed = _speed;
            float phaseStep = speed * deltaTime * 2f * Mathf.PI / _wavelength;
            Vector2 displacement = _axis * (speed * deltaTime) +
                _normal * (_amplitude * (Mathf.Sin(_phase + phaseStep) - Mathf.Sin(_phase)));
            _phase = Mathf.Repeat(_phase + phaseStep, 2f * Mathf.PI);
            UpdateVelocity();
            Vector2 position = Body.position;
            Rect bounds = Playfield.WorldBounds;
            // 分割实际运动线段，只检查到边缘及穿出后的短段，绝不跨屏扫过所有玩家。
            bool wrapped = false;
            while (displacement.sqrMagnitude > Mathf.Epsilon)
            {
                float xFraction = displacement.x > 0 ? (bounds.xMax - position.x) / displacement.x :
                    displacement.x < 0 ? (bounds.xMin - position.x) / displacement.x : float.PositiveInfinity;
                float yFraction = displacement.y > 0 ? (bounds.yMax - position.y) / displacement.y :
                    displacement.y < 0 ? (bounds.yMin - position.y) / displacement.y : float.PositiveInfinity;
                float fraction = Mathf.Min(1f, Mathf.Min(xFraction, yFraction));
                fraction = Mathf.Clamp01(fraction);
                Vector2 segment = displacement * fraction;
                Sweep(position, segment); position += segment;
                if (fraction >= 1f) break;
                if (xFraction <= fraction) position.x = displacement.x > 0 ? bounds.xMin : bounds.xMax;
                if (yFraction <= fraction) position.y = displacement.y > 0 ? bounds.yMin : bounds.yMax;
                displacement *= 1f - fraction; wrapped = true;
            }
            if (wrapped)
            {
                WrapSequence++; Body.position = position; transform.position = position;
                Body.linearVelocity = Vector2.zero;
            }
            else Body.MovePosition(position);
        }
        private void Sweep(Vector2 position, Vector2 segment)
        {
            if (segment.sqrMagnitude <= Mathf.Epsilon) return;
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(Config.PlayerLayers);
            int count = Physics2D.BoxCast(position + Shape.offset, Shape.size, 0f,
                segment.normalized, filter, _hits, segment.magnitude);
            for (int i = 0; i < count; i++) if (Contact.TryImpact(_hits[i].collider, position + Shape.offset)) break;
        }
        public static Vector2 Wrap(Vector2 position, Rect bounds) => new Vector2(
            bounds.xMin + Mathf.Repeat(position.x - bounds.xMin, bounds.width),
            bounds.yMin + Mathf.Repeat(position.y - bounds.yMin, bounds.height));
        public override bool TryValidateConfiguration(out string reason)
        {
            reason = Body == null || Body.bodyType != RigidbodyType2D.Kinematic || Shape == null ||
                Playfield == null || Config == null || Contact == null ? "快应用缺少刚体、主体Box、战区、运动配置或接触攻击。" : string.Empty;
            return reason.Length == 0 && Config.TryValidate(out reason) && Playfield.TryValidate(out reason);
        }
    }
}
