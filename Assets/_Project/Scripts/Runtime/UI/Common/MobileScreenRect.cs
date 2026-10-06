using DeepSleep.Runtime.Input.Touch;
using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>手机控件以整屏定位、屏高定尺寸；安全区只负责避让，不叠加第二份边距。</summary>
    public sealed class MobileScreenRect : MonoBehaviour
    {
        public TouchCommandSource Input;
        public Vector2 ScreenPosition;
        public Vector2 SizeInScreenHeights;
        public bool ScaleAuthoredContent;
        private RectTransform _rect, _parent;
        private Canvas _canvas;
        private Vector2 _anchorsMin, _anchorsMax, _position, _size;
        private Vector3 _scale;
        private bool _mobileApplied;
        private Vector2Int _screen;
        private Rect _safe;
        private Vector2 _parentSize;
        private float _canvasScale;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parent = (RectTransform)_rect.parent;
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _anchorsMin = _rect.anchorMin; _anchorsMax = _rect.anchorMax;
            _position = _rect.anchoredPosition; _size = _rect.sizeDelta; _scale = _rect.localScale;
        }

        private void LateUpdate()
        {
            bool mobile = Application.isMobilePlatform || (Input != null && Input.TouchEnabled);
            if (!mobile)
            {
                if (!_mobileApplied) return;
                _rect.anchorMin = _anchorsMin; _rect.anchorMax = _anchorsMax;
                _rect.anchoredPosition = _position; _rect.sizeDelta = _size; _rect.localScale = _scale;
                _mobileApplied = false;
                return;
            }
            var screen = new Vector2Int(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if (_mobileApplied && screen == _screen && safe == _safe &&
                _parent.rect.size == _parentSize && Mathf.Approximately(_canvas.scaleFactor, _canvasScale)) return;
            _screen = screen; _safe = safe; _parentSize = _parent.rect.size; _canvasScale = _canvas.scaleFactor;
            Vector2 pixels = SizeInScreenHeights * screen.y;
            Vector2 point = new Vector2(ScreenPosition.x * screen.x, ScreenPosition.y * screen.y);
            point.x = Mathf.Clamp(point.x, safe.xMin + pixels.x * _rect.pivot.x,
                safe.xMax - pixels.x * (1 - _rect.pivot.x));
            point.y = Mathf.Clamp(point.y, safe.yMin + pixels.y * _rect.pivot.y,
                safe.yMax - pixels.y * (1 - _rect.pivot.y));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, point, null, out var local);
            _rect.anchorMin = _rect.anchorMax = _parent.pivot;
            _rect.anchoredPosition = local;
            if (ScaleAuthoredContent)
                _rect.localScale = Vector3.one * (pixels.y / (_size.y * _canvasScale));
            else
                _rect.sizeDelta = pixels / _canvasScale;
            _mobileApplied = true;
        }
    }
}
