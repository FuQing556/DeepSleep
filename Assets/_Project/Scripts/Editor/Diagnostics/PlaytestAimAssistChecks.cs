using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class PlaytestAimAssistChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int count = 0;
            void Check(bool ok, string reason) { count++; if (!ok) throw new Exception(reason); }
            foreach (string level in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + level + ".unity");
                try
                {
                    var laser = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HarnessTerminalLaserController>(true)).Single();
                    Check(laser.Perception != null && laser.Perception.gameObject.scene == scene, "Per-scene target registry survives reload");
                    Check(laser.ReviveChannel != null, "Auto-fire yields to rescue channel");
                    Check(laser.Config.AssistedSelectionHeightFraction > 0 && laser.Config.AutomaticScanInterval > 0, "Formal assist parameters");
                    if (level == "World02_2066") Check(laser.Permissions != null && laser.EnergyBalls.Length == 2, "Claude mechanics explicitly bound");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            // 隔离到遥远坐标并严格过滤这三个临时对象，不查询或修改正式敌人。
            var roots = new GameObject[3];
            try
            {
                var colliders = new Collider2D[3];
                Vector2 origin = new Vector2(10000, 10000);
                for (int i = 0; i < roots.Length; i++)
                {
                    roots[i] = new GameObject("AimAssistCheck" + i) { hideFlags = HideFlags.HideAndDontSave };
                    var shape = roots[i].AddComponent<CircleCollider2D>(); shape.radius = .1f; shape.isTrigger = true;
                    roots[i].transform.position = origin + new Vector2(.3f + i * .3f, 0);
                    colliders[i] = shape;
                }
                Physics2D.SyncTransforms();
                var type = typeof(HarnessTerminalLaserController).Assembly.GetType("DeepSleep.Runtime.Combat.Targeting.ManualTargetFinder2D", true);
                var finder = Activator.CreateInstance(type, new object[] { (LayerMask)(-1), 16 });
                var method = type.GetMethods().Single(m => m.Name == "TryFindNearPoint" && m.GetParameters().Length == 7);
                Predicate<Collider2D> eligible = c => Array.IndexOf(colliders, c) >= 0;
                Func<Collider2D, int> priority = c => c == colliders[2] ? 1 : 0;
                Collider2D Query(Vector2 point, float radius, Func<Collider2D, int> rank)
                {
                    object[] args = { point, origin, radius, 5f, eligible, null, rank };
                    method.Invoke(finder, args); return args[5] as Collider2D;
                }
                Check(Query(origin, 1, priority) == colliders[2], "Missed click favors urgent target inside circle");
                Check(Query(colliders[0].transform.position, 1, priority) == colliders[0], "Exact hit wins over higher threat");
                Check(Query(origin, 1, null) == colliders[0], "Equal threat chooses nearest to click");
                Check(Query(origin, 0, priority) == null, "Assist off does not snap missed click");
                Check(Query(colliders[1].transform.position, 0, priority) == colliders[1], "Assist off still accepts exact hit");
            }
            finally { foreach (var root in roots) if (root != null) UnityEngine.Object.DestroyImmediate(root); }
            return count + " assist selection/binding checks passed";
        }
    }
}
