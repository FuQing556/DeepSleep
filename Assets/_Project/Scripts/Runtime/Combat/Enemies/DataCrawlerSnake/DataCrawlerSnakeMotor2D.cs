using DeepSleep.Runtime.Combat.Targeting;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    /// <summary>
    /// 按固定间隔从已登记玩家中选择最近目标并接近。
    /// 没有目标时沿出生方向继续移动，避免停在场外。
    /// </summary>
    public sealed class DataCrawlerSnakeMotor2D : EnemyMotor2D
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private CombatPlayfieldConfig _playfield;
        [SerializeField] private DataCrawlerSnakeMotionConfig _config;

        private PlayerCombatTarget2D _target;
        private Vector2 _fallbackDirection;
        private Vector2 _travelDirection;
        private float _speed;
        private float _retargetRemainingSeconds;
        private bool _hasEnteredPlayfield;
        private bool _isRunning;
        private bool _isMovementLocked;

        public override bool IsRunning => _isRunning;
        public override Vector2 TravelDirection => _travelDirection;
        public PlayerCombatTarget2D Target => _target;
        public bool IsMovementLocked => _isMovementLocked;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DataCrawlerSnakeMotor2D)}] " +
                    $"追踪运动装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        public override void PrepareSimulation(float deltaTime)
        {
            if (!_isRunning || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }

            _retargetRemainingSeconds -= deltaTime;
            if (_retargetRemainingSeconds <= 0f ||
                _target == null || !_target.IsTargetable)
            {
                PlayerCombatTarget2D.TryFindNearest(
                    _body.position,
                    out _target);
                _retargetRemainingSeconds = _config.RetargetInterval;
            }

            if (_target == null || !_target.IsTargetable)
            {
                _travelDirection = _fallbackDirection;
            }
            else
            {
                Vector2 offset = _target.Position - _body.position;
                if (offset.sqrMagnitude > 0.0001f)
                    _travelDirection = offset.normalized;
            }
        }

        public override void Simulate(float deltaTime)
        {
            if (!_isRunning || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }
            Vector2 movement = CalculateMovement();
            Vector2 nextPosition =
                _body.position + movement * deltaTime;
            _body.MovePosition(nextPosition);

            Rect bounds = _playfield.WorldBounds;
            _hasEnteredPlayfield |= bounds.Contains(nextPosition);

            if (_hasEnteredPlayfield && HasExited(bounds, nextPosition))
            {
                _isRunning = false;
                NotifyExitedPlayfield();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public override void Begin(
            Vector2 spawnPosition,
            in EnemySpawnVariation2D variation)
        {
            if (!variation.IsValid)
            {
                return;
            }

            _fallbackDirection = variation.TravelDirection;
            _travelDirection = _fallbackDirection;
            _speed = variation.TravelSpeed;
            _target = null;
            _retargetRemainingSeconds = 0f;
            _hasEnteredPlayfield = false;
            _isMovementLocked = false;
            _body.position = spawnPosition;
            _body.rotation = 0f;
            _isRunning = true;
        }

        public override void Stop()
        {
            _isRunning = false;
            _isMovementLocked = false;
            _target = null;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        public void SetMovementLocked(bool isLocked)
        {
            _isMovementLocked = isLocked;
        }

        public override bool TryValidateConfiguration(out string reason)
        {
            if (_body == null || _body.bodyType != RigidbodyType2D.Kinematic)
            {
                reason = "必须配置 Kinematic Rigidbody2D。";
                return false;
            }

            if (_playfield == null)
            {
                reason = "未配置逻辑战斗区域。";
                return false;
            }

            if (!_playfield.TryValidate(out reason))
            {
                reason = $"逻辑战斗区域无效：{reason}";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置追踪运动参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                reason = $"追踪运动配置无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private Vector2 CalculateMovement()
        {
            if (_target == null || !_target.IsTargetable)
            {
                _travelDirection = _fallbackDirection;
                return _travelDirection * _speed;
            }

            Vector2 offset = _target.Position - _body.position;
            if (offset.sqrMagnitude > 0.0001f)
            {
                _travelDirection = offset.normalized;
            }

            if (_isMovementLocked)
            {
                return Vector2.zero;
            }

            float stoppingDistanceSqr =
                _config.StoppingDistance * _config.StoppingDistance;
            return offset.sqrMagnitude > stoppingDistanceSqr
                ? _travelDirection * _speed
                : Vector2.zero;
        }

        private bool HasExited(Rect bounds, Vector2 position)
        {
            float margin = _config.DespawnMargin;
            return position.x < bounds.xMin - margin ||
                   position.x > bounds.xMax + margin ||
                   position.y < bounds.yMin - margin ||
                   position.y > bounds.yMax + margin;
        }
    }
}
