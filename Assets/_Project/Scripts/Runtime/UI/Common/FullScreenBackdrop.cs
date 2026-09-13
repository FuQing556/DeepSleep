using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>只扩展背景图片至根Canvas，按钮仍留在安全区。随所属弹窗显隐。</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class FullScreenBackdrop : MonoBehaviour
    {
        private readonly Vector3[] _corners = new Vector3[4];
        private void OnEnable() => Fit();
        private void LateUpdate() => Fit();
        public void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (parent == null || canvas == null) return;
            ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(_corners);
            Vector2 min = parent.InverseTransformPoint(_corners[0]);
            Vector2 max = parent.InverseTransformPoint(_corners[2]);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = min - parent.rect.min;
            rect.sizeDelta = max - min;
        }
    }
}
