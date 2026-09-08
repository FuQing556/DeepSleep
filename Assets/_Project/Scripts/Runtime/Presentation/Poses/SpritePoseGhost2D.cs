using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Poses
{
    /// <summary>
    /// 单残影的公共实现。调用者决定何时截取，配置决定透明度和时长；
    /// 本类只复制相对姿态并淡出，不改变主体状态，也不创建渲染器。
    /// </summary>
    public sealed class SpritePoseGhost2D
    {
        private SpriteRenderer _renderer;
        private float _remainingSeconds;
        private float _durationSeconds;
        private float _startAlpha;

        public void Capture(SpriteRenderer subject, SpriteRenderer ghost,
            float startAlpha, float fadeSeconds)
        {
            if (subject == null || ghost == null || subject.sprite == null ||
                ghost.transform.parent == null || fadeSeconds <= 0f) return;

            Clear();
            _renderer = ghost;
            Transform source = subject.transform;
            Transform target = ghost.transform;
            Transform root = target.parent;
            target.localPosition = root.InverseTransformPoint(source.position);
            target.localRotation = Quaternion.Inverse(root.rotation) * source.rotation;
            Vector3 rootScale = root.lossyScale;
            Vector3 sourceScale = source.lossyScale;
            target.localScale = new Vector3(
                Divide(sourceScale.x, rootScale.x),
                Divide(sourceScale.y, rootScale.y),
                Divide(sourceScale.z, rootScale.z));
            ghost.sprite = subject.sprite;
            ghost.flipX = subject.flipX;
            ghost.flipY = subject.flipY;
            Color color = subject.color;
            _startAlpha = color.a * startAlpha;
            color.a = _startAlpha;
            ghost.color = color;
            ghost.enabled = true;
            _durationSeconds = fadeSeconds;
            _remainingSeconds = fadeSeconds;
        }

        public void Tick(float deltaTime)
        {
            if (_renderer == null || !_renderer.enabled) return;
            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Mathf.Max(0f, deltaTime));
            Color color = _renderer.color;
            // 保留截取时主体透明度，避免第一帧之后突然变亮。
            color.a = _startAlpha * (_remainingSeconds / _durationSeconds);
            _renderer.color = color;
            if (_remainingSeconds <= 0f) Clear();
        }

        public void Clear()
        {
            _remainingSeconds = 0f;
            if (_renderer != null) _renderer.enabled = false;
            _renderer = null;
        }

        private static float Divide(float value, float divisor)
        {
            // 只处理零缩放的数学退化，不是可调玩法阈值。
            return Mathf.Abs(divisor) <= Mathf.Epsilon ? value : value / divisor;
        }
    }
}
