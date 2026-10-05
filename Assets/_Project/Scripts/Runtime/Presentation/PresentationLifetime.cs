using UnityEngine;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>短表现从触发帧之后计龄，保证不会在首次可渲染前被该帧完整deltaTime回收。</summary>
    public struct PresentationLifetime
    {
        private int _lastFrame;
        public float Elapsed { get; private set; }
        public void Begin(int frame) { _lastFrame = frame; Elapsed = 0f; }
        public float Advance(int frame, float deltaTime)
        {
            if (frame == _lastFrame) return Elapsed;
            _lastFrame = frame;
            Elapsed += Mathf.Max(0f, deltaTime);
            return Elapsed;
        }
    }
}
