using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI
{
    /// <summary>独立全屏雨层；显式开启，不跟随镜头或战斗倍速。</summary>
    public sealed class RainOverlayView : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public RawImage Image;
            [Min(1)] public float TileHeight;
            [Min(0)] public float UnitsPerSecond;
            public float InitialPhase;
            [NonSerialized] internal float Phase;
        }

        public RectTransform FullScreenRect;
        public CanvasGroup Group;
        public Layer[] Layers;
        [Range(0.01f, 0.45f)] public float SeamOverlap = 0.12f;
        [Min(0)] public float FadeSeconds = 0.6f;
        private bool _visible;
        private bool _allowPausedFade;

        private void Awake()
        {
            Group.alpha = 0;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            for (int i = 0; i < Layers.Length; i++)
                Layers[i].Phase = Layers[i].InitialPhase;
            RefreshUvs();
        }

        public void SetWeatherActive(bool active, bool allowFadeWhilePaused = false)
        {
            _visible = active;
            _allowPausedFade = allowFadeWhilePaused;
        }

        /// <summary>退出/重开立即清理表现；不改变全局暂停所有权。</summary>
        public void ResetWeather()
        {
            _visible = _allowPausedFade = false;
            Group.alpha = 0;
            for (int i = 0; i < Layers.Length; i++) Layers[i].Phase = Layers[i].InitialPhase;
            RefreshUvs();
        }

        private void LateUpdate()
        {
            Advance(Time.unscaledDeltaTime, Time.timeScale <= 0);
        }

        // 独立时钟入口，也用于隔离验证；暂停时连淡入淡出一起冻结。
        public void Advance(float seconds, bool paused)
        {
            if ((paused && !_allowPausedFade) || seconds <= 0) return;
            Group.alpha = FadeSeconds <= 0 ? (_visible ? 1 : 0) :
                Mathf.MoveTowards(Group.alpha, _visible ? 1 : 0, seconds / FadeSeconds);
            if (!paused && Group.alpha > 0)
                for (int i = 0; i < Layers.Length; i++)
                {
                    Layer layer = Layers[i];
                    layer.Phase = Mathf.Repeat(layer.Phase + seconds * layer.UnitsPerSecond / layer.TileHeight,
                        1 - SeamOverlap);
                }
            RefreshUvs();
        }

        private void RefreshUvs()
        {
            Rect screen = FullScreenRect.rect;
            for (int i = 0; i < Layers.Length; i++)
            {
                Layer layer = Layers[i];
                Texture texture = layer.Image.texture;
                float width = layer.TileHeight * texture.width / texture.height;
                layer.Image.uvRect = new Rect(0, layer.Phase, screen.width / width, screen.height / layer.TileHeight);
            }
        }
    }
}
