using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    [CreateAssetMenu(
        fileName = "CFG_EnemyHitMotion_",
        menuName = "DeepSleep/Combat/Enemy Hit Motion")]
    public sealed class EnemyHitMotionConfig : ScriptableObject
    {
        [Header("DS 命中减速")]
        [SerializeField, Range(0.01f, 1f)]
        private float _deepSeekMovementMultiplier = 0.55f;
        [SerializeField, Min(0.01f)]
        private float _deepSeekSlowSeconds = 0.18f;

        [Header("HS 命中停顿")]
        [SerializeField, Min(0.01f)]
        private float _harnessStopSeconds = 0.1f;

        public float DeepSeekMovementMultiplier =>
            _deepSeekMovementMultiplier;
        public float DeepSeekSlowSeconds => _deepSeekSlowSeconds;
        public float HarnessStopSeconds => _harnessStopSeconds;

        public bool TryValidate(out string reason)
        {
            if (_deepSeekMovementMultiplier <= 0f ||
                _deepSeekMovementMultiplier > 1f)
            {
                reason = "DS移动倍率必须大于0且不超过1。";
                return false;
            }

            if (_deepSeekSlowSeconds <= 0f || _harnessStopSeconds <= 0f)
            {
                reason = "减速与停顿时间必须大于0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
