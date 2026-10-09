using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class ClaudePermissionStatusInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            const string path = "Assets/Scenes/World02_2066.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var module = roots.SelectMany(r => r.GetComponentsInChildren<ClaudePermissionModule2D>(true)).Single();
                module.HarnessMelee = roots.SelectMany(r => r.GetComponentsInChildren<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleeController>(true)).Single();
                var existing = roots.SelectMany(r => r.GetComponentsInChildren<ClaudePermissionStatusView>(true)).SingleOrDefault();
                if (existing != null && existing.Markers != null && existing.Markers.Length == 6 && existing.Markers.All(m => m != null))
                {
                    ApplyBookStyle(existing);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    return "Updated six circles to exact book-page style and labels.";
                }
                var root = existing != null ? existing.gameObject : new GameObject("ClaudePermissionStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                if (existing != null)
                    for (int i = root.transform.childCount - 1; i >= 0; i--)
                        UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1980;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
                var view = existing != null ? existing : root.AddComponent<ClaudePermissionStatusView>();
                view.Permissions = module;
                view.WorldCamera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
                view.CanvasRect = (RectTransform)root.transform;
                view.Rows = new RectTransform[2]; view.Groups = new CanvasGroup[2]; view.Markers = new Image[6];
                for (int role = 0; role < 2; role++)
                {
                    var row = Rect(role == 0 ? "DS_Status" : "HS_Status", root.transform, Vector2.zero);
                    view.Rows[role] = row; view.Groups[role] = row.gameObject.AddComponent<CanvasGroup>();
                    view.Groups[role].alpha = 0; view.Groups[role].interactable = view.Groups[role].blocksRaycasts = false;
                    var palette = role == 0 ? module.Books[0].DeepSeekPalette : module.Books[0].HarnessPalette;
                    for (int item = 0; item < 3; item++)
                    {
                        var chip = Rect("Permission_" + item, row, new Vector2(46, 46));
                        var circle = chip.gameObject.AddComponent<Image>();
                        circle.sprite = palette.CircleBase; circle.preserveAspect = true;
                        circle.raycastTarget = false; circle.color = Color.white;
                        var frameRect = Rect("Frame", chip, new Vector2(46, 46));
                        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one; frameRect.sizeDelta = Vector2.zero;
                        var frame = frameRect.gameObject.AddComponent<Image>(); frame.sprite = palette.CircleFrame;
                        frame.preserveAspect = true; frame.raycastTarget = false;
                        view.Markers[role * 3 + item] = circle;
                        chip.gameObject.SetActive(false);
                    }
                }
                ApplyBookStyle(view);
                EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return "Replaced text chips with six DS/HS book identity circles; fixed centered triangle.";
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        private static void ApplyBookStyle(ClaudePermissionStatusView view)
        {
            var source = view.Permissions.Books[0];
            view.PageReferenceSize = source.Label.rectTransform.rect.width;
            view.Labels = new Text[6]; view.Frames = new Image[6];
            view.Decorations = new Image[6];
            for (int index = 0; index < 6; index++)
            {
                var marker = view.Markers[index];
                var decorationName = marker.name + "_Decoration";
                var decorationTransform = marker.transform.parent.Find(decorationName);
                var decorationRect = decorationTransform != null ? (RectTransform)decorationTransform :
                    Rect(decorationName, marker.transform.parent, marker.rectTransform.sizeDelta * 1.15f);
                var decoration = decorationRect.GetComponent<Image>();
                if (decoration == null) decoration = decorationRect.gameObject.AddComponent<Image>();
                decoration.sprite = source.Decoration.sprite;
                decoration.color = source.Decoration.color;
                decoration.preserveAspect = true; decoration.raycastTarget = false;
                decorationRect.anchoredPosition = marker.rectTransform.anchoredPosition;
                decorationRect.localScale = source.IdentityCircle.rectTransform.localScale;
                decorationRect.SetAsFirstSibling();
                decoration.gameObject.SetActive(marker.gameObject.activeSelf);
                view.Decorations[index] = decoration;
                EditorUtility.SetDirty(decoration);
                marker.color = source.IdentityCircle.color;
                marker.rectTransform.localScale = source.IdentityCircle.rectTransform.localScale;
                var frame = marker.transform.Find("Frame").GetComponent<Image>();
                frame.rectTransform.localScale = Vector3.one / marker.rectTransform.localScale.x;
                frame.color = source.Progress.color; frame.type = source.Progress.type;
                frame.fillMethod = source.Progress.fillMethod; frame.fillOrigin = source.Progress.fillOrigin;
                frame.fillClockwise = source.Progress.fillClockwise; frame.fillAmount = 1;
                view.Frames[index] = frame;
                var label = marker.GetComponentInChildren<Text>(true);
                if (label == null) label = UnityEngine.Object.Instantiate(source.Label, marker.transform, false);
                label.name = "PermissionLabel";
                label.text = view.Permissions.Config.PermissionLabels[index % 3];
                label.color = source.DeepSeekInk; label.raycastTarget = false;
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = source.Label.rectTransform.rect.size;
                rect.localScale = Vector3.one * (46 / view.PageReferenceSize / marker.rectTransform.localScale.x);
                label.transform.SetAsLastSibling(); view.Labels[index] = label;
                EditorUtility.SetDirty(marker); EditorUtility.SetDirty(frame); EditorUtility.SetDirty(label);
            }
            EditorUtility.SetDirty(view);
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; return rect;
        }
    }
}
