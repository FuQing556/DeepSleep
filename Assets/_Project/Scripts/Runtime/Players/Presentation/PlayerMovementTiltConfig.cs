using UnityEngine;

namespace DeepSleep.Runtime.Players.Presentation
{
    /// <summary>
    /// 玩家移动倾斜的纯表现参数，不参与碰撞和移动判定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_PlayerMovementTilt_",
        menuName = "DeepSleep/配置/玩家移动倾斜")]
    public sealed class PlayerMovementTiltConfig : ScriptableObject
    {
        [SerializeField] private float _maximumTiltDegrees;
        [SerializeField] private float _tiltSpeedDegreesPerSecond;
        [SerializeField] private float _returnSpeedDegreesPerSecond;

        public float MaximumTiltDegrees => _maximumTiltDegrees;

        public float TiltSpeedDegreesPerSecond =>
            _tiltSpeedDegreesPerSecond;

        public float ReturnSpeedDegreesPerSecond =>
            _returnSpeedDegreesPerSecond;

        public bool TryValidate(out string reason)
        {
            if (_maximumTiltDegrees <= 0f)
            {
                reason = "最大倾斜角必须大于 0。";
                return false;
            }

            if (_tiltSpeedDegreesPerSecond <= 0f)
            {
                reason = "倾斜速度必须大于 0。";
                return false;
            }

            if (_returnSpeedDegreesPerSecond <= 0f)
            {
                reason = "回正速度必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
