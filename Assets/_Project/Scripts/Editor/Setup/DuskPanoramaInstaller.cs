using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.World.Scrolling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class DuskPanoramaInstaller
    {
        public const string ScenePath = "Assets/Scenes/World01_EarlyInternet.unity";
        public const string Panorama = "Assets/_Project/Art/Backgrounds/BG_W01_DuskRiver_Panorama_v01.png";
        public const string Foreground = "Assets/_Project/Art/Foregrounds/World01/FG_W01_Riverbanks_v02.png";

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            Import(Panorama, false);
            Import(Foreground, true);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("World01 has unsaved scene edits");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Camera camera = All<Camera>(scene).Single(c => c.orthographic);
                foreach (var loop in All<LoopingBackgroundLayer2D>(scene))
                {
                    loop.enabled = false;
                    foreach (var sr in loop.GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
                }
                GameObject panorama = Root(scene, "World01_FinitePanorama");
                var renderer = panorama.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = panorama.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Panorama);
                renderer.sortingLayerName = "Background";
                renderer.sortingOrder = -30;
                var layer = panorama.GetComponent<FinitePanoramaLayer2D>();
                if (layer == null) layer = panorama.AddComponent<FinitePanoramaLayer2D>();
                var so = new SerializedObject(layer);
                so.FindProperty("_camera").objectReferenceValue = camera;
                so.FindProperty("_renderer").objectReferenceValue = renderer;
                so.ApplyModifiedPropertiesWithoutUndo();
                layer.FitNow();

                // 保留原根与锚点引用，只替换视觉；不重建遭遇、对象池或角色碰撞体。
                var old = scene.GetRootGameObjects().Single(r => r.name == "World01_ForegroundCity");
                old.GetComponent<SpriteRenderer>().enabled = false;
                var oldLock = old.GetComponent<CameraLockedSpriteLayer2D>();
                if (oldLock != null) oldLock.enabled = false;
                old.transform.position = Vector3.zero;
                old.transform.localScale = Vector3.one;
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Foreground).OfType<Sprite>().ToArray();
                foreach (string rejected in new[] {"LeftCluster", "RightCluster"})
                {
                    var previous = old.transform.Find(rejected);
                    if (previous != null) previous.gameObject.SetActive(false);
                }
                Cluster(old.transform, "LeftBank", sprites.Single(s => s.name == "LeftBank"), -7.8f, 6.4f);
                Cluster(old.transform, "RightBank", sprites.Single(s => s.name == "RightBank"), 7.1f, 5.8f);
                var anchor = old.transform.Find("DoubaoRooftopAnchor");
                if (anchor == null) throw new InvalidOperationException("Missing existing rooftop anchor");
                // 保留旧命名以维持引用，语义现为堤岸平台落脚点而非楼顶。
                // 表示本体根中心；按合成中的脚底接触点校准，不改变人物尺寸。
                anchor.position = new Vector3(5.9f, -3.7f, 0f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
            return "World01 finite panorama and riverbank foreground installed; rejected housing retained inactive.";
        }

        public static string InstallCenteredMenus()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            int count = 0;
            foreach (string path in new[] { "Assets/Scenes/Gameplay_Prototype.unity", ScenePath, "Assets/Scenes/MainMenu.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException(path + " has unsaved edits");
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var fitter in All<SafeAreaRectFitter>(scene))
                    {
                        // 摇杆/技能键不跟随菜单居中策略，继续按实际安全边缘放置。
                        if (fitter.transform.root.name == "UI_TouchControls") continue;
                        var so = new SerializedObject(fitter);
                        so.FindProperty("_symmetricInsets").boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        count++;
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            return count + " centered safe-area roots configured; touch roots unchanged.";
        }

        private static SpriteRenderer Cluster(Transform parent, string name, Sprite sprite, float x, float width)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Gameplay";
            sr.sortingOrder = 30;
            float scale = width / sprite.bounds.size.x;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.localPosition = new Vector3(x, -5.7f, 0);
            return sr;
        }

        private static GameObject Root(Scene scene, string name)
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
            if (go != null) return go;
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        private static void Import(string path, bool sheet)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = sheet;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (sheet)
            {
#pragma warning disable CS0618
                importer.spritesheet = new[] {
                    new SpriteMetaData {name="LeftBank",rect=new Rect(0,40,887,650),alignment=9,pivot=new Vector2(.5f,0)},
                    new SpriteMetaData {name="RightBank",rect=new Rect(887,40,887,650),alignment=9,pivot=new Vector2(.5f,0)}
                };
#pragma warning restore CS0618
            }
            importer.SaveAndReimport();
        }
    }
}
