using DeepSleep.Runtime.Presentation.Poses;
using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// 只负责 Harness 待机与终端校准贴图的切换与保持时间。
    /// </summary>
    [Serializable]
    public sealed class HarnessLaserPoseView2D
    {
        [SerializeField] private SpriteRenderer _characterRenderer;
        [SerializeField] private SpriteRenderer _ghostRenderer;
        [SerializeField] private Sprite _idleSprite;
        [SerializeField] private Sprite _calibrationSprite;
        [SerializeField] private Transform _followRoot;

        private float _firePoseRemainingSeconds;
        private readonly SpritePoseGhost2D _ghost = new();

        public void SetAiming(
            bool isAiming,
            HarnessTerminalLaserPresentationConfig config)
        {
            if (isAiming)
            {
                TransitionTo(_calibrationSprite, config);
                return;
            }

            if (_firePoseRemainingSeconds <= 0f)
            {
                TransitionTo(_idleSprite, config);
            }
        }

        public void HoldFirePose(
            float durationSeconds,
            HarnessTerminalLaserPresentationConfig config)
        {
            _firePoseRemainingSeconds = Mathf.Max(0f, durationSeconds);
            TransitionTo(_calibrationSprite, config);
        }

        public void Tick(
            float deltaTime,
            bool isAiming,
            HarnessTerminalLaserPresentationConfig config)
        {
            _ghost.Tick(deltaTime);

            if (isAiming)
            {
                return;
            }

            _firePoseRemainingSeconds = Mathf.Max(
                0f,
                _firePoseRemainingSeconds - Mathf.Max(0f, deltaTime));

            if (_firePoseRemainingSeconds <= 0f)
            {
                TransitionTo(_idleSprite, config);
            }
        }

        public void Reset()
        {
            Release();
            _ghostRenderer.enabled = false;
            _characterRenderer.sprite = _idleSprite;
        }

        public void Release()
        {
            _firePoseRemainingSeconds = 0f;
            _ghost.Clear();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_characterRenderer == null ||
                _ghostRenderer == null ||
                _idleSprite == null ||
                    _calibrationSprite == null ||
                    _followRoot == null)
            {
                reason =
                    "未配置角色渲染器、残影、贴图或残影跟随根节点。";
                return false;
            }

            if (_ghostRenderer == _characterRenderer)
            {
                reason = "角色渲染器与单残影渲染器不能是同一个组件。";
                return false;
            }

            if (_ghostRenderer.transform.parent != _followRoot)
            {
                reason = "残影必须直属跟随根节点。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void TransitionTo(
            Sprite sprite,
            HarnessTerminalLaserPresentationConfig config)
        {
            if (_characterRenderer.sprite == sprite)
            {
                return;
            }

            Sprite previousSprite = _characterRenderer.sprite;

            if (previousSprite != null)
            {
                _ghost.Capture(_characterRenderer, _ghostRenderer,
                    config.PoseGhostStartAlpha, config.PoseGhostFadeSeconds);
            }

            // 新状态在切换瞬间立即完整显示；旧状态只占用唯一 Ghost。
            _characterRenderer.sprite = sprite;
        }

    }
}
