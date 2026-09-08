using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 消费一份冻结的出生随机结果，用 Rigidbody2D 横穿并翻滚。
    /// 不决定何时生成、掉落什么或如何回收。
    /// </summary>
    public sealed class EnemyTumbleMotor2D : EnemyMotor2D
    {
        [SerializeField] private Rigidbody2D _body;
        [SerializeField] private CombatPlayfieldConfig _playfield;
        [SerializeField] private EnemyTumbleMotionConfig _config;

        private EnemySpawnVariation2D _variation;
        private bool _isRunning;

        public override bool IsRunning => _isRunning;
        public override Vector2 TravelDirection =>
            _variation.TravelDirection;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyTumbleMotor2D)}] " +
                    $"敌人运动装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        public override void Simulate(float deltaTime)
        {
            if (!_isRunning || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }

            Vector2 nextPosition =
                _body.position +
                _variation.TravelDirection *
                _variation.TravelSpeed * deltaTime;
            float nextRotation =
                _body.rotation +
                _variation.AngularVelocityDegreesPerSecond * deltaTime;

            _body.MovePosition(nextPosition);
            _body.MoveRotation(nextRotation);

            if (HasExitedPlayfield(nextPosition))
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
                Debug.LogError(
                    $"[{nameof(EnemyTumbleMotor2D)}] " +
                    "收到无效的敌人出生运动快照。",
                    this);
                return;
            }

            _variation = variation;
            _body.position = spawnPosition;
            _body.rotation = variation.InitialRotationDegrees;
            _isRunning = true;
        }

        public override void Stop()
        {
            _isRunning = false;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        public override bool TryValidateConfiguration(out string reason)
        {
            if (_body == null)
            {
                reason = "未配置 Rigidbody2D。";
                return false;
            }

            if (_body.bodyType != RigidbodyType2D.Kinematic)
            {
                reason = "翻滚横穿敌人必须使用 Kinematic Rigidbody2D。";
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

            if (_config == null || !_config.TryValidate(out reason))
            {
                reason = $"翻滚运动配置无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool HasExitedPlayfield(Vector2 position)
        {
            Rect bounds = _playfield.WorldBounds;

            if (_variation.TravelDirection.x < 0f)
            {
                return position.x < bounds.xMin - _config.DespawnMargin;
            }

            return position.x > bounds.xMax + _config.DespawnMargin;
        }
    }
}
