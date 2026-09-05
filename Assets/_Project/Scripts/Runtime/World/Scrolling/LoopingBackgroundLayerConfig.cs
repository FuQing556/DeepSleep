using UnityEngine;

namespace DeepSleep.Runtime.World.Scrolling
{
    /// <summary>
    /// 单层循环背景的宽度与基础滚动速度。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_LoopingBackgroundLayer_",
        menuName = "DeepSleep/配置/循环背景层")]
    public sealed class LoopingBackgroundLayerConfig : ScriptableObject
    {
        [SerializeField] private float _tileWidth;
        [SerializeField] private float _scrollSpeedUnitsPerSecond;

        public float TileWidth => _tileWidth;

        public float ScrollSpeedUnitsPerSecond =>
            _scrollSpeedUnitsPerSecond;

        public bool TryValidate(out string reason)
        {
            if (_tileWidth <= 0f)
            {
                reason = "背景单片宽度必须大于 0。";
                return false;
            }

            if (_scrollSpeedUnitsPerSecond <= 0f)
            {
                reason = "背景滚动速度必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
