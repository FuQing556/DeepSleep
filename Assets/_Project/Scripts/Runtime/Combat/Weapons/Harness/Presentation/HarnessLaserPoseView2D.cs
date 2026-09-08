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
        private float _ghostFadeRemainingSeconds;

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
            FadeGhost(Mathf.Max(0f, deltaTime), config);

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
            _firePoseRemainingSeconds = 0f;
            _ghostFadeRemainingSeconds = 0f;
            _ghostRenderer.enabled = false;
            _characterRenderer.sprite = _idleSprite;
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
                CopyCharacterToGhost(previousSprite, config);
            }

            // 新状态在切换瞬间立即完整显示；旧状态只占用唯一 Ghost。
            _characterRenderer.sprite = sprite;
        }

        private void CopyCharacterToGhost(
            Sprite previousSprite,
            HarnessTerminalLaserPresentationConfig config)
        {
            Transform characterTransform = _characterRenderer.transform;
            Transform ghostTransform = _ghostRenderer.transform;
            ghostTransform.localPosition =
                _followRoot.InverseTransformPoint(characterTransform.position);
            ghostTransform.localRotation =
                Quaternion.Inverse(_followRoot.rotation) *
                characterTransform.rotation;
            Vector3 rootScale = _followRoot.lossyScale;
            Vector3 characterScale = characterTransform.lossyScale;
            ghostTransform.localScale = new Vector3(
                DivideScale(characterScale.x, rootScale.x),
                DivideScale(characterScale.y, rootScale.y),
                DivideScale(characterScale.z, rootScale.z));

            _ghostRenderer.sprite = previousSprite;
            _ghostRenderer.flipX = _characterRenderer.flipX;
            _ghostRenderer.flipY = _characterRenderer.flipY;
            Color ghostColor = _characterRenderer.color;
            ghostColor.a *= config.PoseGhostStartAlpha;
            _ghostRenderer.color = ghostColor;
            _ghostRenderer.enabled = true;
            _ghostFadeRemainingSeconds = config.PoseGhostFadeSeconds;
        }

        private static float DivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= Mathf.Epsilon
                ? value
                : value / divisor;
        }

        private void FadeGhost(
            float deltaTime,
            HarnessTerminalLaserPresentationConfig config)
        {
            if (!_ghostRenderer.enabled)
            {
                return;
            }

            _ghostFadeRemainingSeconds = Mathf.Max(
                0f,
                _ghostFadeRemainingSeconds - deltaTime);
            float alpha = config.PoseGhostStartAlpha *
                (_ghostFadeRemainingSeconds / config.PoseGhostFadeSeconds);
            Color color = _ghostRenderer.color;
            color.a = alpha;
            _ghostRenderer.color = color;

            if (_ghostFadeRemainingSeconds <= 0f)
            {
                _ghostRenderer.enabled = false;
            }
        }
    }
}
