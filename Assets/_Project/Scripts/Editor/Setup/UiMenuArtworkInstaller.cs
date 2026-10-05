using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式安装菜单生成美术及遗漏的关卡入口装饰；不重建页面，不改变业务按钮。</summary>
    public static class UiMenuArtworkInstaller
    {
        private const string SCENE = "Assets/Scenes/MainMenu.unity";
        private const string CONFIG = "Assets/_Project/Configs/Presentation/UI/CFG_UI_";

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before menu artwork assembly.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before assembly.");
            var ds = AssetDatabase.LoadAssetAtPath<UiThemePalette>(CONFIG + "DeepSeek.asset");
            var hs = AssetDatabase.LoadAssetAtPath<UiThemePalette>(CONFIG + "Harness.asset");
            if (ds == null || hs == null) throw new InvalidOperationException("Both existing UI palettes are required.");
            // 在任何资产或场景写入前确认四张生产图均已作为 Sprite 导入。
            Sprite dsLogo = Load("DeepSeek", "SPR_UI_DS_TitleLogo"), dsBackground = Load("DeepSeek", "BG_UI_DS_MainMenu");
            Sprite hsLogo = Load("Harness", "SPR_UI_HA_TitleLogo"), hsBackground = Load("Harness", "BG_UI_HA_MainMenu");
            Scene active = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(SCENE);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).Single();
                var view = controller.GetComponent<UiThemeView>();
                if (view == null || controller.GetComponent<Canvas>() == null)
                    throw new InvalidOperationException("Main menu requires its existing Canvas and UiThemeView.");
                var serialized = new SerializedObject(controller);
                var home = (GameObject)serialized.FindProperty("_home").objectReferenceValue;
                var prototype = (Button)serialized.FindProperty("_prototypeButton").objectReferenceValue;
                var world = (Button)serialized.FindProperty("_world01Button").objectReferenceValue;
                var title = home.GetComponentsInChildren<Text>(true).Single(t => t.text == "DeepSleep");
                var background = view.transform.Cast<Transform>().Single(t => t.name == "Background").GetComponent<Image>();
                var label = world.GetComponentsInChildren<Text>(true).Single(t => t.name == "EnterLabel");
                var sourceMotion = prototype.GetComponent<UiButtonMotion>();
                var worldMotion = world.GetComponent<UiButtonMotion>();
                if (background == null || sourceMotion == null || worldMotion == null || worldMotion.Visual == null)
                    throw new InvalidOperationException("Existing background and both level button motions are required.");

                ds.TitleLogo = dsLogo; ds.MenuBackground = dsBackground;
                hs.TitleLogo = hsLogo; hs.MenuBackground = hsBackground;
                EditorUtility.SetDirty(ds); EditorUtility.SetDirty(hs);
                InstallBackground(view, background);
                InstallTitle(view, title);
                InstallEntry(view, sourceMotion, worldMotion, label);
                view.Apply(UiThemePreferences.Current);
                EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!loaded) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
            return "Menu generated logos/backgrounds bound for both themes; World01 entry decoration added. Original buttons, events and hit rectangles retained.";
        }

        private static void InstallBackground(UiThemeView view, Image background)
        {
            var fitter = background.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            background.transform.SetAsFirstSibling();
            background.rectTransform.pivot = new Vector2(.5f, .5f);
            background.type = Image.Type.Simple; background.color = Color.white; background.raycastTarget = false;
            view.Graphics = view.Graphics.Where(b => b.Target != background).ToArray();
            view.MenuBackgrounds = new[] { new UiThemeView.BackgroundBinding { Target = background, Fitter = fitter } };
        }

        private static void InstallTitle(UiThemeView view, Text title)
        {
            Image logo;
            if (view.TitleLogos.Length == 0)
            {
                var go = new GameObject("ThemeTitleLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(title.transform.parent, false);
                go.transform.SetSiblingIndex(title.transform.GetSiblingIndex() + 1);
                logo = go.GetComponent<Image>();
                CopyRect(title.rectTransform, logo.rectTransform);
                // 透明徽饰比原字体更高；向上扩展，不侵入下方副标题与按钮。
                logo.rectTransform.sizeDelta = new Vector2(640, 180);
                logo.rectTransform.anchoredPosition = new Vector2(title.rectTransform.anchoredPosition.x, 305);
            }
            else logo = view.TitleLogos.Single();
            logo.type = Image.Type.Simple; logo.color = Color.white; logo.raycastTarget = false; logo.preserveAspect = true;
            title.enabled = false;
            view.Graphics = view.Graphics.Where(b => b.Target != title).ToArray();
            view.TitleLogos = new[] { logo };
        }

        private static void InstallEntry(UiThemeView view, UiButtonMotion source, UiButtonMotion target, Text label)
        {
            if (target.Visual.Cast<Transform>().Any(t => t.name == "EnterActionArtwork")) return;
            var go = new GameObject("EnterActionArtwork", typeof(RectTransform));
            go.transform.SetParent(target.Visual, false);
            CopyRect(label.rectTransform, (RectTransform)go.transform);
            var art = new List<UiThemeView.ArtworkBinding>(view.Artwork);
            foreach (UiArtworkKind kind in new[] { UiArtworkKind.ButtonBase, UiArtworkKind.ButtonFrame, UiArtworkKind.ButtonOrnament })
            {
                Image sourceImage = view.Artwork.Single(b => b.Kind == kind && b.Target.transform.parent == source.Visual).Target;
                var layer = new GameObject(sourceImage.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                layer.transform.SetParent(go.transform, false);
                var image = layer.GetComponent<Image>();
                CopyRect(sourceImage.rectTransform, image.rectTransform);
                image.sprite = sourceImage.sprite; image.type = sourceImage.type; image.color = sourceImage.color;
                image.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
                image.preserveAspect = sourceImage.preserveAspect; image.raycastTarget = false;
                art.Add(new UiThemeView.ArtworkBinding { Target = image, Kind = kind });
            }
            // 左右入口用相同文案；点击仍由右侧原整卡 Button 接收。
            label.text = "进入关卡";
            view.Artwork = art.ToArray();
        }

        private static Sprite Load(string theme, string name)
        {
            string path = "Assets/_Project/Art/UI/Themes/" + theme + "/" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing imported Sprite: " + path);
            return sprite;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta; target.anchoredPosition = source.anchoredPosition;
            target.localScale = Vector3.one; target.localRotation = Quaternion.identity;
        }
    }
}
