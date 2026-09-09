using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Actions;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Movement
{
    /// <summary>
    /// 根据二维移动意图更新玩家刚体；不读取设备输入，也不管理角色状态。
    /// </summary>
    public sealed class PlayerMovementMotor2D :
        MonoBehaviour,
        IPlayerActionCommandConsumer
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private PlayerMotorConfig config;

        private bool isInitialized;

        public PlayerActionBlock ActionCategory =>
            PlayerActionBlock.Movement;

        private void Awake()
        {
            if (!TryValidateConfiguration())
            {
                enabled = false;
                return;
            }

            isInitialized = true;
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public void Simulate(Vector2 moveIntent, float deltaTime)
        {
            if (!isInitialized || !isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }

            Vector2 position = body.position;
            Vector2 velocity = body.linearVelocity;
            Bounds bounds = bodyCollider.bounds;
            PlayerMovementStep.Calculate(ref position, ref velocity, moveIntent, config,
                bounds.extents, (Vector2)bounds.center - position, deltaTime);
            if (position != body.position) body.position = position;
            body.linearVelocity = velocity;
        }

        public void ConsumeCommand(in PlayerCommand command, float deltaTime)
        {
            Simulate(command.Move, deltaTime);
        }

        private bool TryValidateConfiguration()
        {
            bool isValid = true;

            if (body == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerMovementMotor2D)}] 未配置玩家 Rigidbody2D。",
                    this);
                isValid = false;
            }

            if (bodyCollider == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerMovementMotor2D)}] 未配置玩家 Collider2D。",
                    this);
                isValid = false;
            }

            if (config == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerMovementMotor2D)}] 未配置移动参数资产。",
                    this);
                return false;
            }

            if (!config.TryValidate(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerMovementMotor2D)}] 移动参数无效：{reason}",
                    config);
                isValid = false;
            }

            return isValid;
        }
    }
}
