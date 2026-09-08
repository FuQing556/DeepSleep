using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Presentation
{
    /// <summary>
    /// Harness 终端激光的纯视觉参数。
    /// 修改此资产不会改变锁定时间、束宽或伤害判定。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_HA_TerminalLaserPresentation_",
        menuName = "DeepSleep/配置/表现/Harness 终端激光")]
    public sealed class HarnessTerminalLaserPresentationConfig :
        ScriptableObject
    {
        [Header("校准")]
        [SerializeField, Min(0.001f)] private float _aimGuideWidth = 0.035f;
        [SerializeField] private Color _aimGuideColor =
            new(1f, 0.08f, 0.08f, 0.32f);
        [SerializeField, Min(0.01f)]
        private float _aimGuideTextureRepeatWorldLength = 2.25f;
        [SerializeField]
        private float _aimGuideTextureScrollWorldUnitsPerSecond = 1.5f;
        [SerializeField, Min(0.01f)] private float _reticleWorldDiameter = 1.5f;
        [SerializeField, Min(0.01f)]
        private float _reticleStartScaleMultiplier = 1.12f;
        [SerializeField, Min(0f)] private float _reticlePulseScale = 0.12f;
        [SerializeField, Min(0f)] private float _reticlePulseCyclesPerSecond = 3f;
        [SerializeField] private float _reticleRotationDegreesPerSecond = 120f;
        [SerializeField] private Color _calibratingReticleColor =
            new(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color _queuedReticleColor =
            new(1f, 1f, 1f, 0.32f);

        [Header("开火")]
        [SerializeField, Min(0f)] private float _beamFadeInSeconds = 0.025f;
        [SerializeField, Min(0f)] private float _beamHoldSeconds = 0.075f;
        [SerializeField, Min(0f)] private float _beamFadeOutSeconds = 0.16f;
        [SerializeField] private Color _beamColor = Color.white;
        [SerializeField, Min(0.01f)]
        private float _beamTextureRepeatWorldLength = 2.25f;
        [SerializeField]
        private float _beamTextureScrollWorldUnitsPerSecond = 4f;
        [SerializeField, Min(0.01f)] private float _muzzleWorldDiameter = 0.52f;
        [SerializeField] private Color _muzzleColor = Color.white;
        [SerializeField, Range(0f, 1f)]
        private float _muzzleChargingStartAlpha = 0.4f;
        [SerializeField, Range(0.01f, 1f)]
        private float _muzzleChargingStartScaleMultiplier = 0.65f;
        [SerializeField, Range(0f, 1f)]
        private float _queuedMuzzleAlpha = 0.2f;
        [SerializeField, Range(0.01f, 1f)]
        private float _queuedMuzzleScaleMultiplier = 0.5f;

        [Header("命中反馈")]
        [SerializeField, Min(0.01f)]
        private float _hitEffectDurationSeconds = 0.18f;
        [SerializeField, Min(0.01f)]
        private float _hitEffectMinimumWorldDiameter = 0.42f;
        [SerializeField, Min(0.01f)]
        private float _hitEffectBeamWidthMultiplier = 2.2f;
        [SerializeField, Range(0.01f, 1f)]
        private float _hitEffectStartScaleMultiplier = 0.45f;
        [SerializeField, Min(1f)]
        private float _hitEffectEndScaleMultiplier = 1.25f;
        [SerializeField, Range(0f, 1f)]
        private float _hitEffectFadeStart01 = 0.28f;
        [SerializeField, Min(0f)]
        private float _hitEffectMinimumRotationDegrees = 35f;
        [SerializeField, Min(0f)]
        private float _hitEffectMaximumRotationDegrees = 85f;
        [SerializeField] private Color _hitEffectColor = Color.white;
        [SerializeField, Min(1)] private int _hitEffectPoolPrewarmCount = 8;

        [Header("角色姿态")]
        [SerializeField, Min(0f)] private float _firePoseHoldSeconds = 0.3f;
        [SerializeField, Min(0.01f)]
        private float _poseGhostFadeSeconds = 0.32f;
        [SerializeField, Range(0.01f, 1f)]
        private float _poseGhostStartAlpha = 0.38f;

        public float AimGuideWidth => _aimGuideWidth;
        public Color AimGuideColor => _aimGuideColor;
        public float AimGuideTextureRepeatWorldLength =>
            _aimGuideTextureRepeatWorldLength;
        public float AimGuideTextureScrollWorldUnitsPerSecond =>
            _aimGuideTextureScrollWorldUnitsPerSecond;
        public float ReticleWorldDiameter => _reticleWorldDiameter;
        public float ReticleStartScaleMultiplier =>
            _reticleStartScaleMultiplier;
        public float ReticlePulseScale => _reticlePulseScale;
        public float ReticlePulseCyclesPerSecond =>
            _reticlePulseCyclesPerSecond;
        public float ReticleRotationDegreesPerSecond =>
            _reticleRotationDegreesPerSecond;
        public Color CalibratingReticleColor =>
            _calibratingReticleColor;
        public Color QueuedReticleColor => _queuedReticleColor;
        public float BeamFadeInSeconds => _beamFadeInSeconds;
        public float BeamHoldSeconds => _beamHoldSeconds;
        public float BeamFadeOutSeconds => _beamFadeOutSeconds;
        public float BeamTotalSeconds =>
            _beamFadeInSeconds + _beamHoldSeconds + _beamFadeOutSeconds;
        public Color BeamColor => _beamColor;
        public float BeamTextureRepeatWorldLength =>
            _beamTextureRepeatWorldLength;
        public float BeamTextureScrollWorldUnitsPerSecond =>
            _beamTextureScrollWorldUnitsPerSecond;
        public float MuzzleWorldDiameter => _muzzleWorldDiameter;
        public Color MuzzleColor => _muzzleColor;
        public float MuzzleChargingStartAlpha =>
            _muzzleChargingStartAlpha;
        public float MuzzleChargingStartScaleMultiplier =>
            _muzzleChargingStartScaleMultiplier;
        public float QueuedMuzzleAlpha => _queuedMuzzleAlpha;
        public float QueuedMuzzleScaleMultiplier =>
            _queuedMuzzleScaleMultiplier;
        public float HitEffectDurationSeconds =>
            _hitEffectDurationSeconds;
        public float HitEffectMinimumWorldDiameter =>
            _hitEffectMinimumWorldDiameter;
        public float HitEffectBeamWidthMultiplier =>
            _hitEffectBeamWidthMultiplier;
        public float HitEffectStartScaleMultiplier =>
            _hitEffectStartScaleMultiplier;
        public float HitEffectEndScaleMultiplier =>
            _hitEffectEndScaleMultiplier;
        public float HitEffectFadeStart01 => _hitEffectFadeStart01;
        public float HitEffectMinimumRotationDegrees =>
            _hitEffectMinimumRotationDegrees;
        public float HitEffectMaximumRotationDegrees =>
            _hitEffectMaximumRotationDegrees;
        public Color HitEffectColor => _hitEffectColor;
        public int HitEffectPoolPrewarmCount =>
            _hitEffectPoolPrewarmCount;
        public float FirePoseHoldSeconds => _firePoseHoldSeconds;
        public float PoseGhostFadeSeconds => _poseGhostFadeSeconds;
        public float PoseGhostStartAlpha => _poseGhostStartAlpha;

        public float EvaluateBeamAlpha(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f || elapsedSeconds >= BeamTotalSeconds)
            {
                return 0f;
            }

            if (_beamFadeInSeconds > 0f &&
                elapsedSeconds < _beamFadeInSeconds)
            {
                return Mathf.Clamp01(
                    elapsedSeconds / _beamFadeInSeconds);
            }

            float fadeOutStart =
                _beamFadeInSeconds + _beamHoldSeconds;

            if (elapsedSeconds <= fadeOutStart)
            {
                return 1f;
            }

            if (_beamFadeOutSeconds <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Clamp01(
                (elapsedSeconds - fadeOutStart) /
                _beamFadeOutSeconds);
        }

        public bool TryValidate(out string reason)
        {
            if (_aimGuideWidth <= 0f ||
                _aimGuideTextureRepeatWorldLength <= 0f ||
                _reticleWorldDiameter <= 0f ||
                _reticleStartScaleMultiplier <= 0f ||
                _beamTextureRepeatWorldLength <= 0f ||
                _muzzleWorldDiameter <= 0f ||
                _muzzleChargingStartScaleMultiplier <= 0f ||
                _queuedMuzzleScaleMultiplier <= 0f ||
                _hitEffectDurationSeconds <= 0f ||
                _hitEffectMinimumWorldDiameter <= 0f ||
                _hitEffectBeamWidthMultiplier <= 0f ||
                _hitEffectStartScaleMultiplier <= 0f ||
                _hitEffectEndScaleMultiplier < 1f ||
                _hitEffectMinimumRotationDegrees < 0f ||
                _hitEffectMaximumRotationDegrees < 0f ||
                _hitEffectPoolPrewarmCount < 1 ||
                _poseGhostFadeSeconds <= 0f)
            {
                reason = "引导线宽度和视觉尺寸必须大于 0。";
                return false;
            }

            if (_hitEffectMaximumRotationDegrees <
                _hitEffectMinimumRotationDegrees)
            {
                reason = "命中特效最大旋转角度不能小于最小旋转角度。";
                return false;
            }

            if (_reticlePulseScale < 0f ||
                _reticlePulseCyclesPerSecond < 0f ||
                _beamFadeInSeconds < 0f ||
                _beamHoldSeconds < 0f ||
                _beamFadeOutSeconds < 0f ||
                _firePoseHoldSeconds < 0f ||
                _muzzleChargingStartAlpha < 0f ||
                _muzzleChargingStartAlpha > 1f ||
                _queuedMuzzleAlpha < 0f ||
                _queuedMuzzleAlpha > 1f ||
                _poseGhostStartAlpha <= 0f ||
                _poseGhostStartAlpha > 1f)
            {
                reason = "视觉时间、频率和脉冲幅度不能小于 0。";
                return false;
            }

            if (BeamTotalSeconds <= 0f)
            {
                reason = "激光视觉总时长必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
