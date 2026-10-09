using UnityEngine;
using UnityEngine.Rendering;

namespace DeepSleep.Runtime.Presentation.Poses
{
    /// <summary>固定容量的世界空间拖影。独立于主体回池，复用姿态残影的截取/淡出算法。</summary>
    public sealed class SpriteMotionTrail2D : MonoBehaviour
    {
        public SpriteRenderer[] Renderers;
        public SortingGroup Group;
        public float IntervalSeconds;
        public float FadeSeconds;
        [Range(0f, 1f)] public float Alpha;
        private SpritePoseGhost2D[] _ghosts;
        private float _remaining;
        private int _next;

        private void Awake()
        {
            _ghosts = new SpritePoseGhost2D[Renderers.Length];
            for (int i = 0; i < _ghosts.Length; i++)
            { _ghosts[i] = new SpritePoseGhost2D(); Renderers[i].enabled = false; }
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            _remaining = Mathf.Max(0f, _remaining - deltaTime);
            for (int i = 0; i < _ghosts.Length; i++) _ghosts[i].Tick(deltaTime);
        }

        public void Sample(SpriteRenderer subject, SortingGroup sourceGroup, bool emitting)
        {
            if (!emitting) { _remaining = 0f; return; }
            if (_remaining > 0f || !subject.enabled || subject.sprite == null) return;
            if (sourceGroup != null)
            { Group.sortingLayerID = sourceGroup.sortingLayerID; Group.sortingOrder = sourceGroup.sortingOrder; }
            else { Group.sortingLayerID = subject.sortingLayerID; Group.sortingOrder = subject.sortingOrder; }
            var renderer = Renderers[_next];
            renderer.sharedMaterial = subject.sharedMaterial;
            renderer.sortingLayerID = subject.sortingLayerID;
            renderer.sortingOrder = subject.sortingOrder - 1;
            _ghosts[_next].Capture(subject, renderer, Alpha, FadeSeconds);
            _next = (_next + 1) % _ghosts.Length;
            // 低帧率不补发成叠在同一个位置的多枚拖影。
            _remaining = IntervalSeconds;
        }

        public void Clear()
        {
            _remaining = 0f; _next = 0;
            if (_ghosts == null) return;
            for (int i = 0; i < _ghosts.Length; i++) _ghosts[i].Clear();
        }
        private void OnDisable() => Clear();
    }
}
