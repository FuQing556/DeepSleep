using UnityEngine;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>
    /// 有限全景：等比覆盖视野，随镜头轻微视差，不循环地标，不改变玩法相机。
    /// 使用独立、无旋转且单位缩放的父节点；场景安装器显式提供参考相机与中心。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FinitePanoramaLayer2D : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Vector2 _referenceCenter;
        [SerializeField, Min(0.01f)] private float _referenceViewHeight = 10.8f;
        [SerializeField, Min(1f)] private float _designAspect = 22f / 9f;
        [SerializeField, Min(0f)] private float _cameraTravelBudget = 0.51f;
        [SerializeField, Min(0f)] private float _edgePadding = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _cameraFollow = 0.15f;

        private void LateUpdate() => FitNow();

        public void FitNow()
        {
            if (_camera == null || !_camera.orthographic || _renderer == null ||
                _renderer.transform != transform || _renderer.sprite == null) return;

            Vector2 view = new Vector2(_camera.orthographicSize * 2f * _camera.aspect,
                _camera.orthographicSize * 2f);
            Vector2 size = _renderer.sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float scale = CalculateScale(size, view, _referenceViewHeight,
                _designAspect, _cameraTravelBudget, _edgePadding);
            transform.localScale = new Vector3(scale, scale, 1f);

            Vector2 cameraCenter = _camera.transform.position;
            Vector2 desired = _referenceCenter +
                (cameraCenter - _referenceCenter) * _cameraFollow;
            Vector2 half = size * scale * 0.5f;
            Vector2 center = ClampCenter(desired, cameraCenter, half, view * 0.5f, _edgePadding);
            // 以 Sprite bounds 中心放置，不假定导入 Pivot 必须在中间。
            Vector2 localCenter = _renderer.sprite.bounds.center;
            transform.position = new Vector3(center.x - localCenter.x * scale,
                center.y - localCenter.y * scale, transform.position.z);
        }

        public static float CalculateScale(Vector2 spriteSize, Vector2 viewSize,
            float referenceHeight, float designAspect, float travelBudget, float padding)
        {
            float height = Mathf.Max(referenceHeight, viewSize.y) + padding * 2f;
            float width = Mathf.Max(referenceHeight * designAspect, viewSize.x) +
                travelBudget * 2f + padding * 2f;
            return Mathf.Max(height / spriteSize.y, width / spriteSize.x);
        }

        public static Vector2 ClampCenter(Vector2 desired, Vector2 cameraCenter,
            Vector2 imageHalfSize, Vector2 viewHalfSize, float padding)
        {
            Vector2 allowance = Vector2.Max(Vector2.zero,
                imageHalfSize - viewHalfSize - Vector2.one * padding);
            return new Vector2(
                Mathf.Clamp(desired.x, cameraCenter.x - allowance.x, cameraCenter.x + allowance.x),
                Mathf.Clamp(desired.y, cameraCenter.y - allowance.y, cameraCenter.y + allowance.y));
        }
    }
}
