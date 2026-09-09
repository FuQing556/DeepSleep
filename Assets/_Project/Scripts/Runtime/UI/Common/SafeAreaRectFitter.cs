using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>把自身 RectTransform 限制在当前设备安全区域内。</summary>
    public sealed class SafeAreaRectFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform _target;

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

            _target.anchorMin = new Vector2(
                safeArea.xMin / screenSize.x,
                safeArea.yMin / screenSize.y);
            _target.anchorMax = new Vector2(
                safeArea.xMax / screenSize.x,
                safeArea.yMax / screenSize.y);
            _target.offsetMin = Vector2.zero;
            _target.offsetMax = Vector2.zero;
            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
        }
    }
}
