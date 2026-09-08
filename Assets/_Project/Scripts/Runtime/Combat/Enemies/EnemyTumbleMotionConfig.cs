using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 横穿屏幕并翻滚的敌人运动参数范围。
    /// 随机流由外部提供，配置本身不保存运行时状态。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_EnemyTumbleMotion_",
        menuName = "DeepSleep/配置/敌人/翻滚横穿运动")]
    public sealed class EnemyTumbleMotionConfig : EnemySpawnMotionConfig
    {
        [Header("横穿速度")]
        [SerializeField, Min(0.01f)] private float _minimumTravelSpeed = 2.6f;
        [SerializeField, Min(0.01f)] private float _maximumTravelSpeed = 3.4f;

        [Header("翻滚")]
        [SerializeField, Min(0f)]
        private float _minimumAngularSpeed = 45f;
        [SerializeField, Min(0f)]
        private float _maximumAngularSpeed = 110f;

        [Header("离场")]
        [SerializeField, Min(0f)] private float _despawnMargin = 1f;

        public float MinimumTravelSpeed => _minimumTravelSpeed;
        public float MaximumTravelSpeed => _maximumTravelSpeed;
        public float MinimumAngularSpeed => _minimumAngularSpeed;
        public float MaximumAngularSpeed => _maximumAngularSpeed;
        public float DespawnMargin => _despawnMargin;

        public override EnemySpawnVariation2D SampleVariation(
            System.Random random,
            Vector2 travelDirection)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            float travelSpeed = NextFloat(
                random,
                _minimumTravelSpeed,
                _maximumTravelSpeed);
            float angularSpeed = NextFloat(
                random,
                _minimumAngularSpeed,
                _maximumAngularSpeed);
            float angularDirection =
                random.Next(0, 2) == 0 ? -1f : 1f;
            float initialRotation = NextFloat(random, 0f, 360f);

            return new EnemySpawnVariation2D(
                travelDirection,
                travelSpeed,
                initialRotation,
                angularSpeed * angularDirection);
        }

        public override bool TryValidate(out string reason)
        {
            if (_minimumTravelSpeed <= 0f ||
                _maximumTravelSpeed <= 0f)
            {
                reason = "横穿速度必须大于 0。";
                return false;
            }

            if (_maximumTravelSpeed < _minimumTravelSpeed)
            {
                reason = "最大横穿速度不能小于最小速度。";
                return false;
            }

            if (_minimumAngularSpeed < 0f ||
                _maximumAngularSpeed < _minimumAngularSpeed)
            {
                reason = "翻滚角速度范围无效。";
                return false;
            }

            if (_despawnMargin < 0f)
            {
                reason = "离场边距不能小于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static float NextFloat(
            System.Random random,
            float minimum,
            float maximum)
        {
            return Mathf.Lerp(
                minimum,
                maximum,
                (float)random.NextDouble());
        }
    }
}
