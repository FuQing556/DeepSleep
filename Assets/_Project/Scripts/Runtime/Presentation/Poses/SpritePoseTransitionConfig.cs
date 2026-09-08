using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Poses
{
    [CreateAssetMenu(
        fileName = "CFG_SpritePoseTransition_",
        menuName = "DeepSleep/配置/表现/精灵姿态切换")]
    public sealed class SpritePoseTransitionConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.32f;
        [SerializeField, Range(0.01f, 1f)] private float _startAlpha = 0.38f;

        public float FadeSeconds => _fadeSeconds;
        public float StartAlpha => _startAlpha;

        public bool TryValidate(out string reason)
        {
            if (_fadeSeconds <= 0f || _startAlpha <= 0f || _startAlpha > 1f)
            {
                reason = "淡出时间和初始透明度无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
