using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    [CreateAssetMenu(
        fileName = "CFG_EN_DataCrawlerSnake_Attack_",
        menuName = "DeepSleep/Combat/Enemies/Data Crawler Snake Attack")]
    public sealed class DataCrawlerSnakeAttackConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _attackRange = 3.4f;
        [SerializeField, Min(0.01f)] private float _chargeSeconds = 0.65f;
        [SerializeField, Min(0f)] private float _firePoseSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float _recoverySeconds = 1.15f;
        [SerializeField, Min(1f)] private float _cancelRangeMultiplier = 1.35f;

        public float AttackRange => _attackRange;
        public float ChargeSeconds => _chargeSeconds;
        public float FirePoseSeconds => _firePoseSeconds;
        public float RecoverySeconds => _recoverySeconds;
        public float CancelRange => _attackRange * _cancelRangeMultiplier;

        public bool TryValidate(out string reason)
        {
            if (_attackRange <= 0f || _chargeSeconds <= 0f ||
                _firePoseSeconds < 0f || _recoverySeconds <= 0f ||
                _cancelRangeMultiplier < 1f)
            {
                reason = "攻击距离、蓄力、后摇与取消距离参数无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
