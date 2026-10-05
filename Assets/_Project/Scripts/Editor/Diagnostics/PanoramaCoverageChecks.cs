using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class PanoramaCoverageChecks
    {
        public const string ProductionRoot = "docs/ArtProduction/20260915_DuskPanorama";

        public static string RenderComposition()
        {
            string output = Path.Combine(ProductionRoot, "composition_riverbank_v02");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenPreviewScene(DeepSleep.Editor.Setup.DuskPanoramaInstaller.ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
                    canvas.gameObject.SetActive(false);
                var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.orthographic);
                camera.scene = scene;
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta;
                var layer = roots.SelectMany(r => r.GetComponentsInChildren<FinitePanoramaLayer2D>(true)).Single();
                var encounter = roots.SelectMany(r => r.GetComponentsInChildren<DeepSleep.Runtime.Combat.Encounters.Doubao.DoubaoWordWallEncounter2D>(true)).Single();
                var so = new SerializedObject(encounter);
                var anchor = (Transform)so.FindProperty("_bossAnchor").objectReferenceValue;
                encounter.Boss.transform.position = anchor.position;
                encounter.Boss.GetComponent<SpriteRenderer>().enabled = true;
                foreach (int width in new[] {1280, 1760})
                foreach (float x in new[] {-.45f, 0f, .45f})
                {
                    camera.transform.position = new Vector3(x, 0, -10);
                    var rt = new RenderTexture(width, 720, 24);
                    Texture2D capture = null;
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = rt;
                        camera.aspect = width / 720f;
                        layer.FitNow();
                        camera.Render();
                        RenderTexture.active = rt;
                        capture = new Texture2D(width, 720, TextureFormat.RGB24, false);
                        capture.ReadPixels(new Rect(0, 0, width, 720), 0, 0);
                        capture.Apply();
                        string side = x < 0 ? "left" : x > 0 ? "right" : "center";
                        File.WriteAllBytes(Path.Combine(output, width + "x720_" + side + ".png"), capture.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = previous;
                        if(capture != null) UnityEngine.Object.DestroyImmediate(capture);
                        rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                    }
                }
                return "6 composition previews (scene sprites, no gameplay simulation or UI) written.";
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static string CheckSafeAreas()
        {
            int count = 0;
            foreach (var screen in new[] {new Vector2Int(1920,1080),new Vector2Int(2640,1080)})
            foreach(float inset in new[]{0f,80f,140f})
            foreach(bool left in new[]{true,false})
            {
                var safe = new Rect(left ? inset : 0, 20, screen.x-inset, screen.y-20);
                var centered = DeepSleep.Runtime.UI.Common.SafeAreaRectFitter.CalculateArea(safe,screen,true);
                var touch = DeepSleep.Runtime.UI.Common.SafeAreaRectFitter.CalculateArea(safe,screen,false);
                if(Vector2.Distance(centered.center,new Vector2(screen.x,screen.y)*.5f)>.01f ||
                    centered.xMin<safe.xMin || centered.xMax>safe.xMax ||
                    centered.yMin<safe.yMin || centered.yMax>safe.yMax || touch!=safe)
                    throw new Exception("Safe-area centering regression");
                count++;
            }
            return count+" symmetric-menu / unchanged-touch safe area cases passed.";
        }

        public static string CheckInstalledUi()
        {
            int count = 0;
            foreach (string path in new[] {"Assets/Scenes/Gameplay_Prototype.unity",DeepSleep.Editor.Setup.DuskPanoramaInstaller.ScenePath})
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var menu = roots.Single(r => r.name == "UI_NetworkSession");
                    var safe = menu.GetComponentsInChildren<DeepSleep.Runtime.UI.Common.SafeAreaRectFitter>(true).Single();
                    if(!new SerializedObject(safe).FindProperty("_symmetricInsets").boolValue)
                        throw new Exception("Menu centering not installed");
                    var touch = roots.Single(r => r.name == "UI_TouchControls");
                    if(touch.GetComponentsInChildren<DeepSleep.Runtime.UI.Common.SafeAreaRectFitter>(true)
                        .Any(f => new SerializedObject(f).FindProperty("_symmetricInsets").boolValue))
                        throw new Exception("Touch layout unexpectedly changed");
                    var confirm = menu.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="ExitConfirmation");
                    if(confirm.GetComponentInChildren<DeepSleep.Runtime.UI.Common.FullScreenBackdrop>(true)==null)
                        throw new Exception("Missing existing fullscreen confirmation backdrop");
                    // 用场景内真实 RectTransform 链路测试左右不对称安全区，避免只验证数学函数。
                    var canvas = menu.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    var scaler = menu.GetComponent<UnityEngine.UI.CanvasScaler>();
                    if(scaler!=null) scaler.enabled=false;
                    var canvasRect = (RectTransform)canvas.transform;
                    canvasRect.localScale = Vector3.one;
                    canvasRect.sizeDelta = new Vector2(2640,1080);
                    var target = (RectTransform)new SerializedObject(safe).FindProperty("_target").objectReferenceValue;
                    foreach(bool left in new[]{true,false})
                    {
                        Rect area = DeepSleep.Runtime.UI.Common.SafeAreaRectFitter.CalculateArea(
                            new Rect(left?140:0,0,2500,1080),new Vector2Int(2640,1080),true);
                        target.anchorMin = new Vector2(area.xMin/2640,area.yMin/1080);
                        target.anchorMax = new Vector2(area.xMax/2640,area.yMax/1080);
                        target.offsetMin=target.offsetMax=Vector2.zero;
                        target.ForceUpdateRectTransforms();
                        confirm.ForceUpdateRectTransforms();
                        var local = canvasRect.InverseTransformPoint(confirm.TransformPoint(confirm.rect.center));
                        if(Vector2.Distance(local,canvasRect.rect.center)>.1f)
                            throw new Exception("Installed confirmation is not centered: "+local);
                        count++;
                    }
                }
                finally {EditorSceneManager.ClosePreviewScene(scene);}
            }
            return count+" real scene confirmation hierarchy cases passed; touch policy preserved.";
        }

        public static string Run()
        {
            int checks = 0;
            foreach (float aspect in new[] { 16f / 9f, 20f / 9f, 22f / 9f, 3f, 32f / 9f })
            foreach (float height in new[] { 10.8f, 12f })
            // 0.45 前视 + 0.06 受击预算；覆盖纵向受击，不能只测原先的一维平移。
            foreach (float x in new[] { -2f, -0.51f, -0.45f, 0f, 0.45f, 0.51f, 2f })
            foreach (float y in new[] { -.06f, 0f, .06f })
            {
                Vector2 view = new Vector2(height * aspect, height);
                Vector2 source = new Vector2(3f, 1f);
                float scale = FinitePanoramaLayer2D.CalculateScale(source, view,
                    10.8f, 22f / 9f, 0.51f, 0.2f);
                Vector2 camera = new Vector2(x, y);
                Vector2 half = source * scale * 0.5f;
                Vector2 center = FinitePanoramaLayer2D.ClampCenter(camera * .15f,
                    camera, half, view * .5f, .2f);
                Vector2 clearance = half - view * .5f - new Vector2(
                    Mathf.Abs(center.x - camera.x), Mathf.Abs(center.y - camera.y));
                if (clearance.x < .1999f || clearance.y < .1999f)
                    throw new Exception("Panorama exposed edge: " + aspect + " / " + x);
                checks++;
            }
            return checks + " coverage cases passed; math only, not device acceptance.";
        }

        // 在隔离的 PreviewScene 中用真实正交相机渲染，不改当前场景或原图。
        public static string RenderPreviews()
        {
            string output = Path.Combine(ProductionRoot, "previews");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            Texture2D texture = null;
            Sprite sprite = null;
            Material material = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(
                    ProductionRoot, "raw/BG_W01_DuskRiver_Panorama_v01.png"))))
                    throw new Exception("Cannot load panorama");
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100f);
                var cameraObject = new GameObject("PanoramaPreviewCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 5.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta;
                var root = new GameObject("CandidatePanorama");
                SceneManager.MoveGameObjectToScene(root, scene);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new Exception("Sprite unlit shader missing");
                material = new Material(shader);
                renderer.sharedMaterial = material;
                var layer = root.AddComponent<FinitePanoramaLayer2D>();
                var so = new SerializedObject(layer);
                so.FindProperty("_camera").objectReferenceValue = camera;
                so.FindProperty("_renderer").objectReferenceValue = renderer;
                so.ApplyModifiedPropertiesWithoutUndo();
                int count = 0;
                foreach (int width in new[] { 1280, 1600, 1760 })
                foreach (float x in new[] { -.45f, 0f, .45f })
                {
                    camera.transform.position = new Vector3(x, 0f, -10f);
                    var target = new RenderTexture(width, 720, 24);
                    Texture2D capture = null;
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = target;
                        camera.aspect = width / 720f;
                        layer.FitNow();
                        camera.Render();
                        RenderTexture.active = target;
                        capture = new Texture2D(width, 720, TextureFormat.RGB24, false);
                        capture.ReadPixels(new Rect(0, 0, width, 720), 0, 0);
                        capture.Apply();
                        string side = x < 0 ? "left" : x > 0 ? "right" : "center";
                        File.WriteAllBytes(Path.Combine(output, width + "x720_" + side + ".png"),
                            capture.EncodeToPNG());
                        count++;
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = previous;
                        if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                        target.Release();
                        UnityEngine.Object.DestroyImmediate(target);
                    }
                }
                return count + " isolated camera previews written to " + output;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
