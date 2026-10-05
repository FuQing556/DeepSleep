using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只在隔离预览场景/预制体副本中检查本轮 uGUI 装配，不保存正式资产。</summary>
    public static class UiThemeSceneChecks
    {
        private static int _checks;
        private static string _key;
        private static bool _hadPreference;
        private static int _preference;

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before UI scene checks.");
            _checks = 0;
            _key = (string)typeof(UiThemePreferences).GetField("PreferenceKey",
                BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
            _hadPreference = PlayerPrefs.HasKey(_key); _preference = PlayerPrefs.GetInt(_key);
            int canvases = 0;
            try
            {
                foreach (string name in new[] { "Boot", "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet" })
                {
                    var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
                    try
                    {
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            CheckControls(root);
                            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                            {
                                if (canvas.name == "UI_DamageNumbers") continue;
                                CheckView(canvas.GetComponent<UiThemeView>(), name + "/" + canvas.name);
                                var scaler = canvas.GetComponent<CanvasScaler>();
                                Check(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                                    scaler.referenceResolution == new Vector2(1920, 1080) &&
                                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                                    Mathf.Approximately(scaler.matchWidthOrHeight, 1), name + "/" + canvas.name + " scaler mismatch.");
                                canvases++;
                            }
                        }
                    }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
                foreach (string name in new[] { "PF_UI_MetaProductCard", "PF_UI_AchievementCard" })
                {
                    var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Meta/" + name + ".prefab");
                    try
                    {
                        CheckControls(root); CheckView(root.GetComponent<UiThemeView>(), name);
                        var layout = root.GetComponent<LayoutElement>();
                        Check(layout != null && layout.preferredHeight > 0, name + " preferredHeight missing.");
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (int width in new[] { 1920, 2640, 2880 })
                foreach (bool left in new[] { true, false })
                {
                    var safe = new Rect(left ? 140 : 0, 20, width - 140, 1060);
                    var area = SafeAreaRectFitter.CalculateArea(safe, new Vector2Int(width, 1080), true);
                    Check(Vector2.Distance(area.center, new Vector2(width / 2f, 540)) < .01f &&
                        area.xMin >= safe.xMin && area.xMax <= safe.xMax && area.yMin >= safe.yMin && area.yMax <= safe.yMax,
                        "Symmetric safe area must remain centered and inside asymmetric notch bounds.");
                }
                CheckPreference();
                return _checks + " UI checks passed across 4 preview scenes / " + canvases +
                    " themed canvases / 2 card prefabs. No Play simulation or assets saved; preference unchanged.";
            }
            finally
            {
                // 即使未来 Apply 错误引入偏好写入并使检查失败，也还原进入检查前的 key。
                if (PlayerPrefs.HasKey(_key) != _hadPreference || PlayerPrefs.GetInt(_key) != _preference)
                {
                    if (_hadPreference) PlayerPrefs.SetInt(_key, _preference); else PlayerPrefs.DeleteKey(_key);
                    PlayerPrefs.Save();
                }
            }
        }

        private static void CheckControls(GameObject root)
        {
            Check(!root.GetComponentsInChildren<Component>(true).Any(c => c != null &&
                c.GetType().FullName == "UnityEngine.UIElements.UIDocument"), root.name + " contains UI Toolkit.");
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (!(selectable is Button) && !(selectable is InputField)) continue;
                var graphic = selectable.targetGraphic;
                var hit = selectable.GetComponent<Image>();
                Check(graphic != null && graphic.enabled && hit != null && hit.enabled && hit.raycastTarget,
                    root.name + "/" + selectable.name + " visual or original fixed hit Image is missing/disabled.");
                if (selectable is Button)
                {
                    var motion = selectable.GetComponent<UiButtonMotion>();
                    Check(motion != null && motion.Control == selectable && motion.Visual != null &&
                        motion.WholeControlGroup != null && motion.WholeControlGroup.gameObject == selectable.gameObject &&
                        motion.Visual != selectable.transform && motion.Visual.IsChildOf(selectable.transform) &&
                        motion.Highlight != null && motion.Glow != null && !motion.Highlight.raycastTarget && !motion.Glow.raycastTarget,
                        root.name + "/" + selectable.name + " motion must affect only explicit non-raycast visual layers.");
                }
            }
        }

        private static void CheckView(UiThemeView view, string path)
        {
            Check(view != null, path + " missing UiThemeView.");
            Check(view.DeepSeek != null && view.Harness != null, path + " missing dual palettes.");
            Check(view.Surfaces != null && view.Artwork != null && view.Graphics != null && view.Portraits != null &&
                view.TitleLogos != null && view.MenuBackgrounds != null, path + " null binding array.");
            Check(view.Surfaces.Length == 0 && view.GetComponentsInChildren<UiSurfaceGraphic>(true).Length == 0, path + " legacy geometry is still attached.");
            Check(view.Artwork.Length + view.Graphics.Length + view.Portraits.Length > 0, path + " empty theme bindings.");
            foreach (var b in view.Artwork) Check(b.Target != null && !b.Target.raycastTarget, path + " null or raycastable decorative artwork.");
            foreach (var b in view.Graphics) Check(b.Target != null, path + " null graphic target.");
            foreach (var b in view.Portraits) Check(b.Target != null && b.Target.name == "ThemePortrait", path + " global portrait binding touches a role portrait.");
            var portraits = view.GetComponentsInChildren<Image>(true).Where(i => i.name == "Portrait" || i.name == "CharacterPortrait").ToArray();
            var sprites = portraits.Select(i => i.sprite).ToArray(); var colors = portraits.Select(i => i.color).ToArray();
            foreach (PlayerRole role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
            {
                view.Apply(role); CheckPreference();
                var palette = role == PlayerRole.Harness ? view.Harness : view.DeepSeek;
                foreach (var b in view.Artwork)
                {
                    Check(palette.GetArtwork(b.Kind) != null && b.Target.sprite == palette.GetArtwork(b.Kind),
                        path + "/" + b.Target.name + " generated Sprite theme mismatch.");
                    bool sliced = b.Kind == UiArtworkKind.ButtonBase || b.Kind == UiArtworkKind.ButtonFrame ||
                        b.Kind == UiArtworkKind.ButtonGlow || b.Kind == UiArtworkKind.PanelFrame;
                    Check(sliced ? b.Target.type == Image.Type.Sliced && b.Target.sprite.border.sqrMagnitude > 0 :
                        b.Target.type == Image.Type.Simple && b.Target.preserveAspect,
                        path + "/" + b.Target.name + " must use measured 9-slice or preserve-aspect ornaments/circles.");
                }
                foreach (var b in view.Graphics)
                {
                    Color expected = b.Tone == UiThemeView.GraphicTone.Muted ? palette.MutedText :
                        b.Tone == UiThemeView.GraphicTone.Accent ? palette.Accent :
                        b.Tone == UiThemeView.GraphicTone.OnAccent ? palette.OnAccent :
                        b.Tone == UiThemeView.GraphicTone.Backdrop ? palette.Background : palette.Text;
                    if (b.Tone == UiThemeView.GraphicTone.Backdrop) expected.a = b.Target.color.a;
                    Check(b.Target.color == expected, path + "/" + b.Target.name + " graphic theme color mismatch.");
                }
                foreach (var b in view.Portraits)
                    Check(palette.Portrait != null && b.Target.sprite == palette.Portrait && b.Target.preserveAspect,
                        path + " theme portrait mismatch.");
                foreach (var logo in view.TitleLogos)
                    Check(logo != null && palette.TitleLogo != null && logo.sprite == palette.TitleLogo &&
                        logo.type == Image.Type.Simple && logo.preserveAspect && !logo.raycastTarget,
                        path + " generated title logo must preserve aspect and ignore raycasts.");
                foreach (var b in view.MenuBackgrounds)
                    Check(b.Target != null && b.Fitter != null && b.Target.gameObject == b.Fitter.gameObject &&
                        b.Target.sprite == palette.MenuBackground && palette.MenuBackground != null &&
                        b.Target.transform.parent.GetComponent<Canvas>() != null && !b.Target.raycastTarget &&
                        b.Target.type == Image.Type.Simple && b.Target.preserveAspect &&
                        b.Fitter.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent &&
                        Mathf.Approximately(b.Fitter.aspectRatio, palette.MenuBackground.rect.width / palette.MenuBackground.rect.height),
                        path + " menu background must cover the full Canvas with aspect preserved.");
                for (int i = 0; i < portraits.Length; i++)
                    Check(portraits[i].sprite == sprites[i] && portraits[i].color == colors[i], path + " role portrait changed with global theme.");
            }
        }

        private static void CheckPreference() => Check(PlayerPrefs.HasKey(_key) == _hadPreference &&
            PlayerPrefs.GetInt(_key) == _preference, "Applying a visual theme wrote the local preference.");
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            _checks++;
        }
    }
}
