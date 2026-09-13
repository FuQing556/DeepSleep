using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 保存敌人的短暂受击运动修正。停顿优先于减速，重复命中只刷新时长。
    /// 本组件不推进敌人的攻击、索敌或生命状态。
    /// </summary>
    public sealed class EnemyHitMotionController2D :
        MonoBehaviour,
        IHitMotionReceiver2D
    {
        [SerializeField] private EnemyHitMotionConfig _config;

        private float _slowRemainingSeconds;
        private float _stopRemainingSeconds;

        public float MovementTimeScale
        {
            get
            {
                if (_stopRemainingSeconds > 0f)
                {
                    return 0f;
                }

                return _slowRemainingSeconds > 0f
                    ? _config.DeepSeekMovementMultiplier
                    : 1f;
            }
        }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(EnemyHitMotionController2D)}] " +
                    $"敌人受击运动反馈装配无效：{reason}",
                    this);
                enabled = false;
            }
        }

        private void OnDisable() => ResetState();

        public void ApplyHitMotion(HitMotionKind kind)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            switch (kind)
            {
                case HitMotionKind.DeepSeekSlow:
                    _slowRemainingSeconds = _config.DeepSeekSlowSeconds;
                    break;

                case HitMotionKind.HarnessStop:
                    _stopRemainingSeconds = _config.HarnessStopSeconds;
                    break;
            }
        }

        public void Simulate(float deltaTime)
        {
            if (!isActiveAndEnabled || deltaTime <= 0f)
            {
                return;
            }

            _slowRemainingSeconds = Mathf.Max(
                0f,
                _slowRemainingSeconds - deltaTime);
            _stopRemainingSeconds = Mathf.Max(
                0f,
                _stopRemainingSeconds - deltaTime);
        }

        public void ResetState()
        {
            _slowRemainingSeconds = 0f;
            _stopRemainingSeconds = 0f;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_config == null)
            {
                reason = "未配置受击运动参数。";
                return false;
            }

            return _config.TryValidate(out reason);
        }
    }
}
