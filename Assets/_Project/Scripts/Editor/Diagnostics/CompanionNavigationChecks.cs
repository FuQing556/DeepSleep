using System;
using DeepSleep.Runtime.Players.Companion;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>纯路线与连续碰撞数学检查；不创建实体，不读取生成器保护路线，不改场景。</summary>
    public static class CompanionNavigationChecks
    {
        [MenuItem("DeepSleep/验证/同伴动态障碍导航")]
        private static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            Vector2 extent = new Vector2(.195f, .45f);
            var one = new[] { new CompanionObstacleSnapshot(Vector2.zero, .62f, Vector2.zero) };
            Check(!CompanionNavigation2D.IsSegmentSafe(new Vector2(-2f, 0f), new Vector2(2f, 0f),
                0f, .1f, extent, .06f, one, 1), "Fast movement tunnelled through a bubble.");
            Check(CompanionNavigation2D.IsSegmentSafe(new Vector2(.7f, .95f), new Vector2(.7f, .95f),
                0f, 1f, extent, .06f, one, 1), "Rounded-box corner was incorrectly treated as solid AABB.");
            one[0] = new CompanionObstacleSnapshot(new Vector2(0f, 2f), .62f, Vector2.down * 2f);
            Check(!CompanionNavigation2D.IsSegmentSafe(Vector2.zero, Vector2.zero, 0f, 1f,
                extent, .06f, one, 1), "Waiting ignored an incoming falling bubble.");
            one[0] = new CompanionObstacleSnapshot(new Vector2(0f, 2f), .62f, Vector2.up * 2f);
            Check(CompanionNavigation2D.IsSegmentSafe(Vector2.zero, Vector2.zero, 0f, 1f,
                extent, .06f, one, 1), "Receding bubble incorrectly prevented safe waiting.");
            one[0] = new CompanionObstacleSnapshot(new Vector2(0f, 2f), .62f, Vector2.down * 2f);
            Check(!CompanionNavigation2D.IsSegmentSafe(new Vector2(-2f, 0f), new Vector2(2f, 0f),
                0f, 1f, extent, .06f, one, 1, .5f), "Prediction horizon split missed a collision.");

            var navigation = Create();
            var path = new Vector2[4096];
            var wall = new CompanionObstacleSnapshot[7];
            for (int i = 0; i < wall.Length; i++)
                wall[i] = new CompanionObstacleSnapshot(new Vector2(0f, -2.4f + i * .8f), .5f, Vector2.zero);
            Vector2 start = new Vector2(-3f, 0f), goal = new Vector2(3f, 0f);
            Rect bounds = new Rect(-5f, -4.5f, 10f, 9f);
            var result = navigation.Navigate(wall, wall.Length, false, bounds, start, extent,
                Vector2.zero, 6.5f, 30f, goal, .125f);
            Check(result.HasPath && !result.ShouldWait, "Finite wall did not produce a side route.");
            int count = navigation.CopyPath(path);
            Check(count > 2 && Vector2.Distance(path[count - 1], goal) < .001f, "Planner returned only a local nudge, not a full route.");
            bool bends = false;
            Vector2 previous = start;
            for (int i = 0; i < count; i++)
            {
                Check(CompanionNavigation2D.IsSegmentSafe(previous, path[i], 0f, .1f, extent, .06f, wall, wall.Length),
                    "Static wall route intersects a bubble.");
                bends |= Mathf.Abs(path[i].y) > 3.3f;
                previous = path[i];
            }
            Check(bends, "Route failed to go around wall endpoint.");
            Check(navigation.LastExpandedNodes <= 768, "Expansion budget exceeded.");

            // 封顶 U 形，朝目标直接靠近会死路；必须先从底部后退，再侧绕到上方。
            var u = new CompanionObstacleSnapshot[11];
            for (int i = 0; i < 5; i++)
            {
                u[i] = new CompanionObstacleSnapshot(new Vector2(-.85f, i * .65f), .34f, Vector2.zero);
                u[i + 5] = new CompanionObstacleSnapshot(new Vector2(.85f, i * .65f), .34f, Vector2.zero);
            }
            u[10] = new CompanionObstacleSnapshot(new Vector2(0f, 2.6f), .52f, Vector2.zero);
            navigation.Reset();
            start = new Vector2(0f, 1.1f); goal = new Vector2(0f, 3.8f);
            bounds = new Rect(-4f, -3f, 8f, 7.5f);
            result = navigation.Navigate(u, u.Length, false, bounds, start, extent,
                Vector2.zero, 6.5f, 30f, goal, .125f);
            Check(result.HasPath && result.Waypoint.y < start.y, "Dead-end route did not backtrack.");
            count = navigation.CopyPath(path);
            Check(count > 2 && Vector2.Distance(path[count - 1], goal) < .001f, "U-shaped route did not reach goal.");

            // 一个不能通过的完整分隔墙：可以靠近安全等待点，但永远不能直线穿越。
            var partition = new CompanionObstacleSnapshot[15];
            for (int i = 0; i < partition.Length; i++)
                partition[i] = new CompanionObstacleSnapshot(new Vector2(0f, -5.6f + i * .8f), .6f, Vector2.zero);
            navigation.Reset();
            start = new Vector2(-2f, 0f); goal = new Vector2(2f, 0f);
            bounds = new Rect(-4f, -4f, 8f, 8f);
            bool waited = false;
            for (int step = 0; step < 80; step++)
            {
                result = navigation.Navigate(partition, partition.Length, false, bounds, start, extent,
                    Vector2.zero, 2f, 30f, goal, .125f);
                Vector2 next = result.ShouldWait ? start : Vector2.MoveTowards(start, result.Waypoint, .25f);
                Check(CompanionNavigation2D.IsSegmentSafe(start, next, 0f, .125f, extent, .06f, partition, partition.Length),
                    "No-route fallback crossed a wall.");
                waited |= result.ShouldWait;
                start = next;
            }
            Check(start.x < -.8f && waited, "Blocked route neither retained safe side nor waited.");

            // 移动快照闭环：只按返回路点走，不读取保护通路；每一步用连续碰撞独立复核。
            var falling = new CompanionObstacleSnapshot[4];
            for (int i = 0; i < falling.Length; i++)
                falling[i] = new CompanionObstacleSnapshot(new Vector2(0f, i * .9f), .42f, Vector2.down * .5f);
            navigation.Reset();
            start = new Vector2(-3f, -1f); goal = new Vector2(3f, 1f);
            bounds = new Rect(-5f, -4f, 10f, 8f);
            Vector2 velocity = Vector2.zero;
            bool reached = false;
            for (int step = 0; step < 240; step++)
            {
                result = navigation.Navigate(falling, falling.Length, false, bounds, start, extent,
                    velocity, 2f, 30f, goal, .05f);
                Vector2 next = result.ShouldWait ? start : Vector2.MoveTowards(start, result.Waypoint, .1f);
                Check(CompanionNavigation2D.IsSegmentSafe(start, next, 0f, .05f, extent, .06f, falling, falling.Length),
                    "Dynamic route produced an unsafe executed segment.");
                velocity = (next - start) / .05f;
                start = next;
                for (int i = 0; i < falling.Length; i++)
                    falling[i] = new CompanionObstacleSnapshot(falling[i].Center + falling[i].Velocity * .05f,
                        falling[i].Radius, falling[i].Velocity);
                Check(navigation.LastExpandedNodes <= 768, "Dynamic route exceeded CPU expansion budget.");
                if (Vector2.Distance(start, goal) < .15f) { reached = true; break; }
            }
            Check(reached, "Dynamic route did not eventually reach goal.");
            result = navigation.Navigate(falling, falling.Length, true, bounds, start, extent,
                Vector2.zero, 2f, 30f, goal, .125f);
            Check(result.ShouldWait && !result.HasPath, "Truncated perception fabricated a safe route.");
            navigation.Reset();
            Check(navigation.RemainingPathCount == 0, "Reset retained a stale route.");
            return "Navigation continuous collision, rounded corner, incoming/receding/prediction split, full side detour, " +
                "U-shaped backtrack, blocked wait, dynamic snapshot loop, overflow, reset and bounded expansion checks passed. " +
                "Synthetic math only; real motor/maze/mobile acceptance remains separate.";
        }

        private static CompanionNavigation2D Create()
        {
            var navigation = new CompanionNavigation2D();
            navigation.Configure(.36f, .06f, 1.2f, .35f, .35f, 1.2f, 768);
            return navigation;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
