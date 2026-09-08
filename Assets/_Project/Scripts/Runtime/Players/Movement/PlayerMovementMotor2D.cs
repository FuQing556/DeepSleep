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

            Vector2 normalizedIntent = Vector2.ClampMagnitude(moveIntent, 1f);
            Vector2 targetVelocity = normalizedIntent * config.MaximumSpeed;
            float changeRate = normalizedIntent.sqrMagnitude > 0f
                ? config.Acceleration
                : config.Deceleration;

            Vector2 velocity = Vector2.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                changeRate * deltaTime);

            KeepInsideMovementBounds(ref velocity, deltaTime);
            body.linearVelocity = velocity;
        }

        public void ConsumeCommand(in PlayerCommand command, float deltaTime)
        {
            Simulate(command.Move, deltaTime);
        }

        private void KeepInsideMovementBounds(ref Vector2 velocity, float deltaTime)
        {
            Bounds colliderBounds = bodyCollider.bounds;
            Vector2 bodyPosition = body.position;
            Vector2 colliderCenterOffset =
                (Vector2)colliderBounds.center - bodyPosition;
            Vector2 colliderExtents = colliderBounds.extents;

            Rect allowedArea = config.MovementBounds;
            Vector2 minimumBodyPosition =
                allowedArea.min + colliderExtents - colliderCenterOffset;
            Vector2 maximumBodyPosition =
                allowedArea.max - colliderExtents - colliderCenterOffset;

            Vector2 correctedPosition = new Vector2(
                Mathf.Clamp(
                    bodyPosition.x,
                    minimumBodyPosition.x,
                    maximumBodyPosition.x),
                Mathf.Clamp(
                    bodyPosition.y,
                    minimumBodyPosition.y,
                    maximumBodyPosition.y));

            if (correctedPosition != bodyPosition)
            {
                body.position = correctedPosition;
                bodyPosition = correctedPosition;
            }

            Vector2 predictedPosition = bodyPosition + velocity * deltaTime;

            if (predictedPosition.x < minimumBodyPosition.x)
            {
                velocity.x =
                    (minimumBodyPosition.x - bodyPosition.x) / deltaTime;
            }
            else if (predictedPosition.x > maximumBodyPosition.x)
            {
                velocity.x =
                    (maximumBodyPosition.x - bodyPosition.x) / deltaTime;
            }

            if (predictedPosition.y < minimumBodyPosition.y)
            {
                velocity.y =
                    (minimumBodyPosition.y - bodyPosition.y) / deltaTime;
            }
            else if (predictedPosition.y > maximumBodyPosition.y)
            {
                velocity.y =
                    (maximumBodyPosition.y - bodyPosition.y) / deltaTime;
            }
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
