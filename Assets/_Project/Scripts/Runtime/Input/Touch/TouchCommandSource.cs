using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Input.Touch
{
    /// <summary>桌面输入透传，移动设备使用屏幕摇杆/技能键/场景点击。两者共用 PlayerCommand。</summary>
    public sealed class TouchCommandSource : MonoBehaviour, ICommandSource
    {
        public MonoBehaviour DesktopInput;
        public GameObject TouchCanvas;
        public Camera AimCamera;
        public PlayerControlAssignment Assignment;
        public Transform DeepSeek, Harness;
        public bool ForceTouchForTesting;
        private ICommandSource _desktop;
        private uint _sequence;
        private Vector2 _move;
        private AimIntent _aim;
        private CommandButtonState _skill, _secondary, _attack, _cancel;
        public bool TouchEnabled => ForceTouchForTesting || Application.isMobilePlatform;
        private void Awake()
        {
            _desktop = DesktopInput as ICommandSource;
            if (_desktop == null || TouchCanvas == null || AimCamera == null)
            { Debug.LogError("[TouchCommandSource] 配置不完整", this); enabled = false; return; }
            TouchCanvas.SetActive(TouchEnabled);
        }
        public void SetMove(Vector2 value) => _move = Vector2.ClampMagnitude(value, 1);
        public void SetAimScreen(Vector2 screen)
        {
            Vector3 world = AimCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -AimCamera.transform.position.z));
            _aim = new AimIntent(AimReference.WorldPosition, world);
        }
        public void SetAttack(bool down) => SetButton(ref _attack, down);
        public void SetSkill(bool down) => SetButton(ref _skill, down);
        public void SetSecondary(bool down) => SetButton(ref _secondary, down);
        public void SetCancel(bool down) => SetButton(ref _cancel, down);
        private static void SetButton(ref CommandButtonState state, bool down)
        { state = down ? state | CommandButtonState.Pressed | CommandButtonState.Held :
                (state & ~CommandButtonState.Held) | CommandButtonState.Released; }
        private void OnApplicationFocus(bool focus) { if (!focus) Clear(); }
        private void Clear() { _move = Vector2.zero; _skill = _secondary = _attack = _cancel = 0; }
        public bool TryGetCommand(uint tick, out PlayerCommand command)
        {
            if (!TouchEnabled) return _desktop.TryGetCommand(tick, out command);
            command = new PlayerCommand(++_sequence, tick, _move, _aim, _skill, _secondary, _attack, _cancel, 0, 0);
            _skill &= CommandButtonState.Held; _secondary &= CommandButtonState.Held;
            _attack &= CommandButtonState.Held; _cancel &= CommandButtonState.Held;
            return true;
        }
    }
}
