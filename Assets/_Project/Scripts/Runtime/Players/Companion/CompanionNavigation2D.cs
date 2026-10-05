using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>导航只提出碰撞体中心的路点，不移动实体、不读迷宫生成路线、不改变伤害规则。</summary>
    public readonly struct CompanionNavigationResult2D
    {
        public Vector2 Waypoint { get; }
        public bool HasPath { get; }
        public bool ShouldWait { get; }
        public bool ReachedGoal { get; }
        public bool IsStuck { get; }
        public int PathLength { get; }

        public CompanionNavigationResult2D(Vector2 waypoint, bool hasPath, bool shouldWait,
            bool reachedGoal, bool isStuck, int pathLength)
        {
            Waypoint = waypoint; HasPath = hasPath; ShouldWait = shouldWait;
            ReachedGoal = reachedGoal; IsStuck = isStuck; PathLength = pathLength;
        }
    }

    /// <summary>
    /// 有界 A* 路线 + 短时动态危险预测。所有工作缓冲只在实例创建时分配。
    /// 超出预测窗口的泡停在预测位置；后续决策根据新快照重规划，不能把旧远端路线当作安全保证。
    /// </summary>
    public sealed class CompanionNavigation2D
    {
        // 这是容量/执行上限，不是玩法参数；过密栅格会等比粗化，危险快照溢出则停止规划。
        private const int MAX_NODES = 4096;
        private const int MAX_HAZARDS = 256;
        private const int WORDS_PER_NODE = MAX_HAZARDS / 32;
        private const float EPSILON = .000001f;
        private readonly float[] _cost = new float[MAX_NODES];
        private readonly float[] _score = new float[MAX_NODES];
        private readonly float[] _travelCost = new float[MAX_NODES];
        private readonly int[] _parent = new int[MAX_NODES];
        private readonly byte[] _state = new byte[MAX_NODES];
        private readonly int[] _heap = new int[MAX_NODES];
        private readonly int[] _heapPosition = new int[MAX_NODES];
        private readonly uint[] _candidates = new uint[MAX_NODES * WORDS_PER_NODE];
        private readonly Vector2[] _positions = new Vector2[MAX_NODES];
        private readonly byte[] _positionReady = new byte[MAX_NODES];
        private readonly Vector2[] _path = new Vector2[MAX_NODES];
        private float _cellSize, _padding, _prediction, _replanInterval, _goalHysteresis, _stuckSeconds;
        private int _expansionLimit;
        private bool _configured, _hasGoal, _wasMoving;
        private Vector2 _goal, _progressAnchor, _blockedDirection, _lastWaypoint;
        private float _replanRemaining, _stuckElapsed;
        private int _pathCount, _pathIndex, _heapCount, _columns, _rows, _startNode, _goalNode;
        private float _spacing, _speed, _startupDelay;
        private Vector2 _origin, _start, _extent;
        private CompanionObstacleSnapshot[] _hazards;
        private int _hazardCount, _candidateWords;

        public int LastExpandedNodes { get; private set; }
        public int RemainingPathCount => Mathf.Max(0, _pathCount - _pathIndex);

        /// <summary>参数由现役战术配置显式提供；重复同参数调用不清空状态。</summary>
        public void Configure(float cellSize, float padding, float predictionSeconds, float replanSeconds,
            float goalHysteresis, float stuckSeconds, int maxExpandedNodes)
        {
            _cellSize = Mathf.Max(.05f, cellSize);
            _padding = Mathf.Max(0f, padding);
            _prediction = Mathf.Max(0f, predictionSeconds);
            _replanInterval = Mathf.Max(.01f, replanSeconds);
            _goalHysteresis = Mathf.Max(0f, goalHysteresis);
            _stuckSeconds = Mathf.Max(.01f, stuckSeconds);
            _expansionLimit = Mathf.Clamp(maxExpandedNodes, 1, MAX_NODES);
            _configured = true;
        }

        /// <summary>角色切换、节点切换或退出托管时清理路线与卡住计时。</summary>
        public void Reset()
        {
            _pathCount = _pathIndex = 0;
            _hasGoal = _wasMoving = false;
            _replanRemaining = _stuckElapsed = 0f;
            _blockedDirection = Vector2.zero;
            LastExpandedNodes = 0;
        }

        /// <summary>输入/输出位置均为角色真实碰撞体中心，不是角色根节点。</summary>
        public CompanionNavigationResult2D Navigate(CompanionObstacleSnapshot[] hazards, int count, bool truncated,
            Rect bounds, Vector2 playerCenter, Vector2 extent, Vector2 currentVelocity,
            float maximumSpeed, float acceleration, Vector2 desiredGoal, float deltaTime)
        {
            if (!_configured || maximumSpeed <= 0f || truncated || count > MAX_HAZARDS ||
                count < 0 || (count > 0 && (hazards == null || count > hazards.Length)))
                return Wait(playerCenter, false, false);
            _hazards = hazards;
            _hazardCount = count;
            _candidateWords = (count + 31) / 32;
            _extent = new Vector2(Mathf.Max(0f, extent.x), Mathf.Max(0f, extent.y));
            if (bounds.width <= _extent.x * 2f || bounds.height <= _extent.y * 2f)
                return Wait(playerCenter, false, false);
            Vector2 target = CompanionThreatMath.ClampPoint(desiredGoal, bounds, _extent);
            bool changed = !_hasGoal || Vector2.Distance(target, _goal) > _goalHysteresis;
            if (changed)
            {
                _goal = target;
                _hasGoal = true;
                _pathCount = _pathIndex = 0;
                _replanRemaining = 0f;
                _stuckElapsed = 0f;
                _progressAnchor = playerCenter;
                _blockedDirection = Vector2.zero;
            }
            float dt = Mathf.Max(0f, deltaTime);
            _replanRemaining -= dt;
            if (Vector2.Distance(playerCenter, _progressAnchor) >= _cellSize * .5f)
            {
                _progressAnchor = playerCenter;
                _stuckElapsed = 0f;
            }
            else if (_wasMoving) _stuckElapsed += dt;
            bool stuck = _wasMoving && _stuckElapsed >= _stuckSeconds;
            if (stuck)
            {
                _blockedDirection = (_lastWaypoint - playerCenter).normalized;
                _replanRemaining = 0f;
                _stuckElapsed = 0f;
            }
            _speed = maximumSpeed;
            // 启动/转弯不能假定立即达到极速；更精确的首步电机预测由末端 Steering 执行。
            _startupDelay = acceleration > EPSILON
                ? Mathf.Max(0f, maximumSpeed - Vector2.Dot(currentVelocity, (_goal - playerCenter).normalized)) / acceleration
                : 0f;
            float arrival = Mathf.Max(.05f, _cellSize * .4f);
            if (Vector2.Distance(playerCenter, _goal) <= arrival &&
                IsSegmentSafe(playerCenter, playerCenter, 0f, _replanInterval, extent, _padding,
                    hazards, count, _prediction))
                return Wait(playerCenter, true, stuck);

            while (_pathIndex < _pathCount && Vector2.Distance(playerCenter, _path[_pathIndex]) <= arrival)
                _pathIndex++;
            bool nextSafe = _pathIndex < _pathCount && IsSegmentSafe(playerCenter, _path[_pathIndex], 0f,
                TravelDuration(playerCenter, _path[_pathIndex], true), extent, _padding, hazards, count, _prediction);
            bool waitingThreat = _pathIndex >= _pathCount && !IsSegmentSafe(playerCenter, playerCenter,
                0f, _replanInterval, extent, _padding, hazards, count, _prediction);
            if (_replanRemaining <= 0f || (_pathIndex < _pathCount && !nextSafe) || waitingThreat)
            {
                BuildRoute(bounds, playerCenter);
                _replanRemaining = _replanInterval;
            }
            if (_pathIndex >= _pathCount) return Wait(playerCenter, false, stuck);

            // 只在连续碰撞检查通过时略过折线拐点；不把八方向局部趋近当作寻路失败回退。
            int chosen = _pathIndex;
            for (int i = _pathIndex + 1; i < _pathCount; i++)
            {
                if (Vector2.Distance(playerCenter, _path[i]) > _cellSize * 4f) break;
                if (!IsSegmentSafe(playerCenter, _path[i], 0f, TravelDuration(playerCenter, _path[i], true),
                    extent, _padding, hazards, count, _prediction)) break;
                chosen = i;
            }
            if (!IsSegmentSafe(playerCenter, _path[chosen], 0f, TravelDuration(playerCenter, _path[chosen], true),
                extent, _padding, hazards, count, _prediction)) return Wait(playerCenter, false, stuck);
            _pathIndex = chosen;
            _lastWaypoint = _path[chosen];
            _wasMoving = true;
            return new CompanionNavigationResult2D(_lastWaypoint, true, false, false, stuck, RemainingPathCount);
        }

        /// <summary>诊断/只读可视化复制剩余路径；调用者提供缓冲，不创建数组。</summary>
        public int CopyPath(Vector2[] destination)
        {
            if (destination == null) return 0;
            int count = Mathf.Min(destination.Length, RemainingPathCount);
            for (int i = 0; i < count; i++) destination[i] = _path[_pathIndex + i];
            return count;
        }

        private CompanionNavigationResult2D Wait(Vector2 position, bool reachedGoal, bool stuck)
        {
            _wasMoving = false;
            _pathCount = _pathIndex = 0;
            return new CompanionNavigationResult2D(position, false, true, reachedGoal, stuck, 0);
        }

        private void BuildRoute(Rect bounds, Vector2 start)
        {
            _pathCount = _pathIndex = 0;
            LastExpandedNodes = 0;
            _start = start;
            if (IsSegmentSafe(start, _goal, 0f, TravelDuration(start, _goal, true), _extent, _padding,
                _hazards, _hazardCount, _prediction))
            {
                _path[0] = _goal;
                _pathCount = 1;
                return;
            }
            _origin = bounds.min + _extent;
            Vector2 size = bounds.size - _extent * 2f;
            _spacing = _cellSize;
            do
            {
                _columns = Mathf.Max(2, Mathf.CeilToInt(size.x / _spacing) + 1);
                _rows = Mathf.Max(2, Mathf.CeilToInt(size.y / _spacing) + 1);
                if (_columns * _rows <= MAX_NODES) break;
                _spacing *= 1.25f;
            } while (true);
            // 统一格距的末行夹在玩法边界内，不把宽屏背景当可移动区域。
            _startNode = ToNode(start);
            _goalNode = ToNode(_goal);
            int total = _columns * _rows;
            System.Array.Clear(_candidates, 0, total * WORDS_PER_NODE);
            for (int i = 0; i < total; i++)
            {
                _cost[i] = float.PositiveInfinity;
                _state[i] = 0;
                _parent[i] = -1;
                _heapPosition[i] = -1;
                // 密集障碍下未访问的格子可能很多，不为它们预计算坐标。
                _positionReady[i] = 0;
            }
            BuildCandidates();
            _heapCount = 0;
            _cost[_startNode] = 0f;
            _travelCost[_startNode] = 0f;
            _score[_startNode] = Vector2.Distance(start, _goal);
            PushOrUpdate(_startNode);
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            while (_heapCount > 0 && LastExpandedNodes < _expansionLimit)
            {
                int node = Pop();
                if (_state[node] == 2) continue;
                _state[node] = 2;
                LastExpandedNodes++;
                Vector2 from = Position(node, bounds);
                float distance = Vector2.Distance(from, _goal);
                if (node != _startNode && distance < bestDistance)
                { bestDistance = distance; best = node; }
                if (node == _goalNode && node != _startNode) { best = node; break; }
                int x = node % _columns, y = node / _columns;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows) continue;
                    int next = ny * _columns + nx;
                    if (_state[next] == 2) continue;
                    Vector2 to = Position(next, bounds);
                    float length = Vector2.Distance(from, to);
                    float cost = _cost[node] + length;
                    if (node == _startNode && Vector2.Dot((to - from).normalized, _blockedDirection) > .5f)
                        cost += _spacing;
                    if (cost >= _cost[next]) continue;
                    float startTime = node == _startNode ? 0f : _startupDelay + _travelCost[node] / _speed;
                    float duration = length / _speed + (node == _startNode ? _startupDelay : 0f);
                    if (!EdgeSafe(node, next, from, to, startTime, duration)) continue;
                    _cost[next] = cost;
                    _travelCost[next] = _travelCost[node] + length;
                    _parent[next] = node;
                    _score[next] = cost + Vector2.Distance(to, _goal);
                    PushOrUpdate(next);
                }
            }
            if (best < 0) return;
            // 不可达目标的最近安全点不是左右来回踱步的理由；安全时等待新快照/拆墙。
            // 若原地即将被移动泡覆盖，仍允许返回已搜索到的退避路线。
            if (best != _goalNode && bestDistance >= Vector2.Distance(start, _goal) - _spacing * .25f &&
                IsSegmentSafe(start, start, 0f, _replanInterval, _extent, _padding,
                    _hazards, _hazardCount, _prediction)) return;
            for (int node = best; node != _startNode && node >= 0 && _pathCount < MAX_NODES; node = _parent[node])
                _path[_pathCount++] = _positions[node];
            for (int i = 0, j = _pathCount - 1; i < j; i++, j--)
            { Vector2 p = _path[i]; _path[i] = _path[j]; _path[j] = p; }
        }

        private int ToNode(Vector2 p)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((p.x - _origin.x) / _spacing), 0, _columns - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((p.y - _origin.y) / _spacing), 0, _rows - 1);
            return y * _columns + x;
        }

        private Vector2 Position(int node, Rect bounds)
        {
            if (_positionReady[node] != 0) return _positions[node];
            // 同一规划中坐标不变；保留原计算次序和起终点优先级，只缓存实际访问的节点。
            Vector2 position;
            if (node == _startNode) position = _start;
            else if (node == _goalNode) position = _goal;
            else position = CompanionThreatMath.ClampPoint(_origin + new Vector2(node % _columns, node / _columns) * _spacing,
                bounds, _extent);
            _positions[node] = position;
            _positionReady[node] = 1;
            return position;
        }

        private float TravelDuration(Vector2 a, Vector2 b, bool startup) =>
            Vector2.Distance(a, b) / _speed + (startup ? _startupDelay : 0f);

        private void BuildCandidates()
        {
            // 每格记录整段预测扫过的候选泡；精确相交仍用圆与矩形，不用外接大圆封死狭窄口。
            for (int i = 0; i < _hazardCount; i++)
            {
                CompanionObstacleSnapshot h = _hazards[i];
                Vector2 end = h.Center + h.Velocity * _prediction;
                Vector2 margin = _extent + Vector2.one * (h.Radius + _padding + _spacing * 2f);
                Vector2 min = Vector2.Min(h.Center, end) - margin;
                Vector2 max = Vector2.Max(h.Center, end) + margin;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - _origin.x) / _spacing), 0, _columns - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - _origin.x) / _spacing), 0, _columns - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((min.y - _origin.y) / _spacing), 0, _rows - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((max.y - _origin.y) / _spacing), 0, _rows - 1);
                uint bit = 1u << (i % 32);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    _candidates[(y * _columns + x) * WORDS_PER_NODE + i / 32] |= bit;
            }
        }

        private bool EdgeSafe(int a, int b, Vector2 from, Vector2 to, float startTime, float duration)
        {
            int aOffset = a * WORDS_PER_NODE, bOffset = b * WORDS_PER_NODE;
            for (int word = 0; word < _candidateWords; word++)
            {
                uint bits = _candidates[aOffset + word] | _candidates[bOffset + word];
                while (bits != 0)
                {
                    // 仍按原危险体索引升序检查，只跳过零位；不会改变碰撞公式或第一个命中的对象。
                    int bit = LowestSetBitIndex(bits);
                    if (IntersectsHazard(from, to, startTime, duration, _extent,
                        _padding, _hazards[word * 32 + bit], _prediction)) return false;
                    bits &= bits - 1u;
                }
            }
            return true;
        }

        private static int LowestSetBitIndex(uint bits)
        {
            // 调用方保证非零；二分跳过空区间，兼容没有硬件 bit-scan API 的 Unity 后端。
            int index = 0;
            if ((bits & 0xFFFFu) == 0) { bits >>= 16; index += 16; }
            if ((bits & 0xFFu) == 0) { bits >>= 8; index += 8; }
            if ((bits & 0xFu) == 0) { bits >>= 4; index += 4; }
            if ((bits & 0x3u) == 0) { bits >>= 2; index += 2; }
            if ((bits & 0x1u) == 0) index++;
            return index;
        }

        /// <summary>连续检测线段轨迹对移动圆泡与角色 AABB 的 Minkowski 和；可供末端转向器复用。</summary>
        public static bool IsSegmentSafe(Vector2 from, Vector2 to, float startTime, float duration,
            Vector2 extent, float padding, CompanionObstacleSnapshot[] hazards, int count,
            float predictionSeconds = float.PositiveInfinity)
        {
            if (count < 0 || (count > 0 && (hazards == null || count > hazards.Length))) return false;
            for (int i = 0; i < count; i++)
                if (IntersectsHazard(from, to, startTime, duration, extent, padding, hazards[i], predictionSeconds))
                    return false;
            return true;
        }

        /// <summary>单危险体版本；转向器可据此区分已有重叠的脱离与新碰撞。</summary>
        public static bool IsObstacleSegmentSafe(Vector2 from, Vector2 to, float startTime, float duration,
            Vector2 extent, float padding, CompanionObstacleSnapshot hazard,
            float predictionSeconds = float.PositiveInfinity) =>
            !IntersectsHazard(from, to, startTime, duration, extent, padding, hazard, predictionSeconds);

        private static bool IntersectsHazard(Vector2 from, Vector2 to, float startTime, float duration,
            Vector2 extent, float padding, CompanionObstacleSnapshot hazard, float horizon)
        {
            startTime = Mathf.Max(0f, startTime);
            duration = Mathf.Max(0f, duration);
            horizon = Mathf.Max(0f, horizon);
            float endTime = startTime + duration;
            float radius = Mathf.Max(0f, hazard.Radius) + Mathf.Max(0f, padding);
            if (duration > EPSILON && startTime < horizon && endTime > horizon)
            {
                Vector2 split = Vector2.Lerp(from, to, (horizon - startTime) / duration);
                return SweptBoxCircle(from - hazard.Center - hazard.Velocity * startTime,
                           split - hazard.Center - hazard.Velocity * horizon, extent, radius) ||
                       SweptBoxCircle(split - hazard.Center - hazard.Velocity * horizon,
                           to - hazard.Center - hazard.Velocity * horizon, extent, radius);
            }
            // 常见短步不切预测窗口。保留原相对端点的运算次序，远离 X 范围时连 Y 端点也无需计算。
            // 这里只提前执行原 SweptBoxCircle 的矩形早退；圆角/切线仍交给完全相同的精确检测。
            float sampleStart = Mathf.Min(startTime, horizon), sampleEnd = Mathf.Min(endTime, horizon);
            float ax = from.x - hazard.Center.x - hazard.Velocity.x * sampleStart;
            float bx = to.x - hazard.Center.x - hazard.Velocity.x * sampleEnd;
            float limitX = extent.x + radius;
            if (Mathf.Min(ax, bx) > limitX || Mathf.Max(ax, bx) < -limitX) return false;
            float ay = from.y - hazard.Center.y - hazard.Velocity.y * sampleStart;
            float by = to.y - hazard.Center.y - hazard.Velocity.y * sampleEnd;
            float limitY = extent.y + radius;
            if (Mathf.Min(ay, by) > limitY || Mathf.Max(ay, by) < -limitY) return false;
            return SweptBoxCircleNarrow(new Vector2(ax, ay), new Vector2(bx, by), extent, radius);
        }

        private static bool SweptBoxCircle(Vector2 a, Vector2 b, Vector2 extent, float radius)
        {
            Vector2 limit = extent + Vector2.one * radius;
            if (Mathf.Min(a.x, b.x) > limit.x || Mathf.Max(a.x, b.x) < -limit.x ||
                Mathf.Min(a.y, b.y) > limit.y || Mathf.Max(a.y, b.y) < -limit.y) return false;
            return SweptBoxCircleNarrow(a, b, extent, radius);
        }

        private static bool SweptBoxCircleNarrow(Vector2 a, Vector2 b, Vector2 extent, float radius)
        {
            Vector2 d = b - a;
            float enter = 0f, exit = 1f;
            if (Slab(a.x, d.x, extent.x, ref enter, ref exit) &&
                Slab(a.y, d.y, extent.y, ref enter, ref exit)) return true;
            float distance = Mathf.Min(PointBoxDistanceSquared(a, extent), PointBoxDistanceSquared(b, extent));
            distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(-extent.x, -extent.y), a, b));
            distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(-extent.x, extent.y), a, b));
            distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(extent.x, -extent.y), a, b));
            distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(extent.x, extent.y), a, b));
            return distance <= radius * radius;
        }

        private static bool Slab(float origin, float delta, float extent, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) <= EPSILON) return origin >= -extent && origin <= extent;
            float a = (-extent - origin) / delta, b = (extent - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
            return enter <= exit;
        }

        private static float PointBoxDistanceSquared(Vector2 p, Vector2 extent)
        {
            float x = Mathf.Max(0f, Mathf.Abs(p.x) - extent.x);
            float y = Mathf.Max(0f, Mathf.Abs(p.y) - extent.y);
            return x * x + y * y;
        }

        private static float PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude > EPSILON ? Mathf.Clamp01(Vector2.Dot(point - a, d) / d.sqrMagnitude) : 0f;
            return (point - a - d * t).sqrMagnitude;
        }

        private void PushOrUpdate(int node)
        {
            int index = _heapPosition[node];
            if (index < 0) { index = _heapCount++; _heap[index] = node; _heapPosition[node] = index; _state[node] = 1; }
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_score[_heap[parent]] <= _score[node]) break;
                _heap[index] = _heap[parent]; _heapPosition[_heap[index]] = index;
                index = parent;
            }
            _heap[index] = node; _heapPosition[node] = index;
        }

        private int Pop()
        {
            int result = _heap[0], replacement = _heap[--_heapCount];
            _heapPosition[result] = -1;
            if (_heapCount == 0) return result;
            int index = 0;
            while (index * 2 + 1 < _heapCount)
            {
                int child = index * 2 + 1;
                if (child + 1 < _heapCount && _score[_heap[child + 1]] < _score[_heap[child]]) child++;
                if (_score[replacement] <= _score[_heap[child]]) break;
                _heap[index] = _heap[child]; _heapPosition[_heap[index]] = index;
                index = child;
            }
            _heap[index] = replacement; _heapPosition[replacement] = index;
            return result;
        }
    }
}
