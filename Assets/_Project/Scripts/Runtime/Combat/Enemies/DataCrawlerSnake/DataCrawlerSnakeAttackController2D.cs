using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Targeting;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    /// <summary>
    /// 数据蛇攻击状态机：就绪、蓄力、开火姿态、后摇。
    /// 只决定攻击时序；弹体生命周期由场景级对象池负责。
    /// </summary>
    public sealed class DataCrawlerSnakeAttackController2D : MonoBehaviour
    {
        [SerializeField] private DataCrawlerSnakeMotor2D _motor;
        [SerializeField] private DataCrawlerSnakeVisual2D _visual;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private DataCrawlerSnakeAttackConfig _config;

        private EnemyProjectilePool2D _projectilePool;
        private float _remainingSeconds;

        public DataCrawlerSnakeAttackState State { get; private set; }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DataCrawlerSnakeAttackController2D)}] " +
                    $"攻击装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            ResetCycle();
        }

        private void OnDisable()
        {
            ResetCycle();
        }

        private void Update()
        {
            if (_projectilePool == null || !_motor.IsRunning)
            {
                if (State != DataCrawlerSnakeAttackState.Ready)
                {
                    ResetCycle();
                }

                return;
            }

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            switch (State)
            {
                case DataCrawlerSnakeAttackState.Ready:
                    TryBeginCharge();
                    break;

                case DataCrawlerSnakeAttackState.Charging:
                    UpdateCharging(deltaTime);
                    break;

                case DataCrawlerSnakeAttackState.FirePose:
                    AdvanceTimer(
                        deltaTime,
                        DataCrawlerSnakeAttackState.Recovery,
                        _config.RecoverySeconds,
                        false);
                    break;

                case DataCrawlerSnakeAttackState.Recovery:
                    AdvanceTimer(
                        deltaTime,
                        DataCrawlerSnakeAttackState.Ready,
                        0f,
                        false);
                    break;
            }
        }

        public void BindProjectilePool(EnemyProjectilePool2D projectilePool)
        {
            _projectilePool = projectilePool;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_motor == null || _visual == null || _muzzle == null)
            {
                reason = "未配置运动、表现或炮口。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置攻击参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                reason = $"攻击参数无效：{reason}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void TryBeginCharge()
        {
            PlayerCombatTarget2D target = _motor.Target;
            if (!IsTargetWithin(target, _config.AttackRange))
            {
                return;
            }

            State = DataCrawlerSnakeAttackState.Charging;
            _remainingSeconds = _config.ChargeSeconds;
            _motor.SetMovementLocked(true);
            _visual.SetAiming(true);
        }

        private void UpdateCharging(float deltaTime)
        {
            PlayerCombatTarget2D target = _motor.Target;
            if (!IsTargetWithin(target, _config.CancelRange))
            {
                ResetCycle();
                return;
            }

            _remainingSeconds -= deltaTime;
            if (_remainingSeconds > 0f)
            {
                return;
            }

            Vector2 direction = target.Position - (Vector2)_muzzle.position;
            _projectilePool.TryRent(
                _muzzle.position,
                direction,
                gameObject,
                out _);
            State = DataCrawlerSnakeAttackState.FirePose;
            _remainingSeconds = _config.FirePoseSeconds;
        }

        private void AdvanceTimer(
            float deltaTime,
            DataCrawlerSnakeAttackState nextState,
            float nextDuration,
            bool aiming)
        {
            _remainingSeconds -= deltaTime;
            if (_remainingSeconds > 0f)
            {
                return;
            }

            State = nextState;
            _remainingSeconds = nextDuration;
            _visual.SetAiming(aiming);
            if (nextState == DataCrawlerSnakeAttackState.Recovery)
            {
                _motor.SetMovementLocked(false);
            }
        }

        private bool IsTargetWithin(
            PlayerCombatTarget2D target,
            float distance)
        {
            return target != null && target.IsTargetable &&
                   (target.Position - (Vector2)_muzzle.position).sqrMagnitude <=
                   distance * distance;
        }

        private void ResetCycle()
        {
            State = DataCrawlerSnakeAttackState.Ready;
            _remainingSeconds = 0f;
            if (_motor != null)
            {
                _motor.SetMovementLocked(false);
            }
            if (_visual != null)
            {
                _visual.SetAiming(false);
            }
        }
    }
}
