using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake
{
    [CreateAssetMenu(
        fileName = "CFG_EN_DataCrawlerSnake_Motion_",
        menuName = "DeepSleep/配置/敌人/数据爬虫蛇/追踪运动")]
    public sealed class DataCrawlerSnakeMotionConfig :
        EnemySpawnMotionConfig
    {
        [Header("追踪速度")]
        [SerializeField, Min(0.01f)] private float _minimumSpeed = 0.9f;
        [SerializeField, Min(0.01f)] private float _maximumSpeed = 1.3f;
        [SerializeField, Min(0f)] private float _stoppingDistance = 2.8f;

        [Header("索敌与离场")]
        [SerializeField, Min(0.02f)] private float _retargetInterval = 0.2f;
        [SerializeField, Min(0f)] private float _despawnMargin = 1.5f;

        public float StoppingDistance => _stoppingDistance;
        public float RetargetInterval => _retargetInterval;
        public float DespawnMargin => _despawnMargin;

        public override EnemySpawnVariation2D SampleVariation(
            System.Random random,
            Vector2 travelDirection)
        {
            float speed = Mathf.Lerp(
                _minimumSpeed,
                _maximumSpeed,
                (float)random.NextDouble());
            return new EnemySpawnVariation2D(
                travelDirection,
                speed,
                0f,
                0f);
        }

        public override bool TryValidate(out string reason)
        {
            if (_minimumSpeed <= 0f || _maximumSpeed < _minimumSpeed)
            {
                reason = "追踪速度范围无效。";
                return false;
            }

            if (_stoppingDistance < 0f || _retargetInterval <= 0f ||
                _despawnMargin < 0f)
            {
                reason = "停止距离、索敌间隔或离场边距无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
