using UnityEngine;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>显式 CanvasGroup 的短淡入；不接管显隐、热区、交互或布局。</summary>
    public sealed class UiPanelMotion : MonoBehaviour
    {
        public CanvasGroup Group;
        [Min(.01f)] public float Duration = .16f;
        private float _originalAlpha, _elapsed;
        private bool _playing;

        private void OnEnable()
        {
            if (Group == null)
            {
                string path = name;
                for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                Debug.LogError($"[UiPanelMotion] {path}: Group 引用缺失。", this);
                enabled = false;
                return;
            }
            _originalAlpha = Group.alpha;
            _elapsed = 0;
            _playing = true;
            Group.alpha = 0;
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Duration);
            Group.alpha = _originalAlpha * t * t * (3 - 2 * t);
            if (t >= 1) _playing = false;
        }

        private void OnDisable()
        {
            if (Group != null && _playing) Group.alpha = _originalAlpha;
            _playing = false;
        }
    }
}
