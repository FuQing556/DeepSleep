using System;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>命令决定意图；固定模拟刻推进近战时钟、轨迹与伤害。表现不负责扣血。</summary>
    public sealed class HarnessMeleeController : MonoBehaviour, IPlayerActionCommandConsumer,
        IFixedSimulationStep, IPlayerReviveStartBlocker
    {
        [SerializeField] private HarnessMeleeConfig _config;
        [SerializeField] private HarnessTerminalLaserController _laser;
        [SerializeField] private PlayerFacingController2D _facing;
        [SerializeField] private HarnessMeleeDamageExecutor2D _damage;
        [SerializeField, Tooltip("与激光 LaserOrigin 共用的轨迹基准，剑柄在其周围运动；不挂在角色图片下面。")]
        private Transform _swordGrip;
        private float _modeRemaining, _cooldown, _elapsed, _recovery;
        private int _nextAttack;
        private double _comboIdleSeconds;
        private bool _held, _commandReceived;
        private bool _exitRequested;
        private AimIntent _aim;
        private Vector2 _previousOrigin;
        public bool IsMelee { get; private set; }
        public bool IsSwinging { get; private set; }
        public bool BlocksReviveStart => IsSwinging;
        public PlayerActionBlock ActionCategory => PlayerActionBlock.ActiveCombat;
        public HarnessMeleeAttackConfig Attack { get; private set; }
        public float AimDegrees { get; private set; }
        public float Progress => IsSwinging ? Mathf.Clamp01(_elapsed / Attack.SwingSeconds) : 0f;
        public bool ExitRequested => _exitRequested;
        public Vector2 GripPosition => _swordGrip != null ? (Vector2)_swordGrip.position : (Vector2)transform.position;
        public float ModeRemaining => _modeRemaining;
        public float CooldownRemaining => _cooldown;
        public HarnessMeleeConfig Config => _config;
        public event Action<bool> ModeChanged;
        public event Action SwingStarted;
        public event Action<HarnessMeleeAttackConfig, Vector2, float> WaveRequested;
        public event Action SwingFinished;

        private void Awake()
        {
            if (_config == null || !_config.IsValid || _laser == null || _facing == null ||
                _damage == null || !_damage.IsConfigured || _swordGrip == null)
            {
                Debug.LogError("[HarnessMelee] 近战配置不完整，已停止运行。", this);
                enabled = false;
            }
        }

        public void ConsumeCommand(in PlayerCommand command, float deltaTime)
        {
            _commandReceived = true;
            _aim = command.Aim;
            _held = (command.ConfirmAim & (CommandButtonState.Held | CommandButtonState.Pressed)) != 0;
            if ((command.PrimarySkill & CommandButtonState.Pressed) != 0 && IsMelee)
            {
                _exitRequested = true;
                _held = false;
                if (!IsSwinging) EndMode();
                return;
            }
            if ((command.PrimarySkill & CommandButtonState.Pressed) != 0 && !IsMelee && _cooldown <= 0f)
            {
                IsMelee = true;
                _exitRequested = false;
                _modeRemaining = _config.DurationSeconds;
                _nextAttack = 0;
                ModeChanged?.Invoke(true);
                _laser.SetInputSuppressed(true);
                // 入场第一刀跟当前朝向，不要求额外点敌人。
                StartSwing(_facing.Forward);
            }
        }

        public void Simulate(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            _cooldown = Mathf.Max(0, _cooldown - dt);
            if (IsMelee)
            {
                _modeRemaining = Mathf.Max(0, _modeRemaining - dt);
                if (IsSwinging) AdvanceSwing(dt);
                else if (_modeRemaining <= 0 || _exitRequested) EndMode();
                else
                {
                    // 只累计收刀后的模拟时间，松键或暂停不会提前消耗接招窗口。
                    _comboIdleSeconds += dt;
                    if (_comboIdleSeconds > _config.ComboResetSeconds) _nextAttack = 0;
                    _recovery = Mathf.Max(0, _recovery - dt);
                    if (_held && _commandReceived && _recovery <= 0) StartSwing(ReadAim());
                }
            }
            // 失去命令源、被行动门禁阻拦时不能沿用旧的按住命令继续攻击。
            _commandReceived = false;
            _held = false;
        }

        private Vector2 ReadAim()
        {
            Vector2 direction = _aim.Reference switch
            {
                AimReference.WorldPosition => _aim.Value - (Vector2)transform.position,
                AimReference.Direction => _aim.Value,
                _ => _facing.Forward
            };
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : _facing.Forward;
        }

        private void StartSwing(Vector2 direction)
        {
            Attack = _config.Attacks[_nextAttack];
            _nextAttack = (_nextAttack + 1) % _config.Attacks.Length;
            AimDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _elapsed = 0;
            _comboIdleSeconds = 0;
            IsSwinging = true;
            _damage.BeginSwing();
            SwingStarted?.Invoke();
            // 先截取旧动作和旧朝向，再让新姿态转身。
            if (Mathf.Abs(direction.x) > 0.001f)
                _facing.SetDirection(direction.x < 0 ? FacingDirection.Left : FacingDirection.Right);
            _previousOrigin = GripPosition;
        }

        private void AdvanceSwing(float dt)
        {
            float previous = Progress;
            _elapsed = Mathf.Min(Attack.SwingSeconds, _elapsed + dt);
            Vector2 origin = GripPosition;
            _damage.Sweep(Attack, _previousOrigin, origin, AimDegrees, previous, Progress);
            _previousOrigin = origin;
            if (_elapsed < Attack.SwingSeconds) return;
            IsSwinging = false;
            _comboIdleSeconds = 0;
            _recovery = Attack.RecoverySeconds;
            if (_modeRemaining > 0 && !_exitRequested) SwingFinished?.Invoke();
            // 最后一刀即使跨过技能到期也完整结算，之后不再起新刀。
            _damage.Burst(Attack, origin, AimDegrees);
            WaveRequested?.Invoke(Attack, origin, AimDegrees);
            if (_modeRemaining <= 0 || _exitRequested) EndMode();
        }

        private void EndMode()
        {
            if (!IsMelee) return;
            IsMelee = IsSwinging = false;
            _exitRequested = false;
            _nextAttack = 0;
            _comboIdleSeconds = 0;
            _cooldown = _config.CooldownSeconds;
            _laser.SetInputSuppressed(false);
            ModeChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            _held = _commandReceived = false;
            EndMode();
        }
    }
}
