using DeepSleep.Runtime.Input.Commands;
using UnityEngine.InputSystem;

namespace DeepSleep.Runtime.Input.Local
{
    /// <summary>
    /// 缓存两个模拟刻之间发生的按钮边沿，避免快速点击被固定更新漏掉。
    /// </summary>
    internal sealed class InputActionButtonBuffer
    {
        private readonly InputAction action;

        private bool isEnabled;
        private bool isHeld;
        private bool pressedSinceLastRead;
        private bool releasedSinceLastRead;

        public InputActionButtonBuffer(InputAction action)
        {
            this.action = action;
        }

        public void Enable()
        {
            if (isEnabled)
            {
                return;
            }

            ResetState();
            action.performed += OnPerformed;
            action.canceled += OnCanceled;
            action.Enable();
            isHeld = action.IsPressed();
            isEnabled = true;
        }

        public void Disable()
        {
            if (!isEnabled)
            {
                return;
            }

            action.performed -= OnPerformed;
            action.canceled -= OnCanceled;
            action.Disable();
            isEnabled = false;
            ResetState();
        }

        public CommandButtonState ConsumeState()
        {
            CommandButtonState state = CommandButtonState.None;

            if (pressedSinceLastRead)
            {
                state |= CommandButtonState.Pressed;
            }

            if (isHeld)
            {
                state |= CommandButtonState.Held;
            }

            if (releasedSinceLastRead)
            {
                state |= CommandButtonState.Released;
            }

            pressedSinceLastRead = false;
            releasedSinceLastRead = false;
            return state;
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            if (isHeld)
            {
                return;
            }

            isHeld = true;
            pressedSinceLastRead = true;
        }

        private void OnCanceled(InputAction.CallbackContext context)
        {
            if (!isHeld)
            {
                return;
            }

            isHeld = false;
            releasedSinceLastRead = true;
        }

        private void ResetState()
        {
            isHeld = false;
            pressedSinceLastRead = false;
            releasedSinceLastRead = false;
        }
    }
}
