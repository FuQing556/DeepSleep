using UnityEngine;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>把一张装饰 Sprite 等比铺满相机，并保持为屏幕固定层；不参与玩法边界。</summary>
    [ExecuteAlways]
    public sealed class CameraLockedSpriteLayer2D : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Vector2 _screenCenterOffset;
        [SerializeField, Min(1f)] private float _coverageMultiplier = 1.01f;

        private float _lastAspect = -1f;
        private float _lastOrthographicSize = -1f;

        private void OnEnable() => FitNow();

        private void LateUpdate()
        {
            if (_camera == null || _renderer == null || _renderer.sprite == null) return;
            Vector3 cameraPosition = _camera.transform.position;
            transform.position = new Vector3(
                cameraPosition.x + _screenCenterOffset.x,
                cameraPosition.y + _screenCenterOffset.y,
                transform.position.z);
            if (!Mathf.Approximately(_lastAspect, _camera.aspect) ||
                !Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize))
                FitNow();
        }

        public void FitNow()
        {
            if (_camera == null || _renderer == null || _renderer.sprite == null ||
                !_camera.orthographic)
                return;

            Vector2 spriteSize = _renderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
            float viewHeight = _camera.orthographicSize * 2f;
            float viewWidth = viewHeight * _camera.aspect;
            float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) *
                _coverageMultiplier;
            transform.localScale = new Vector3(scale, scale, 1f);
            _lastAspect = _camera.aspect;
            _lastOrthographicSize = _camera.orthographicSize;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            reason = string.Empty;
            if (_camera == null || !_camera.orthographic)
            {
                reason = "必须显式配置正交玩法相机。";
                return false;
            }
            if (_renderer == null || _renderer.sprite == null)
            {
                reason = "必须显式配置带 Sprite 的渲染器。";
                return false;
            }
            return true;
        }
    }
}
