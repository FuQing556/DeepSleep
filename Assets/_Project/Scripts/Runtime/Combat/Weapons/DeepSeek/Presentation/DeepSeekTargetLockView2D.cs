using DeepSleep.Runtime.Combat.Beams.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Presentation
{
    /// <summary>
    /// 将 DeepSeek 已存在的手动锁定结果表现为跟随目标的旋转标记。
    /// 本组件不搜索目标，也不参与射击和伤害。
    /// </summary>
    public sealed class DeepSeekTargetLockView2D : MonoBehaviour
    {
        [SerializeField]
        private DeepSeekManualTargetController _targetController;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private DeepSeekTargetLockViewConfig _config;

        private float _visibleSeconds;
        private bool _wasVisible;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(DeepSeekTargetLockView2D)}] " +
                    $"锁定标记装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            Hide();
        }

        private void LateUpdate()
        {
            if (!_targetController.TryGetLockedTargetPosition(
                    out Vector2 targetPosition))
            {
                Hide();
                return;
            }

            if (!_wasVisible)
            {
                _visibleSeconds = 0f;
                _wasVisible = true;
            }

            _visibleSeconds += Mathf.Max(0f, Time.deltaTime);
            float pulsePhase =
                _visibleSeconds *
                _config.PulseCyclesPerSecond *
                Mathf.PI * 2f;
            float pulse = (Mathf.Sin(pulsePhase) + 1f) * 0.5f;
            float diameter =
                _config.WorldDiameter *
                (1f + pulse * _config.PulseScale);

            WorldSpriteGeometry2D.ShowWithWorldDiameter(
                _renderer,
                targetPosition,
                diameter,
                _visibleSeconds * _config.RotationDegreesPerSecond,
                _config.Color);
        }

        private void OnDisable()
        {
            Hide();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_targetController == null)
            {
                reason = "未配置手动目标控制器。";
                return false;
            }

            if (_renderer == null || _renderer.sprite == null)
            {
                reason = "未配置锁定标记渲染器与贴图。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置锁定标记参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }

        private void Hide()
        {
            _wasVisible = false;
            _visibleSeconds = 0f;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }
    }
}
