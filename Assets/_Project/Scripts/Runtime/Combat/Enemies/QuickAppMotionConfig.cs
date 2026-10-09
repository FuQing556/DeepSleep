using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/敌人/快应用大S穿屏")]
    public sealed class QuickAppMotionConfig : EnemySpawnMotionConfig
    {
        public Vector2 SpeedRange;
        public Vector2 AmplitudeRange;
        public Vector2 WavelengthRange;
        public float AxisTiltDegrees;
        public float VisualTiltDegrees;
        public LayerMask PlayerLayers;
        public override EnemySpawnVariation2D SampleVariation(System.Random random, Vector2 travelDirection)
        {
            float angle = Mathf.Atan2(travelDirection.y, travelDirection.x) +
                Mathf.Lerp(-AxisTiltDegrees, AxisTiltDegrees, (float)random.NextDouble()) * Mathf.Deg2Rad;
            return new EnemySpawnVariation2D(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                Sample(SpeedRange, random), (float)random.NextDouble() * 360f, 0f,
                Sample(AmplitudeRange, random), Sample(WavelengthRange, random));
        }
        private static float Sample(Vector2 range, System.Random random)
            => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        private static bool ValidRange(Vector2 range) => float.IsFinite(range.x) && float.IsFinite(range.y) && range.x > 0 && range.y >= range.x;
        public override bool TryValidate(out string reason)
        {
            reason = !ValidRange(SpeedRange) || !ValidRange(AmplitudeRange) || !ValidRange(WavelengthRange) ||
                !float.IsFinite(AxisTiltDegrees) || AxisTiltDegrees < 0 || AxisTiltDegrees >= 45 ||
                !float.IsFinite(VisualTiltDegrees) || VisualTiltDegrees < 0 || PlayerLayers.value == 0
                ? "快应用速度、振幅、波长、倾角或接触目标层无效。" : string.Empty;
            return reason.Length == 0;
        }
    }
}
