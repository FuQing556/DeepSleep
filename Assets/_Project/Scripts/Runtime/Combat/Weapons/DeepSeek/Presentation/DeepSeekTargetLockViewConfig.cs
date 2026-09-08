using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Presentation
{
    /// <summary>
    /// DeepSeek 手动锁定标记的纯表现参数。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_DS_TargetLockView_",
        menuName = "DeepSleep/配置/表现/DeepSeek 锁定标记")]
    public sealed class DeepSeekTargetLockViewConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _worldDiameter = 1.45f;
        [SerializeField, Min(0f)] private float _pulseScale = 0.08f;
        [SerializeField, Min(0f)] private float _pulseCyclesPerSecond = 2.25f;
        [SerializeField] private float _rotationDegreesPerSecond = 72f;
        [SerializeField] private Color _color = Color.white;

        public float WorldDiameter => _worldDiameter;
        public float PulseScale => _pulseScale;
        public float PulseCyclesPerSecond => _pulseCyclesPerSecond;
        public float RotationDegreesPerSecond => _rotationDegreesPerSecond;
        public Color Color => _color;

        public bool TryValidate(out string reason)
        {
            if (_worldDiameter <= 0f ||
                _pulseScale < 0f ||
                _pulseCyclesPerSecond < 0f)
            {
                reason = "世界尺寸必须大于 0，脉冲参数不能小于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
