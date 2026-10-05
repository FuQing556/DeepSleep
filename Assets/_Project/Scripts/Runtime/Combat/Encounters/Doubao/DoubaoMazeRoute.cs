using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Doubao
{
    /// <summary>固定种子的滚动路径；先规划路线，再以实体净空筛选墙组。</summary>
    public sealed class DoubaoMazeRoute
    {
        private readonly List<int> _columns = new();
        private readonly System.Random _random;
        private readonly int _count;
        private readonly int _holdRows;
        private readonly float _width;
        private readonly float _pitch;
        public DoubaoMazeRoute(int seed, int columns, int holdRows, float width, float pitch)
        {
            _random = new System.Random(seed);
            _count = columns;
            _holdRows = holdRows;
            _width = width;
            _pitch = pitch;
            _columns.Add(columns / 2);
        }

        public Vector2 Point(int row)
        {
            int index = Mathf.Max(0, row);
            while (_columns.Count <= index)
            {
                int next = _columns[_columns.Count - 1];
                if (_columns.Count % _holdRows == 0)
                {
                    int direction = _random.Next(2) == 0 ? -1 : 1;
                    if (next + direction < 0 || next + direction >= _count) direction = -direction;
                    next += direction;
                }
                _columns.Add(next);
            }
            return new Vector2(-_width * .5f + (_columns[index] + .5f) * _width / _count, row * _pitch);
        }

        public bool IntersectsRoute(Vector2 bubbleCenter, float radius, int row)
        {
            int reach = Mathf.CeilToInt(radius / _pitch) + 2;
            for (int i = row - reach; i <= row + reach; i++)
            {
                Vector2 a = Point(i), b = Point(i + 1);
                Vector2 segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(bubbleCenter - a, segment) / segment.sqrMagnitude);
                if ((bubbleCenter - (a + segment * t)).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }
    }
}
