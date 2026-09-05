using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Movement;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Identity
{
    /// <summary>
    /// 玩家实体的轻量入口，只汇总稳定身份与明确装配的核心模块。
    /// 具体移动、战斗和状态逻辑继续由各自组件负责。
    /// </summary>
    public sealed class PlayerActor : MonoBehaviour
    {
        [SerializeField] private PlayerCharacterDefinition _definition;
        [SerializeField] private PlayerCommandDispatcher _commandDispatcher;
        [SerializeField] private PlayerMovementMotor2D _movementMotor;

        public PlayerCharacterDefinition Definition => _definition;

        public PlayerCommandDispatcher CommandDispatcher => _commandDispatcher;

        public PlayerMovementMotor2D MovementMotor => _movementMotor;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerActor)}] 玩家实体装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_definition == null)
            {
                reason = "未配置角色定义。";
                return false;
            }

            if (!_definition.TryValidate(out reason))
            {
                return false;
            }

            if (_commandDispatcher == null)
            {
                reason = "未配置玩家命令分发器。";
                return false;
            }

            if (_movementMotor == null)
            {
                reason = "未配置玩家移动组件。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
