using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>把自身 RectTransform 限制在当前设备安全区域内。</summary>
    public sealed class SafeAreaRectFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private bool _symmetricInsets;

        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            if (_target == null)
            {
                Debug.LogError($"[{nameof(SafeAreaRectFitter)}] 未配置目标矩形。", this);
                enabled = false;
                return;
            }

            ApplyIfChanged(true);
        }

        private void Update() => ApplyIfChanged(false);

        private void ApplyIfChanged(bool force)
        {
            Rect safeArea = Screen.safeArea;
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            {
                return;
            }

            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            safeArea = CalculateArea(safeArea, screenSize, _symmetricInsets);

            _target.anchorMin = new Vector2(
                safeArea.xMin / screenSize.x,
                safeArea.yMin / screenSize.y);
            _target.anchorMax = new Vector2(
                safeArea.xMax / screenSize.x,
                safeArea.yMax / screenSize.y);
            _target.offsetMin = Vector2.zero;
            _target.offsetMax = Vector2.zero;
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = screenSize;
        }

        /// <summary>居中内容采用左右/上下最大避让；触控按钮保留设备原始安全区域。</summary>
        public static Rect CalculateArea(Rect safeArea, Vector2Int screen, bool symmetric)
        {
            float left = Mathf.Clamp(safeArea.xMin, 0, screen.x);
            float right = Mathf.Clamp(screen.x - safeArea.xMax, 0, screen.x);
            float bottom = Mathf.Clamp(safeArea.yMin, 0, screen.y);
            float top = Mathf.Clamp(screen.y - safeArea.yMax, 0, screen.y);
            if (symmetric)
            {
                left = right = Mathf.Min(Mathf.Max(left, right), screen.x * .5f);
                bottom = top = Mathf.Min(Mathf.Max(bottom, top), screen.y * .5f);
            }
            return Rect.MinMaxRect(left, bottom, screen.x - right, screen.y - top);
        }
    }
}
