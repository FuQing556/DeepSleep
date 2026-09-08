using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive
{
    [CreateAssetMenu(
        fileName = "CFG_PlayerReviveConvergeRing",
        menuName = "DeepSleep/Players/Revive Converge Ring Config")]
    public sealed class PlayerReviveConvergeRingConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)]
        private float _outerDiameter = 2.5f;

        [SerializeField, Min(0.01f)]
        private float _innerDiameter = 0.45f;

        [SerializeField]
        private float _rotationDegreesPerSecond = -28f;

        [SerializeField, Range(0f, 1f)]
        private float _maximumAlpha = 0.9f;

        public float OuterDiameter => _outerDiameter;

        public float InnerDiameter => _innerDiameter;

        public float RotationDegreesPerSecond =>
            _rotationDegreesPerSecond;

        public float MaximumAlpha => _maximumAlpha;

        public bool TryValidate(out string reason)
        {
            if (_outerDiameter <= 0f || _innerDiameter <= 0f ||
                _innerDiameter >= _outerDiameter)
            {
                reason = "外圈直径必须大于内圈直径，且两者都必须为正数。";
                return false;
            }

            if (_maximumAlpha <= 0f || _maximumAlpha > 1f)
            {
                reason = "最大透明度必须在 0 到 1 之间。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
