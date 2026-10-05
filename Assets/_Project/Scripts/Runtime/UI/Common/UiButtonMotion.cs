using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>显式绑定同根 Selectable、视觉子根与两张生图高光层；不改热区或点击事件。</summary>
    public sealed class UiButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public Selectable Control;
        public CanvasGroup WholeControlGroup;
        public RectTransform Visual;
        public Image Highlight, Glow;
        [Range(.8f, 1)] public float PressScale = .97f;
        [Range(1, 1.1f)] public float ReboundScale = 1.015f;
        [Min(.01f)] public float ResponseDuration = .09f, ReboundDuration = .10f;
        [Range(0, 1)] public float HoverAlpha = .40f, PressedAlpha = .65f, SelectedAlpha = .20f;
        [Range(0, 1)] public float BreatheAlpha = .08f;
        [Min(.1f)] public float BreathePeriod = 2.8f;
        [Range(0, 1)] public float GlowStrength = .55f;
        [Range(0, 1)] public float DisabledAlpha = .45f;

        private CanvasRenderer _highlightRenderer, _glowRenderer;
        private Vector3 _baseScale;
        private bool _ready, _interactive, _hover, _selected, _held, _inside, _focused = true, _paused;
        private int _pointerId;
        private float _scale = 1, _from = 1, _target = 1, _elapsed, _duration;
        private float _submitRemaining, _highlight, _glow, _breatheTime;
        private float _originalAlpha;
        private bool _rebound;

        private void OnEnable()
        {
            if (!_ready)
            {
                if (Control == null || Control.gameObject != gameObject || WholeControlGroup == null ||
                    WholeControlGroup.gameObject != gameObject || Visual == null ||
                    Visual == transform || !Visual.IsChildOf(transform) || Highlight == null || Glow == null)
                {
                    string path = name;
                    for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                        path = parent.name + "/" + path;
                    Debug.LogError($"[UiButtonMotion] {path}: 需要同根 Control / WholeControlGroup、独立 Visual 子根及 Highlight / Glow。", this);
                    enabled = false;
                    return;
                }
                _baseScale = Visual.localScale;
                _highlightRenderer = Highlight.canvasRenderer;
                _glowRenderer = Glow.canvasRenderer;
                _ready = true;
            }
            _focused = true;
            _paused = false;
            _originalAlpha = WholeControlGroup.alpha;
            ResetVisual();
            _interactive = Control.IsActive() && Control.IsInteractable();
            WholeControlGroup.alpha = _originalAlpha * (_interactive ? 1 : DisabledAlpha);
        }

        private void OnDisable()
        {
            ResetVisual();
            if (_ready) WholeControlGroup.alpha = _originalAlpha;
        }
        private void OnApplicationFocus(bool focused) { _focused = focused; if (!focused) ResetVisual(); }
        private void OnApplicationPause(bool paused) { _paused = paused; if (paused) ResetVisual(); }

        private void Update()
        {
            bool interactive = Control.IsActive() && Control.IsInteractable();
            if (_interactive != interactive)
            {
                ResetVisual();
                _interactive = interactive;
                WholeControlGroup.alpha = _originalAlpha * (interactive ? 1 : DisabledAlpha);
            }
            if (!interactive || !_focused || _paused) return;
            float dt = Time.unscaledDeltaTime;
            if (_scale != _target || _rebound)
            {
                _elapsed += dt;
                float t = Mathf.Clamp01(_elapsed / _duration);
                _scale = Mathf.Lerp(_from, _target, t * t * (3 - 2 * t));
                Visual.localScale = _baseScale * _scale;
                if (t >= 1 && _rebound) { _rebound = false; TweenScale(1, ReboundDuration); }
            }
            bool pressed = (_held && _inside) || _submitRemaining > 0;
            float targetHighlight = pressed ? PressedAlpha : _hover ? HoverAlpha : _selected ? SelectedAlpha : 0;
            float targetGlow = targetHighlight * GlowStrength;
            if (_selected && !pressed)
            {
                _breatheTime = Mathf.Repeat(_breatheTime + dt, BreathePeriod);
                targetGlow += BreatheAlpha * (.5f - .5f * Mathf.Cos(_breatheTime / BreathePeriod * Mathf.PI * 2));
            }
            float step = dt / ResponseDuration;
            float highlight = Mathf.MoveTowards(_highlight, targetHighlight, step);
            float glow = Mathf.MoveTowards(_glow, Mathf.Clamp01(targetGlow), step);
            if (highlight != _highlight) { _highlight = highlight; _highlightRenderer.SetAlpha(highlight); }
            if (glow != _glow) { _glow = glow; _glowRenderer.SetAlpha(glow); }
            // 先渲染本帧按压，再排下一帧回弹；低帧率下也不能跳过 PressScale。
            if (_submitRemaining > 0)
            {
                _submitRemaining -= dt;
                if (_submitRemaining <= 0) Release(true);
            }
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!CanRespond() || (_held && e.pointerId != _pointerId)) return;
            _hover = !IsTouch(e);
            if (_held) { _inside = true; TweenScale(PressScale, ResponseDuration); }
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (!_ready || (_held && e.pointerId != _pointerId)) return;
            _hover = false;
            if (_held) { _inside = false; TweenScale(1, ResponseDuration); }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!CanRespond() || _held || e.button != PointerEventData.InputButton.Left) return;
            _pointerId = e.pointerId;
            _held = _inside = true;
            _submitRemaining = 0;
            if (IsTouch(e)) _hover = _selected = false;
            TweenScale(PressScale, ResponseDuration);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_ready || !_held || e.pointerId != _pointerId || e.button != PointerEventData.InputButton.Left) return;
            bool rebound = _inside && CanRespond();
            _held = false;
            if (IsTouch(e)) _hover = _selected = false;
            Release(rebound);
        }

        public void OnSelect(BaseEventData e)
        {
            if (!CanRespond()) return;
            // 触屏也会触发 uGUI 的 Select；不能把一次触摸误留成永久悬停/呼吸。
            _selected = !(e is PointerEventData pointer) || !IsTouch(pointer);
            _breatheTime = 0;
        }

        public void OnDeselect(BaseEventData e) { _selected = false; }

        public void OnSubmit(BaseEventData e)
        {
            if (!CanRespond() || _held) return;
            _selected = true;
            _submitRemaining = ResponseDuration;
            TweenScale(PressScale, ResponseDuration);
        }

        private bool CanRespond() => _ready && isActiveAndEnabled && _focused && !_paused &&
            Control.IsActive() && Control.IsInteractable();

        private static bool IsTouch(PointerEventData e) => e is ExtendedPointerEventData extended
            ? extended.pointerType == UIPointerType.Touch : e.pointerId >= 0;

        private void Release(bool rebound)
        {
            TweenScale(rebound ? ReboundScale : 1, ReboundDuration);
            _rebound = rebound;
        }

        private void TweenScale(float target, float duration)
        {
            _from = _scale; _target = target; _elapsed = 0; _duration = duration; _rebound = false;
        }

        private void ResetVisual()
        {
            _held = _inside = _hover = _selected = _rebound = false;
            _submitRemaining = _breatheTime = _highlight = _glow = 0;
            _scale = _from = _target = 1;
            if (!_ready) return;
            Visual.localScale = _baseScale;
            _highlightRenderer.SetAlpha(0);
            _glowRenderer.SetAlpha(0);
        }
    }
}
