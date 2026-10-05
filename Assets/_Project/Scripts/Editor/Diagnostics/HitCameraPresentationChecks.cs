using System;
using System.Reflection;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.World.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>隔离预览场景中的镜头/投影回归；不装配或保存正式场景。</summary>
    public static class HitCameraPresentationChecks
    {
        [MenuItem("DeepSleep/验证/受击镜头与稳定瞄准")]
        private static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Run HitCameraPresentationChecks outside Play Mode.");

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject("HitCameraProjectionCheck");
                SceneManager.MoveGameObjectToScene(go, scene);
                var camera = go.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographicSize = 5.4f;
                var assignment = go.AddComponent<PlayerControlAssignment>();
                var motion = go.AddComponent<CameraHorizontalLookAhead2D>();
                Set(motion, "_assignment", assignment);
                Set(motion, "_hitAmplitude", .06f);

                int count = 0;
                foreach (bool orthographic in new[] { true, false })
                foreach (float aspect in new[] { 16f / 9f, 20f / 9f, 22f / 9f, 3f, 32f / 9f })
                foreach (float lookAhead in new[] { -.45f, 0f, .45f })
                foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up,
                    Vector2.down, new Vector2(1f, 1f) })
                {
                    camera.orthographic = orthographic;
                    camera.pixelRect = new Rect(0f, 0f, 720f * aspect, 720f);
                    camera.aspect = aspect;
                    motion.ResetHit();
                    // 前视只作为已有镜头中心，断言受击绝不改动这个输入基准。
                    var stable = new Vector3(lookAhead, 0f, -10f);
                    camera.transform.position = stable;
                    Set(motion, "_restPosition", stable);
                    Set(motion, "_currentOffset", 0f);
                    Set(motion, "_smoothVelocity", 0f);
                    motion.ShakeStrength = 1f;

                    var points = new[] { new Vector2(.1f, .1f), new Vector2(.5f, .5f),
                        new Vector2(.9f, .9f) };
                    var baseline = new Vector3[points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] = Vector2.Scale(points[i], new Vector2(720f * aspect, 720f));
                        baseline[i] = Intersect(camera.ScreenPointToRay(points[i]));
                    }

                    motion.PlayHit(direction);
                    for (int step = 0; step < 40; step++)
                    {
                        Step(motion, .005f);
                        Check(motion.PresentationOffset.magnitude <= .06001f, "Shake exceeded .06 world units.");
                        Check(Mathf.Abs(camera.transform.position.x) <= .51001f &&
                            Mathf.Abs(camera.transform.position.y) <= .06001f, "Combined camera travel exceeded budget.");
                        Check(Vector3.Distance(motion.StablePosition, stable) < .0001f, "Stable camera drifted.");
                        Check(camera.transform.rotation == Quaternion.identity, "Shake rotated camera.");
                        for (int i = 0; i < points.Length; i++)
                        {
                            Vector3 actual = Intersect(motion.ScreenPointToStableRay(camera, points[i]));
                            Check(Vector3.Distance(actual, baseline[i]) < .0002f, "Shake changed world aim.");
                            count++;
                        }
                        // 相同命中每帧重入也不能放大；另一段测试再单独验证正常结束。
                        if (step < 10) motion.PlayHit(direction);
                    }
                    motion.ResetHit();
                    motion.PlayHit(direction);
                    Step(motion, .02f);
                    Vector3 paused = camera.transform.position;
                    Step(motion, 0f);
                    Check(Vector3.Distance(camera.transform.position, paused) < .00001f, "Pause advanced shake.");
                    motion.ShakeStrength = 0f;
                    Check(motion.PresentationOffset == Vector3.zero &&
                        Vector3.Distance(camera.transform.position, stable) < .0001f, "Zero strength left residual shake.");
                    motion.PlayHit(direction);
                    Step(motion, .01f);
                    Check(motion.PresentationOffset == Vector3.zero, "Disabled shake restarted.");
                    motion.ShakeStrength = 1f;
                    motion.PlayHit(direction);
                    Step(motion, .17f);
                    Check(motion.PresentationOffset == Vector3.zero, "Shake did not settle at duration.");
                }
                // 再与无震动基准相机同步推进真实 SmoothDamp，验证两者前视轨迹完全一致。
                var referenceObject = new GameObject("UnshakenLookAheadReference");
                SceneManager.MoveGameObjectToScene(referenceObject, scene);
                var referenceCamera = referenceObject.AddComponent<Camera>();
                referenceCamera.scene = scene;
                referenceCamera.enabled = false;
                referenceCamera.orthographic = camera.orthographic = true;
                referenceCamera.orthographicSize = camera.orthographicSize;
                referenceCamera.pixelRect = camera.pixelRect;
                referenceCamera.aspect = camera.aspect;
                var referenceMotion = referenceObject.AddComponent<CameraHorizontalLookAhead2D>();
                Set(referenceMotion, "_assignment", assignment);
                var origin = new Vector3(0f, 0f, -10f);
                foreach (float startOffset in new[] { -.45f, .45f })
                {
                    motion.ResetHit();
                    referenceMotion.ResetHit();
                    Set(motion, "_restPosition", origin);
                    Set(referenceMotion, "_restPosition", origin);
                    Set(motion, "_currentOffset", startOffset);
                    Set(referenceMotion, "_currentOffset", startOffset);
                    Set(motion, "_smoothVelocity", 0f);
                    Set(referenceMotion, "_smoothVelocity", 0f);
                    camera.transform.position = referenceCamera.transform.position = origin + Vector3.right * startOffset;
                    motion.PlayHit(new Vector2(1f, 1f));
                    for (int step = 0; step < 120; step++)
                    {
                        Step(motion, .005f);
                        Step(referenceMotion, .005f);
                        Check(Vector3.Distance(motion.StablePosition, referenceCamera.transform.position) < .0001f,
                            "Hit changed look-ahead trajectory.");
                        Vector2 point = camera.pixelRect.center;
                        Check(Vector3.Distance(Intersect(motion.ScreenPointToStableRay(camera, point)),
                            Intersect(referenceCamera.ScreenPointToRay(point))) < .0002f, "Moving look-ahead changed stable aim.");
                    }
                }
                return count + " stable-aim samples + 240 moving look-ahead comparisons passed; repeated-hit cap, pause, reset, disable and duration passed. " +
                    PanoramaCoverageChecks.Run();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Vector3 Intersect(Ray ray)
        {
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) throw new InvalidOperationException("Aim ray missed gameplay plane.");
            return ray.GetPoint(distance);
        }

        private static void Step(CameraHorizontalLookAhead2D motion, float deltaTime) =>
            typeof(CameraHorizontalLookAhead2D).GetMethod("UpdatePresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(motion, new object[] { deltaTime });

        private static void Set(CameraHorizontalLookAhead2D motion, string name, object value) =>
            typeof(CameraHorizontalLookAhead2D).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(motion, value);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
