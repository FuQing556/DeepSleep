using UnityEngine;

namespace DeepSleep.Runtime.World.Scrolling
{
    /// <summary>
    /// 平滑左移两张已无缝处理的背景，并在离开一片宽度后回绕。
    /// 只处理视觉，不移动敌人、拾取物或其他玩法对象。
    /// </summary>
    public sealed class LoopingBackgroundLayer2D : MonoBehaviour
    {
        private const float POSITION_TOLERANCE = 0.01f;

        [SerializeField] private Transform _firstTile;
        [SerializeField] private Transform _secondTile;
        [SerializeField] private LoopingBackgroundLayerConfig _config;

        private float _scrollMultiplier = 1f;
        private bool _isInitialized;

        public float ScrollMultiplier => _scrollMultiplier;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(LoopingBackgroundLayer2D)}] " +
                    $"循环背景装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized ||
                _scrollMultiplier <= 0f ||
                Time.deltaTime <= 0f)
            {
                return;
            }

            float distance =
                _config.ScrollSpeedUnitsPerSecond *
                _scrollMultiplier *
                Time.deltaTime;

            MoveTileLeft(_firstTile, distance);
            MoveTileLeft(_secondTile, distance);
            WrapTilesIfNeeded();
        }

        /// <summary>
        /// 设置本层当前滚动倍率。0用于定点Boss战，1用于普通或飞行Boss战。
        /// </summary>
        public void SetScrollMultiplier(float multiplier)
        {
            _scrollMultiplier = Mathf.Max(0f, multiplier);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_firstTile == null || _secondTile == null)
            {
                reason = "必须配置两张背景节点。";
                return false;
            }

            if (_firstTile == _secondTile)
            {
                reason = "两张背景节点不能引用同一个对象。";
                return false;
            }

            if (_config == null)
            {
                reason = "未配置循环背景参数。";
                return false;
            }

            if (!_config.TryValidate(out reason))
            {
                return false;
            }

            float actualSpacing = Mathf.Abs(
                _secondTile.localPosition.x -
                _firstTile.localPosition.x);

            if (Mathf.Abs(actualSpacing - _config.TileWidth) >
                POSITION_TOLERANCE)
            {
                reason =
                    $"两张背景的本地X间距应为 {_config.TileWidth}，" +
                    $"当前为 {actualSpacing}。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void MoveTileLeft(
            Transform tile,
            float distance)
        {
            Vector3 position = tile.localPosition;
            position.x -= distance;
            tile.localPosition = position;
        }

        private void WrapTilesIfNeeded()
        {
            float wrapBoundary = -_config.TileWidth;

            if (_firstTile.localPosition.x <= wrapBoundary)
            {
                PlaceAfter(_firstTile, _secondTile);
            }

            if (_secondTile.localPosition.x <= wrapBoundary)
            {
                PlaceAfter(_secondTile, _firstTile);
            }
        }

        private void PlaceAfter(Transform tile, Transform leadingTile)
        {
            Vector3 position = tile.localPosition;
            position.x =
                leadingTile.localPosition.x + _config.TileWidth;
            tile.localPosition = position;
        }
    }
}
