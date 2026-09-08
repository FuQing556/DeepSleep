using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive.Presentation
{
    /// <summary>
    /// 将复活进度表现为等比缩小的世界空间收束环。
    /// </summary>
    public sealed class PlayerReviveConvergeRingView2D : MonoBehaviour
    {
        [SerializeField] private PlayerReviveCoordinator2D _coordinator;
        [SerializeField] private PlayerReviveConvergeRingConfig _config;
        [SerializeField] private Transform _ringTransform;
        [SerializeField] private SpriteRenderer _ringRenderer;

        private bool _isInitialized;
        private bool _isVisible;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerReviveConvergeRingView2D)}] " +
                    $"复活收束环装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
            HideAndReset();
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _coordinator.ProgressChanged += OnProgressChanged;
            SynchronizeVisual();
        }

        private void OnDisable()
        {
            if (_coordinator != null)
            {
                _coordinator.ProgressChanged -= OnProgressChanged;
            }

            HideAndReset();
        }

        private void Update()
        {
            if (!_isVisible)
            {
                return;
            }

            _ringTransform.Rotate(
                0f,
                0f,
                _config.RotationDegreesPerSecond * Time.deltaTime,
                Space.Self);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_coordinator == null || _config == null ||
                _ringTransform == null || _ringRenderer == null)
            {
                reason = "协调器、表现参数、环变换和环渲染器必须全部配置。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                return false;
            }

            if (_coordinator.gameObject != gameObject ||
                !_ringTransform.IsChildOf(transform) ||
                _ringRenderer.transform != _ringTransform ||
                _ringRenderer.sprite == null)
            {
                reason = "收束环必须是玩家子节点，并配置有效的独立精灵。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void OnProgressChanged(float progress01)
        {
            SynchronizeVisual();
        }

        private void SynchronizeVisual()
        {
            if (!_coordinator.IsReviving)
            {
                HideAndReset();
                return;
            }

            if (!_isVisible)
            {
                _ringTransform.localRotation = Quaternion.identity;
                _ringRenderer.enabled = true;
                _isVisible = true;
            }

            float diameter = Mathf.Lerp(
                _config.OuterDiameter,
                _config.InnerDiameter,
                _coordinator.Progress01);
            float spriteWidth = _ringRenderer.sprite.bounds.size.x;
            float uniformScale = diameter / spriteWidth;
            _ringTransform.localScale = Vector3.one * uniformScale;

            Color color = _ringRenderer.color;
            color.a = _config.MaximumAlpha;
            _ringRenderer.color = color;
        }

        private void HideAndReset()
        {
            _isVisible = false;

            if (_ringRenderer != null)
            {
                _ringRenderer.enabled = false;
            }

            if (_ringTransform != null)
            {
                _ringTransform.localRotation = Quaternion.identity;
            }
        }
    }
}
