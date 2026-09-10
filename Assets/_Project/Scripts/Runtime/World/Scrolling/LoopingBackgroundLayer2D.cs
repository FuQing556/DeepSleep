using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.World.Scrolling
{
    /// <summary>
    /// 平滑左移无缝背景，并按相机当前可见范围回绕。
    /// 只处理视觉，不移动敌人、拾取物或其他玩法对象。
    /// </summary>
    public sealed class LoopingBackgroundLayer2D : MonoBehaviour
    {
        private const float POSITION_TOLERANCE = 0.01f;

        [SerializeField] private Transform _firstTile;
        [SerializeField] private Transform _secondTile;
        [SerializeField] private LoopingBackgroundLayerConfig _config;
        [SerializeField] private Camera _coverageCamera;
        [SerializeField, Min(0f)] private float _coveragePadding = 0.6f;

        private readonly List<Transform> _tiles = new List<Transform>();
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

            _coverageCamera ??= Camera.main;
            if (_coverageCamera == null || !_coverageCamera.orthographic)
            {
                Debug.LogError(
                    $"[{nameof(LoopingBackgroundLayer2D)}] " +
                    "需要配置正交摄像机以计算背景覆盖范围。",
                    this);
                enabled = false;
                return;
            }

            BuildCoverageTiles();
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

            for (int i = 0; i < _tiles.Count; i++)
            {
                MoveTileLeft(_tiles[i], distance);
            }

            EnsureCameraCoverage();
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

            if (!TryGetTileRenderer(_firstTile, out SpriteRenderer first) ||
                !TryGetTileRenderer(_secondTile, out SpriteRenderer second))
            {
                reason = "两张背景节点都必须直接挂载 SpriteRenderer。";
                return false;
            }

            float firstWidth = first.bounds.size.x;
            float secondWidth = second.bounds.size.x;
            if (firstWidth <= 0f ||
                Mathf.Abs(firstWidth - secondWidth) > POSITION_TOLERANCE)
            {
                reason = "两张背景图片的实际世界宽度必须相同且大于0。";
                return false;
            }

            if (Mathf.Abs(firstWidth - _config.TileWidth) >
                POSITION_TOLERANCE)
            {
                reason =
                    $"背景图片实际宽度为 {firstWidth}，" +
                    $"配置宽度为 {_config.TileWidth}。";
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
            Vector3 position = tile.position;
            position.x -= distance;
            tile.position = position;
        }

        private void EnsureCameraCoverage()
        {
            float cameraLeft =
                _coverageCamera.transform.position.x -
                GetCameraHalfWidth() -
                _coveragePadding;
            float cameraRight =
                _coverageCamera.transform.position.x +
                GetCameraHalfWidth() +
                _coveragePadding;
            int safety = _tiles.Count * 2;

            while (safety-- > 0)
            {
                Transform leftmost = FindExtremeTile(findLeftmost: true);
                Transform rightmost = FindExtremeTile(findLeftmost: false);
                Bounds leftBounds = GetTileBounds(leftmost);
                Bounds rightBounds = GetTileBounds(rightmost);

                if (leftBounds.min.x > cameraLeft)
                {
                    PlaceBefore(rightmost, leftmost);
                    continue;
                }

                if (rightBounds.max.x < cameraRight)
                {
                    PlaceAfter(leftmost, rightmost);
                    continue;
                }

                break;
            }
        }

        private void PlaceAfter(Transform tile, Transform leadingTile)
        {
            float targetLeft = GetTileBounds(leadingTile).max.x;
            MoveTileLeftEdgeTo(tile, targetLeft);
        }

        private void PlaceBefore(Transform tile, Transform trailingTile)
        {
            float targetRight = GetTileBounds(trailingTile).min.x;
            MoveTileRightEdgeTo(tile, targetRight);
        }

        private void BuildCoverageTiles()
        {
            _tiles.Clear();
            _tiles.Add(_firstTile);
            _tiles.Add(_secondTile);

            float tileWidth = GetTileBounds(_firstTile).size.x;
            float visibleWidth =
                GetCameraHalfWidth() * 2f + _coveragePadding * 2f;
            int requiredTileCount = Mathf.Max(
                3,
                Mathf.CeilToInt(visibleWidth / tileWidth) + 1);

            while (_tiles.Count < requiredTileCount)
            {
                GameObject clone = Instantiate(
                    _secondTile.gameObject,
                    _secondTile.parent);
                clone.name = $"Tile_{(char)('A' + _tiles.Count)}";
                _tiles.Add(clone.transform);
            }

            float cameraX = _coverageCamera.transform.position.x;
            float firstCenter =
                cameraX - (_tiles.Count - 1) * tileWidth * 0.5f;

            for (int i = 0; i < _tiles.Count; i++)
            {
                MoveTileCenterTo(
                    _tiles[i],
                    firstCenter + i * tileWidth);
            }

            EnsureCameraCoverage();
        }

        private Transform FindExtremeTile(bool findLeftmost)
        {
            Transform result = _tiles[0];
            for (int i = 1; i < _tiles.Count; i++)
            {
                bool isMoreExtreme = findLeftmost
                    ? GetTileBounds(_tiles[i]).min.x <
                        GetTileBounds(result).min.x
                    : GetTileBounds(_tiles[i]).max.x >
                        GetTileBounds(result).max.x;
                if (isMoreExtreme)
                {
                    result = _tiles[i];
                }
            }

            return result;
        }

        private float GetCameraHalfWidth()
        {
            return _coverageCamera.orthographicSize * _coverageCamera.aspect;
        }

        private static bool TryGetTileRenderer(
            Transform tile,
            out SpriteRenderer renderer)
        {
            renderer = tile != null
                ? tile.GetComponent<SpriteRenderer>()
                : null;
            return renderer != null && renderer.sprite != null;
        }

        private static Bounds GetTileBounds(Transform tile)
        {
            return tile.GetComponent<SpriteRenderer>().bounds;
        }

        private static void MoveTileCenterTo(
            Transform tile,
            float targetCenterX)
        {
            Bounds bounds = GetTileBounds(tile);
            MoveTileByWorldDelta(tile, targetCenterX - bounds.center.x);
        }

        private static void MoveTileLeftEdgeTo(
            Transform tile,
            float targetLeftX)
        {
            Bounds bounds = GetTileBounds(tile);
            MoveTileByWorldDelta(tile, targetLeftX - bounds.min.x);
        }

        private static void MoveTileRightEdgeTo(
            Transform tile,
            float targetRightX)
        {
            Bounds bounds = GetTileBounds(tile);
            MoveTileByWorldDelta(tile, targetRightX - bounds.max.x);
        }

        private static void MoveTileByWorldDelta(
            Transform tile,
            float deltaX)
        {
            Vector3 position = tile.position;
            position.x += deltaX;
            tile.position = position;
        }
    }
}
