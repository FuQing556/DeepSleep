using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>仅显式执行：把已有主题装饰换为生成 PNG 层，保留业务根、事件、热区与布局。</summary>
    public static class UiLayeredArtworkInstaller
    {
        private const string CONFIG = "Assets/_Project/Configs/Presentation/UI/CFG_UI_";
        private static readonly string[] SCENES = { "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet", "Boot" };
        private static readonly string[] PREFABS = { "PF_UI_MetaProductCard", "PF_UI_AchievementCard" };
        private static readonly string[] PANELS = { "Home", "LevelSelection", "WhaleVoucherShop", "Inventory", "Achievements",
            "CharacterSelectionPanel", "SessionOverlay", "ExitConfirmation", "RestNodeUpgradePanel", "ChapterSettlementPanel" };

        /// <summary>两个切片缩放值必须依据实际生产图边距传入；不会猜图或重跑旧布局安装。</summary>
        public static string Install(float buttonSliceScale, float panelSliceScale)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before artwork assembly.");
            if (!(buttonSliceScale > 0) || !(panelSliceScale > 0) || float.IsInfinity(buttonSliceScale) || float.IsInfinity(panelSliceScale))
                throw new ArgumentOutOfRangeException("Slice multipliers must be finite and positive.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before assembly: " + SceneManager.GetSceneAt(i).path);
            UiThemePalette ds = AssetDatabase.LoadAssetAtPath<UiThemePalette>(CONFIG + "DeepSeek.asset");
            UiThemePalette hs = AssetDatabase.LoadAssetAtPath<UiThemePalette>(CONFIG + "Harness.asset");
            if (ds == null || hs == null) throw new InvalidOperationException("Both existing palette assets are required.");
            // 先检查两套生产图都齐全、切片已设置，再触碰正式场景。
            Sprite[] dsSprites = LoadSprites("DeepSeek", "DS"), hsSprites = LoadSprites("Harness", "HA");
            BindPalette(ds, dsSprites); BindPalette(hs, hsSprites);
            Scene active = SceneManager.GetActiveScene();
            int converted = 0;
            try
            {
                foreach (string name in SCENES)
                {
                    string path = "Assets/Scenes/" + name + ".unity";
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool wasLoaded = scene.IsValid() && scene.isLoaded;
                    if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        foreach (var view in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UiThemeView>(true)))
                            converted += Convert(view, buttonSliceScale, panelSliceScale);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
                }
                foreach (string name in PREFABS)
                {
                    string path = "Assets/_Project/Prefabs/UI/Meta/" + name + ".prefab";
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        converted += Convert(root.GetComponent<UiThemeView>(), buttonSliceScale, panelSliceScale);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
            return "Generated artwork assembled: " + converted + " legacy surfaces converted across 4 scenes and 2 prefabs. Controls and hit rectangles retained.";
        }

        private static Sprite[] LoadSprites(string theme, string prefix)
        {
            var kinds = (UiArtworkKind[])Enum.GetValues(typeof(UiArtworkKind));
            var result = new Sprite[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                string path = "Assets/_Project/Art/UI/Themes/" + theme + "/SPR_UI_" + prefix + "_" + kinds[i] + ".png";
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (result[i] == null) throw new InvalidOperationException("Missing imported production Sprite: " + path);
                if (IsSliced(kinds[i]) && (result[i].border.x <= 0 || result[i].border.y <= 0 || result[i].border.z <= 0 || result[i].border.w <= 0))
                    throw new InvalidOperationException("Measured 9-slice borders must be set first: " + path);
            }
            return result;
        }

        private static void BindPalette(UiThemePalette p, Sprite[] s)
        {
            p.ButtonBase = s[(int)UiArtworkKind.ButtonBase]; p.ButtonFrame = s[(int)UiArtworkKind.ButtonFrame];
            p.ButtonOrnament = s[(int)UiArtworkKind.ButtonOrnament]; p.ButtonGlow = s[(int)UiArtworkKind.ButtonGlow];
            p.PanelFrame = s[(int)UiArtworkKind.PanelFrame]; p.CircleBase = s[(int)UiArtworkKind.CircleBase];
            p.CircleFrame = s[(int)UiArtworkKind.CircleFrame]; EditorUtility.SetDirty(p);
        }

        private static int Convert(UiThemeView view, float buttonScale, float panelScale)
        {
            if (view == null) throw new InvalidOperationException("Existing UiThemeView is required; this is not a UI generator.");
            var art = new List<UiThemeView.ArtworkBinding>(view.Artwork);
            var graphics = new List<UiThemeView.GraphicBinding>(view.Graphics);
            int count = view.Surfaces.Length;
            foreach (var old in view.Surfaces)
            {
                if (old.Target == null) throw new InvalidOperationException(view.name + " has a missing legacy surface.");
                RectTransform source = old.Target.rectTransform;
                bool halo = source.name == "PortraitHalo";
                Transform host = halo ? source : source.parent;
                Image hit = host.GetComponent<Image>();
                Selectable control = host.GetComponent<Selectable>();
                bool raycast = old.Target.raycastTarget;
                if (old.Tone == UiThemeView.SurfaceTone.Backdrop)
                {
                    if (hit == null) throw new InvalidOperationException("Backdrop has no original Image: " + host.name);
                    hit.enabled = true; hit.sprite = null; hit.color = new Color(1, 1, 1, old.Opacity); hit.raycastTarget = raycast;
                    graphics.Add(new UiThemeView.GraphicBinding { Target = hit, Tone = UiThemeView.GraphicTone.Backdrop });
                }
                else
                {
                    bool circle = old.RadiusOverride >= 999;
                    bool button = control is Button && !circle && ((RectTransform)host).rect.height < 180;
                    RectTransform visual = Visual(host);
                    Image face = Layer(visual, "Base", circle ? UiArtworkKind.CircleBase : UiArtworkKind.ButtonBase, old.Opacity, art);
                    if (((RectTransform)host).rect.height > 16)
                        Layer(visual, "Frame", circle ? UiArtworkKind.CircleFrame : button ? UiArtworkKind.ButtonFrame : UiArtworkKind.PanelFrame, halo ? .24f : old.Opacity, art);
                    if (button) Ornament(visual, host, art);
                    if (hit != null)
                    {
                        // 视觉缩放只发生在子层；原矩形负责命中，不能随按压缩小。
                        hit.enabled = true; hit.color = Color.clear; hit.raycastTarget = raycast;
                    }
                    if (control != null)
                    {
                        control.targetGraphic = face;
                        if (control is Button)
                        {
                            UiArtworkKind glowKind = circle ? UiArtworkKind.CircleFrame : button ? UiArtworkKind.ButtonGlow : UiArtworkKind.PanelFrame;
                            Image highlight = Layer(visual, "Highlight", glowKind, 1, art);
                            Image glow = Layer(visual, "Glow", glowKind, 1, art);
                            // 编辑模式下高光不常亮；运行时动效独立驱动 CanvasRenderer alpha。
                            highlight.canvasRenderer.SetAlpha(0); glow.canvasRenderer.SetAlpha(0);
                            var motion = host.gameObject.GetComponent<UiButtonMotion>();
                            if (motion == null) motion = host.gameObject.AddComponent<UiButtonMotion>();
                            motion.Control = control; motion.Visual = visual; motion.Highlight = highlight; motion.Glow = glow;
                        }
                    }
                }
                // 只销毁旧装饰组件/空子层，业务根与其上原始 Image、事件、引用保持不变。
                if (halo) Object.DestroyImmediate(old.Target);
                else Object.DestroyImmediate(source.gameObject);
            }
            view.Surfaces = Array.Empty<UiThemeView.SurfaceBinding>();
            view.Artwork = art.ToArray(); view.Graphics = graphics.ToArray(); view.enabled = true;
            foreach (var b in view.Artwork)
            {
                if (IsSliced(b.Kind)) b.Target.pixelsPerUnitMultiplier = b.Kind == UiArtworkKind.PanelFrame ? panelScale : buttonScale;
                if (b.Target.name == "Frame" && (b.Kind == UiArtworkKind.ButtonFrame || b.Kind == UiArtworkKind.PanelFrame))
                {
                    Color color = b.Target.color; color.a = Mathf.Min(color.a, .70f); b.Target.color = color;
                    if (b.Kind == UiArtworkKind.PanelFrame)
                    {
                        var face = art.FirstOrDefault(a => a.Target.name == "Base" && a.Target.transform.parent == b.Target.transform.parent).Target;
                        if (face == null) throw new InvalidOperationException("Panel Frame has no explicitly bound Base: " + b.Target.transform.parent.name);
                        face.rectTransform.offsetMin = new Vector2(4, 4); face.rectTransform.offsetMax = new Vector2(-4, -4);
                    }
                }
                if (b.Kind == UiArtworkKind.ButtonGlow)
                {
                    b.Target.rectTransform.offsetMin = new Vector2(-4, -4); b.Target.rectTransform.offsetMax = new Vector2(4, 4);
                }
            }
            // 迁移后重跑也处理既有动效；只增加表现透明度，不改变控件可交互或射线规则。
            foreach (var motion in view.GetComponentsInChildren<UiButtonMotion>(true))
            {
                var group = motion.GetComponent<CanvasGroup>(); if (group == null) group = motion.gameObject.AddComponent<CanvasGroup>();
                motion.WholeControlGroup = group;
            }
            foreach (Transform panel in view.GetComponentsInChildren<Transform>(true).Where(t => PANELS.Contains(t.name)))
            {
                var group = panel.GetComponent<CanvasGroup>(); if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
                var motion = panel.GetComponent<UiPanelMotion>(); if (motion == null) motion = panel.gameObject.AddComponent<UiPanelMotion>();
                motion.Group = group;
            }
            view.Apply(UiThemePreferences.Current); EditorUtility.SetDirty(view);
            return count;
        }

        private static RectTransform Visual(Transform host)
        {
            if (host.Find("ThemeArtwork") != null) throw new InvalidOperationException("Legacy and generated layers overlap at " + host.name);
            var go = new GameObject("ThemeArtwork", typeof(RectTransform));
            go.transform.SetParent(host, false); go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform; Stretch(rect); return rect;
        }

        private static Image Layer(RectTransform parent, string name, UiArtworkKind kind, float opacity, List<UiThemeView.ArtworkBinding> art)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); Stretch(image.rectTransform);
            image.raycastTarget = false; image.color = new Color(1, 1, 1, opacity);
            image.type = IsSliced(kind) ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !IsSliced(kind);
            art.Add(new UiThemeView.ArtworkBinding { Target = image, Kind = kind }); return image;
        }

        private static void Ornament(RectTransform visual, Transform host, List<UiThemeView.ArtworkBinding> art)
        {
            Image image = Layer(visual, "Ornament", UiArtworkKind.ButtonOrnament, .88f, art);
            RectTransform rect = image.rectTransform;
            float size = Mathf.Min(36, ((RectTransform)host).rect.height * .52f);
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size); rect.anchoredPosition = new Vector2(16 + size * .5f, 0);
            foreach (Text label in host.GetComponentsInChildren<Text>(true))
                if (label.transform.parent == host && label.alignment == TextAnchor.MiddleLeft && label.rectTransform.anchorMin.x == 0 && label.rectTransform.anchorMax.x == 1)
                {
                    Vector2 inset = label.rectTransform.offsetMin; inset.x = Mathf.Max(inset.x, size + 26); label.rectTransform.offsetMin = inset;
                }
        }

        private static bool IsSliced(UiArtworkKind kind) => kind == UiArtworkKind.ButtonBase || kind == UiArtworkKind.ButtonFrame ||
            kind == UiArtworkKind.ButtonGlow || kind == UiArtworkKind.PanelFrame;
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
    }
}
