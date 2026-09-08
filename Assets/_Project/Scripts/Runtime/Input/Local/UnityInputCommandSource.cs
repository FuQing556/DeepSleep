using DeepSleep.Runtime.Input.Commands;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepSleep.Runtime.Input.Local
{
    /// <summary>
    /// 将 Unity Input System 的本地输入转换为与设备无关的玩家命令。
    /// 本组件不移动角色，也不决定任何技能效果。
    /// </summary>
    public sealed class UnityInputCommandSource : MonoBehaviour, ICommandSource
    {
        [Header("连续输入")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference aimAction;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float aimWorldPlaneZ;

        [Header("按钮输入")]
        [SerializeField] private InputActionReference primarySkillAction;
        [SerializeField] private InputActionReference secondarySkillAction;
        [SerializeField] private InputActionReference confirmAimAction;
        [SerializeField] private InputActionReference cancelAimAction;
        [SerializeField] private InputActionReference reconnectAction;
        [SerializeField] private InputActionReference pauseAction;

        private InputActionButtonBuffer primarySkillBuffer;
        private InputActionButtonBuffer secondarySkillBuffer;
        private InputActionButtonBuffer confirmAimBuffer;
        private InputActionButtonBuffer cancelAimBuffer;
        private InputActionButtonBuffer reconnectBuffer;
        private InputActionButtonBuffer pauseBuffer;

        private uint sequence;
        private bool isInitialized;

        private void Awake()
        {
            if (!HasAllActionReferences())
            {
                enabled = false;
                return;
            }

            primarySkillBuffer = new InputActionButtonBuffer(primarySkillAction.action);
            secondarySkillBuffer = new InputActionButtonBuffer(secondarySkillAction.action);
            confirmAimBuffer = new InputActionButtonBuffer(confirmAimAction.action);
            cancelAimBuffer = new InputActionButtonBuffer(cancelAimAction.action);
            reconnectBuffer = new InputActionButtonBuffer(reconnectAction.action);
            pauseBuffer = new InputActionButtonBuffer(pauseAction.action);
            isInitialized = true;
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                return;
            }

            moveAction.action.Enable();
            aimAction.action.Enable();
            primarySkillBuffer.Enable();
            secondarySkillBuffer.Enable();
            confirmAimBuffer.Enable();
            cancelAimBuffer.Enable();
            reconnectBuffer.Enable();
            pauseBuffer.Enable();
        }

        private void OnDisable()
        {
            if (!isInitialized)
            {
                return;
            }

            moveAction.action.Disable();
            aimAction.action.Disable();
            primarySkillBuffer.Disable();
            secondarySkillBuffer.Disable();
            confirmAimBuffer.Disable();
            cancelAimBuffer.Disable();
            reconnectBuffer.Disable();
            pauseBuffer.Disable();
        }

        public bool TryGetCommand(uint simulationTick, out PlayerCommand command)
        {
            if (!isInitialized || !isActiveAndEnabled)
            {
                command = default;
                return false;
            }

            unchecked
            {
                sequence++;
            }

            command = new PlayerCommand(
                sequence,
                simulationTick,
                moveAction.action.ReadValue<Vector2>(),
                ReadAimIntent(),
                primarySkillBuffer.ConsumeState(),
                secondarySkillBuffer.ConsumeState(),
                confirmAimBuffer.ConsumeState(),
                cancelAimBuffer.ConsumeState(),
                reconnectBuffer.ConsumeState(),
                pauseBuffer.ConsumeState());

            return true;
        }

        private AimIntent ReadAimIntent()
        {
            if (aimCamera == null)
            {
                return new AimIntent(AimReference.None, Vector2.zero);
            }

            Vector2 screenPosition = aimAction.action.ReadValue<Vector2>();
            Ray aimRay = aimCamera.ScreenPointToRay(screenPosition);
            Plane gameplayPlane = new Plane(
                Vector3.forward,
                new Vector3(0f, 0f, aimWorldPlaneZ));

            if (!gameplayPlane.Raycast(aimRay, out float enter))
            {
                return new AimIntent(AimReference.None, Vector2.zero);
            }

            return new AimIntent(
                AimReference.WorldPosition,
                aimRay.GetPoint(enter));
        }

        private bool HasAllActionReferences()
        {
            bool isValid = true;
            isValid &= HasAction(moveAction, nameof(moveAction));
            isValid &= HasAction(aimAction, nameof(aimAction));
            isValid &= HasAimCamera();
            isValid &= HasAction(primarySkillAction, nameof(primarySkillAction));
            isValid &= HasAction(secondarySkillAction, nameof(secondarySkillAction));
            isValid &= HasAction(confirmAimAction, nameof(confirmAimAction));
            isValid &= HasAction(cancelAimAction, nameof(cancelAimAction));
            isValid &= HasAction(reconnectAction, nameof(reconnectAction));
            isValid &= HasAction(pauseAction, nameof(pauseAction));
            return isValid;
        }

        private bool HasAimCamera()
        {
            if (aimCamera != null)
            {
                return true;
            }

            Debug.LogError(
                $"[{nameof(UnityInputCommandSource)}] 未配置瞄准摄像机。",
                this);
            return false;
        }

        private bool HasAction(InputActionReference actionReference, string fieldName)
        {
            if (actionReference != null && actionReference.action != null)
            {
                return true;
            }

            Debug.LogError(
                $"[{nameof(UnityInputCommandSource)}] 输入引用 {fieldName} 未配置。",
                this);
            return false;
        }
    }
}
