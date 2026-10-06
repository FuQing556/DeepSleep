using System;
using System.Linq;
using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class MobileUiLayoutInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
            var active = SceneManager.GetActiveScene();
            foreach (string name in new[] { "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (name == "MainMenu") InstallThumbnails(scene);
                    else InstallControls(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            return "Both gameplay scenes: mobile-only screen ratios installed. Menu: equal 568x220 cover viewports.";
        }

        private static void InstallControls(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var input = roots.SelectMany(r => r.GetComponentsInChildren<TouchCommandSource>(true)).Single();
            foreach (var pad in input.TouchCanvas.GetComponentsInChildren<TouchCommandPad>(true))
            {
                switch (pad.Kind)
                {
                    case TouchCommandPad.PadKind.Movement:
                        Bind(pad.Area, input, new Vector2(.182f, .261f), Vector2.one * .215f, false); break;
                    case TouchCommandPad.PadKind.Skill:
                        Bind(pad.Area, input, new Vector2(.866f, .179f), Vector2.one * .155f, false); break;
                    case TouchCommandPad.PadKind.Secondary:
                        Bind(pad.Area, input, new Vector2(.777f, .155f), Vector2.one * .115f, false); break;
                    case TouchCommandPad.PadKind.Cancel:
                        Bind(pad.Area, input, new Vector2(.878f, .34f), Vector2.one * .105f, false); break;
                }
            }
            foreach (var hud in roots.SelectMany(r => r.GetComponentsInChildren<PlayerCombatHudView>(true)))
            {
                var rect = (RectTransform)hud.transform;
                Bind(rect, input, new Vector2(rect.pivot.x == 0 ? .06f : .94f, .976f),
                    new Vector2(.41f, .41f * 230 / 480), true);
            }
            foreach (var rect in roots.SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)))
            {
                if (rect.parent == null || rect.parent.name != "SafeArea") continue;
                switch (rect.name)
                {
                    case "菜单": Bind(rect, input, new Vector2(.46f, .95f), new Vector2(.145f, .054375f), true); break;
                    case "AI 托管": Bind(rect, input, new Vector2(.54f, .95f), new Vector2(.145f, .054375f), true); break;
                    case "TokenWalletHUD": Bind(rect, input, new Vector2(.5f, .887f), new Vector2(.388f, .03104f), true); break;
                    case "ChapterRunHUD": Bind(rect, input, new Vector2(.5f, .833f), new Vector2(.431f, .056892f), true); break;
                }
            }
        }

        private static void Bind(RectTransform rect, TouchCommandSource input, Vector2 position, Vector2 size, bool scale)
        {
            var layout = rect.GetComponent<MobileScreenRect>();
            if (layout == null) layout = rect.gameObject.AddComponent<MobileScreenRect>();
            layout.Input = input; layout.ScreenPosition = position;
            layout.SizeInScreenHeights = size; layout.ScaleAuthoredContent = scale;
            EditorUtility.SetDirty(layout);
        }

        private static void InstallThumbnails(Scene scene)
        {
            var images = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Image>(true))
                .Where(i => i.name == "LevelPreview").ToArray();
            if (images.Length != 2) throw new InvalidOperationException("Expected two level previews.");
            foreach (var image in images)
            {
                if (AssetDatabase.GetAssetPath(image.sprite).Contains("BG_P0_"))
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Backgrounds/BG_P0_RestNode_DataTemple_v01.png");
                if (image.sprite == null) throw new InvalidOperationException("Missing level artwork.");
                var rect = image.rectTransform;
                var viewport = rect.parent.name == "LevelPreviewViewport" ? (RectTransform)rect.parent : null;
                if (viewport == null)
                {
                    viewport = new GameObject("LevelPreviewViewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                    viewport.SetParent(rect.parent, false); viewport.SetSiblingIndex(rect.GetSiblingIndex());
                    viewport.anchorMin = rect.anchorMin; viewport.anchorMax = rect.anchorMax;
                    viewport.anchoredPosition = rect.anchoredPosition;
                    rect.SetParent(viewport, false);
                }
                viewport.sizeDelta = new Vector2(568, 220);
                image.preserveAspect = false; image.raycastTarget = false;
                var fitter = image.GetComponent<AspectRatioFitter>();
                if (fitter == null) fitter = image.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            }
        }
    }
}
