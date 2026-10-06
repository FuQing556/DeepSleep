using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Presentation.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>显式按钮反馈；业务成功/失败另行播放时关闭 PlayClick，避免同次操作叠音。</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class UiAudioFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        public Selectable Control;
        public bool PlayClick = true;
        public AudioCue ClickCue = AudioCue.UiConfirm;
        [Range(0f, 1f)] public float Gain = 1f;
        [Min(.02f)] public float FocusInterval = .08f;

        private static float _nextFocusTime = float.NegativeInfinity;
        private Button _button;
        private int _sourceId;
        private bool _hover, _selected, _focused = true, _paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetThrottle() => _nextFocusTime = float.NegativeInfinity;

        private void OnEnable()
        {
            if (Control == null || Control.gameObject != gameObject)
            {
                Debug.LogError("[UiAudioFeedback] 需要显式绑定同物体的 Selectable。", this);
                enabled = false;
                return;
            }
            _hover = _selected = _paused = false;
            _focused = Application.isFocused;
            _button = Control as Button;
            _sourceId = GetEntityId().GetHashCode();
            if (_button != null) _button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
            _button = null;
            _hover = _selected = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) _hover = _selected = false;
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused) _hover = _selected = false;
        }

        private bool CanRespond() => isActiveAndEnabled && _focused && !_paused &&
            Control != null && Control.IsActive() && Control.IsInteractable();

        private void OnClick()
        {
            // onClick 已排除按住、拖出与取消；不要再订阅 PointerDown/Up 或 Submit 重播。
            // Button.Press 在派发前已检查可用性。前一个业务监听可能立即关页，不能事后再查显隐而漏音。
            if (PlayClick && _focused && !_paused)
                GameAppRoot.Instance?.Audio?.Play(ClickCue, sourceId: _sourceId, gain: Gain);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanRespond() || IsTouch(eventData)) return;
            bool alreadyFocused = _hover || _selected;
            _hover = true;
            if (!alreadyFocused) PlayFocus();
        }

        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        public void OnSelect(BaseEventData eventData)
        {
            // 只接受真实导航轴事件。初始化 SetSelectedGameObject 和触屏选中都静音。
            if (!CanRespond() || !(eventData is AxisEventData axis) || axis.moveDir == MoveDirection.None)
                return;
            bool alreadyFocused = _hover || _selected;
            _selected = true;
            if (!alreadyFocused) PlayFocus();
        }

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        private void PlayFocus()
        {
            float now = Time.unscaledTime;
            if (now < _nextFocusTime) return;
            if (GameAppRoot.Instance?.Audio?.Play(AudioCue.UiFocus, sourceId: _sourceId, gain: Gain) == true)
                _nextFocusTime = now + Mathf.Max(.02f, FocusInterval);
        }

        private static bool IsTouch(PointerEventData eventData) => eventData is ExtendedPointerEventData extended
            ? extended.pointerType == UIPointerType.Touch : eventData.pointerId >= 0;
    }
}
