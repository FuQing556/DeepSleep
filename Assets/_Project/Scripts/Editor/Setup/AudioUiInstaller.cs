using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>只追加声音绑定和三音量设置；场景由调用方保存，不重跑旧 UI 布局安装器。</summary>
    public static class AudioUiInstaller
    {
        public static string InstallInActiveScene()
        {
            RequireEditMode();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("A loaded scene is required.");
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var menu in All<MainMenuController>(roots))
            {
                GameObject home = Reference<GameObject>(menu, "_home");
                var quit = Reference<Button>(menu, "_quitButton");
                Vector2 position = new Vector2(-455, -271);
                if (quit != null) ((RectTransform)quit.transform).anchoredPosition = new Vector2(-455, -355);
                InstallSettings(home.transform, home.transform, menu.GetComponent<UiThemeView>(), position,
                    new Vector2(510, 64), new Vector2(.5f, .5f));
                var theme = menu.GetComponent<UiThemeView>();
                var entry = home.GetComponent<AudioSettingsPanel>().OpenButton;
                var text = entry.GetComponentsInChildren<Text>(true).First(t => t.name == "Label");
                text.alignment = TextAnchor.MiddleLeft; text.fontSize = 27;
                text.rectTransform.offsetMin = new Vector2(59.28f, 0); text.rectTransform.offsetMax = new Vector2(-24, 0);
                var graphics = new List<UiThemeView.GraphicBinding>(theme.Graphics);
                Label(entry.transform, "Arrow", "›", new Vector2(210, 0), new Vector2(30, 44), 32, text.font, graphics);
                theme.Graphics = graphics.ToArray(); theme.Apply(UiThemePreferences.Current); EditorUtility.SetDirty(theme);
            }
            foreach (var menu in All<CoopSessionMenu>(roots))
            {
                if (menu.Panel == null || menu.TitleLabel == null) throw new InvalidOperationException("Coop menu references are missing.");
                InstallSettings(menu.Panel.transform, menu.TitleLabel.transform.parent, menu.GetComponent<UiThemeView>(),
                    new Vector2(-165, -62), new Vector2(240, 64), new Vector2(1, 1));
                menu.MenuCard = (RectTransform)menu.TitleLabel.transform.parent;
                menu.LobbyMenuSize = new Vector2(1600, 960);
                menu.PlayMenuSize = new Vector2(1000, 800);
                EditorUtility.SetDirty(menu);
            }

            int count = 0;
            foreach (var selectable in All<Selectable>(roots))
            {
                if (selectable is Button || selectable is Slider)
                { BindFeedback(selectable, selectable is Button); count++; }
            }
            foreach (var menu in All<MainMenuController>(roots))
                Silence(menu, "_startGame", "_shopButton", "_inventoryButton", "_achievementsButton", "_prototypeButton", "_world01Button", "_backButton");
            foreach (var menu in All<CoopSessionMenu>(roots))
                Silence(menu, "TogglePanel", "CloseButton", "HostDs", "HostHs", "JoinButton", "RoomButtons", "ReadyButton", "AiButton", "QuickAi", "LeaveButton", "ConfirmLeave", "CancelLeave");
            foreach (var node in All<RestNodePrototypeController2D>(roots)) Silence(node, "_actionButton");
            foreach (var shop in All<RestNodeUpgradePanelView>(roots))
            {
                Silence(shop, "_cardButtons", "_refreshButton");
                BindFeedback(Reference<Button>(shop, "_closeButton"), true, AudioCue.UiCancel);
            }
            foreach (var card in All<MetaProductCardView>(roots)) Silence(card, "_purchase");
            foreach (var entry in All<GameplayEntryFlow>(roots)) BindFeedback(Reference<Button>(entry, "_backButton"), true, AudioCue.UiCancel);
            foreach (var settings in All<AudioSettingsPanel>(roots))
            {
                BindFeedback(settings.OpenButton, false);
                BindFeedback(settings.CloseButton, false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return $"Audio UI: {count} controls bound in {scene.name}; settings use existing safe area/artwork. Scene not saved.";
        }

        public static string InstallPrefabs()
        {
            RequireEditMode();
            int count = 0;
            foreach (string name in new[] { "PF_UI_MetaProductCard", "PF_UI_AchievementCard" })
            {
                string path = "Assets/_Project/Prefabs/UI/Meta/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing UI prefab: " + path);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var button in root.GetComponentsInChildren<Button>(true)) { BindFeedback(button, true); count++; }
                    foreach (var card in root.GetComponentsInChildren<MetaProductCardView>(true)) Silence(card, "_purchase");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return $"Audio UI: {count} buttons bound in two explicit card prefabs (achievement display has no invented interaction).";
        }

        private static void InstallSettings(Transform owner, Transform entryParent, UiThemeView theme,
            Vector2 entryPosition, Vector2 entrySize, Vector2 entryAnchor)
        {
            if (theme == null || theme.DeepSeek == null || theme.Harness == null)
                throw new InvalidOperationException("Existing dual UiThemeView is required.");
            foreach (var palette in new[] { theme.DeepSeek, theme.Harness })
                foreach (UiArtworkKind kind in Enum.GetValues(typeof(UiArtworkKind)))
                    if (palette.GetArtwork(kind) == null) throw new InvalidOperationException($"Missing generated {kind}: {palette.name}");
            Transform safe = owner.parent;
            while (safe != null && safe.GetComponent<SafeAreaRectFitter>() == null) safe = safe.parent;
            if (safe == null || !safe.IsChildOf(theme.transform)) throw new InvalidOperationException("Existing owning SafeArea is required.");
            Font font = theme.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.font != null)?.font;
            if (font == null) throw new InvalidOperationException("Existing UI font is required.");
            var art = new List<UiThemeView.ArtworkBinding>(theme.Artwork);
            var graphics = new List<UiThemeView.GraphicBinding>(theme.Graphics);

            Button open = MakeButton(entryParent, "AudioSettingsButton", "设置", entryPosition, entrySize, font, art, graphics);
            // 只在新入口首次生成时用约定锚点；重跑保留已调整的位置/尺寸。
            ((RectTransform)open.transform).anchorMin = ((RectTransform)open.transform).anchorMax = entryAnchor;
            RectTransform overlay = Rect(safe, "AudioSettingsOverlay", Vector2.zero, Vector2.zero, true);
            overlay.SetAsLastSibling();
            CanvasGroup group = Get<CanvasGroup>(overlay.gameObject);
            group.alpha = 0; group.interactable = group.blocksRaycasts = false;
            Image backdrop = Get<Image>(overlay.gameObject);
            backdrop.color = new Color(0, 0, 0, .78f); backdrop.raycastTarget = true;
            BindGraphic(graphics, backdrop, UiThemeView.GraphicTone.Backdrop);
            RectTransform panel = Rect(overlay, "Panel", Vector2.zero, new Vector2(1000, 800));
            panel.sizeDelta = new Vector2(1000, 800);
            Layer(panel, "Base", UiArtworkKind.ButtonBase, art);
            Layer(panel, "Frame", UiArtworkKind.PanelFrame, art, .7f);
            Label(panel, "Title", "设置", new Vector2(0, 330), new Vector2(700, 60), 34, font, graphics);
            Label(panel, "Hint", "局内 / 局外共用 · 仅本机生效 · 自动保存", new Vector2(0, 278), new Vector2(850, 36), 22, font, graphics, true);
            Label(panel, "SoundHeading", "声音", new Vector2(-305, 223), new Vector2(140, 36), 24, font, graphics);
            Slider master = MakeSlider(panel, "Master", "总音量", 170, font, art, graphics, out Text masterValue);
            Slider sfx = MakeSlider(panel, "Sfx", "音效", 90, font, art, graphics, out Text sfxValue);
            Slider ambience = MakeSlider(panel, "Ambience", "环境音", 10, font, art, graphics, out Text ambienceValue);
            Label(panel, "DisplayHeading", "画面", new Vector2(-305, -65), new Vector2(140, 36), 24, font, graphics);
            Slider clouds = MakeSlider(panel, "CloudDensity", "云朵密度", -125, font, art, graphics, out Text cloudValue);
            Button close = MakeButton(panel, "Close", "完成", new Vector2(0, -330), new Vector2(280, 64), font, art, graphics);
            var options = theme.GetComponentInChildren<PlayerHitFeedbackOptions>(true);
            if (options == null) options = owner.gameObject.AddComponent<PlayerHitFeedbackOptions>();
            if (options.gameObject != owner.gameObject)
            {
                var replacement = Get<PlayerHitFeedbackOptions>(owner.gameObject);
                EditorUtility.CopySerialized(options, replacement);
                Object.DestroyImmediate(options);
                options = replacement;
            }
            // 保留原设置组件/按钮的身份，只将原有画面选项移入共用设置面板。
            if (options.ShakeButton != null) { options.ShakeButton.transform.SetParent(panel, false); options.ShakeButton.name = "HitShakeOption"; }
            if (options.FlashButton != null) { options.FlashButton.transform.SetParent(panel, false); options.FlashButton.name = "HitFlashOption"; }
            options.ShakeButton = MakeButton(panel, "HitShakeOption", "受击震屏", new Vector2(-190, -230), new Vector2(345, 64), font, art, graphics);
            options.FlashButton = MakeButton(panel, "HitFlashOption", "受击闪光", new Vector2(190, -230), new Vector2(345, 64), font, art, graphics);
            options.ShakeLabel = options.ShakeButton.GetComponentsInChildren<Text>(true).First(t => t.name == "Label");
            options.FlashLabel = options.FlashButton.GetComponentsInChildren<Text>(true).First(t => t.name == "Label");
            EditorUtility.SetDirty(options);
            var settings = Get<AudioSettingsPanel>(owner.gameObject);
            settings.Panel = group; settings.MasterSlider = master; settings.SfxSlider = sfx; settings.AmbienceSlider = ambience;
            settings.MasterValue = masterValue; settings.SfxValue = sfxValue; settings.AmbienceValue = ambienceValue;
            settings.OpenButton = open; settings.CloseButton = close;
            settings.CloudSlider = clouds; settings.CloudValue = cloudValue;
            // 显式导航不越过弹层跳回底下的业务按钮；程序选中不触发焦点音。
            Selectable[] order = { master, sfx, ambience, clouds, options.ShakeButton, options.FlashButton, close };
            for (int i = 0; i < order.Length; i++)
                order[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = order[(i + order.Length - 1) % order.Length], selectOnDown = order[(i + 1) % order.Length] };
            SelectOnClick(open, master); SelectOnClick(close, open);
            theme.Artwork = art.ToArray(); theme.Graphics = graphics.ToArray(); theme.Apply(UiThemePreferences.Current);
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(theme);
        }

        private static Slider MakeSlider(Transform parent, string name, string title, float y, Font font,
            List<UiThemeView.ArtworkBinding> art, List<UiThemeView.GraphicBinding> graphics, out Text value)
        {
            RectTransform row = Rect(parent, name, new Vector2(0, y), new Vector2(720, 72));
            row.anchoredPosition = new Vector2(0, y);
            Label(row, "Label", title, new Vector2(-277, 0), new Vector2(150, 48), 25, font, graphics);
            value = Label(row, "Value", "", new Vector2(313, 0), new Vector2(90, 48), 24, font, graphics);
            RectTransform rect = Rect(row, "Slider", new Vector2(28, 0), new Vector2(450, 64));
            Image hit = Get<Image>(rect.gameObject); hit.color = Color.clear; hit.raycastTarget = true;
            RectTransform track = Rect(rect, "Track", Vector2.zero, new Vector2(450, 14));
            Layer(track, "Base", UiArtworkKind.ButtonBase, art, .65f);
            Layer(track, "Frame", UiArtworkKind.ButtonFrame, art, .7f);
            RectTransform fillArea = Rect(rect, "FillArea", Vector2.zero, new Vector2(410, 14));
            Image fill = Layer(fillArea, "Fill", UiArtworkKind.ButtonGlow, art, .85f);
            RectTransform handleArea = Rect(rect, "HandleArea", Vector2.zero, new Vector2(410, 42));
            Image handle = Layer(handleArea, "Handle", UiArtworkKind.CircleBase, art);
            handle.rectTransform.sizeDelta = new Vector2(42, 0);
            Layer(handle.transform, "Frame", UiArtworkKind.CircleFrame, art);
            Slider slider = Get<Slider>(rect.gameObject);
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(.8f);
            EditorUtility.SetDirty(slider);
            return slider;
        }

        private static Button MakeButton(Transform parent, string name, string title, Vector2 position, Vector2 size,
            Font font, List<UiThemeView.ArtworkBinding> art, List<UiThemeView.GraphicBinding> graphics)
        {
            RectTransform rect = Rect(parent, name, position, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            Image hit = Get<Image>(rect.gameObject); hit.color = Color.clear; hit.raycastTarget = true;
            Button button = Get<Button>(rect.gameObject);
            RectTransform visual = Rect(rect, "ThemeArtwork", Vector2.zero, Vector2.zero, true);
            visual.SetAsFirstSibling();
            Image face = Layer(visual, "Base", UiArtworkKind.ButtonBase, art);
            Layer(visual, "Frame", UiArtworkKind.ButtonFrame, art, .7f);
            Image ornament = Layer(visual, "Ornament", UiArtworkKind.ButtonOrnament, art);
            ornament.type = Image.Type.Simple; ornament.preserveAspect = true;
            ornament.rectTransform.anchorMin = ornament.rectTransform.anchorMax = new Vector2(0, .5f);
            ornament.rectTransform.sizeDelta = Vector2.one * (size.y * .65f);
            ornament.rectTransform.anchoredPosition = new Vector2(size.y * .45f, 0);
            Image highlight = Layer(visual, "Highlight", UiArtworkKind.ButtonGlow, art);
            Image glow = Layer(visual, "Glow", UiArtworkKind.ButtonGlow, art);
            highlight.canvasRenderer.SetAlpha(0); glow.canvasRenderer.SetAlpha(0);
            Text label = Label(rect, "Label", title, Vector2.zero, size - new Vector2(32, 0), 26, font, graphics);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(38, 0); label.rectTransform.offsetMax = new Vector2(-12, 0);
            label.transform.SetAsLastSibling();
            button.targetGraphic = face; button.transition = Selectable.Transition.None;
            var motion = Get<UiButtonMotion>(rect.gameObject);
            motion.Control = button; motion.WholeControlGroup = Get<CanvasGroup>(rect.gameObject);
            motion.Visual = visual; motion.Highlight = highlight; motion.Glow = glow;
            EditorUtility.SetDirty(motion);
            return button;
        }

        private static Image Layer(Transform parent, string name, UiArtworkKind kind, List<UiThemeView.ArtworkBinding> art, float alpha = 1)
        {
            Image image = Get<Image>(Rect(parent, name, Vector2.zero, Vector2.zero, true).gameObject);
            bool circle = kind == UiArtworkKind.CircleBase || kind == UiArtworkKind.CircleFrame;
            image.raycastTarget = false; image.color = new Color(1, 1, 1, alpha);
            image.type = circle ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = circle; image.pixelsPerUnitMultiplier = 6;
            art.RemoveAll(binding => binding.Target == image);
            art.Add(new UiThemeView.ArtworkBinding { Target = image, Kind = kind });
            return image;
        }

        private static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
            int fontSize, Font font, List<UiThemeView.GraphicBinding> graphics, bool muted = false)
        {
            Text label = Get<Text>(Rect(parent, name, position, size).gameObject);
            label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
            label.text = text; label.font = font; label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            BindGraphic(graphics, label, muted ? UiThemeView.GraphicTone.Muted : UiThemeView.GraphicTone.Text);
            return label;
        }

        private static void BindGraphic(List<UiThemeView.GraphicBinding> graphics, Graphic graphic, UiThemeView.GraphicTone tone)
        {
            graphics.RemoveAll(binding => binding.Target == graphic);
            graphics.Add(new UiThemeView.GraphicBinding { Target = graphic, Tone = tone });
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, bool stretch = false)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing as RectTransform ?? throw new InvalidOperationException("Expected RectTransform: " + name);
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            if (stretch) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            else { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
            return rect;
        }

        private static void BindFeedback(Selectable selectable, bool click, AudioCue cue = AudioCue.UiConfirm)
        {
            if (selectable == null) return;
            var feedback = Get<UiAudioFeedback>(selectable.gameObject);
            feedback.Control = selectable; feedback.PlayClick = click; feedback.ClickCue = cue;
            EditorUtility.SetDirty(feedback);
        }

        private static void Silence(Object owner, params string[] fields)
        {
            var serialized = new SerializedObject(owner);
            foreach (string name in fields)
            {
                var field = serialized.FindProperty(name) ?? throw new InvalidOperationException(owner.name + " missing field " + name);
                if (field.isArray)
                    for (int i = 0; i < field.arraySize; i++) BindFeedback(field.GetArrayElementAtIndex(i).objectReferenceValue as Selectable, false);
                else BindFeedback(field.objectReferenceValue as Selectable, false);
            }
        }

        private static void SelectOnClick(Button button, Selectable selection)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == selection && button.onClick.GetPersistentMethodName(i) == nameof(Selectable.Select)) return;
            UnityEventTools.AddPersistentListener(button.onClick, selection.Select);
            EditorUtility.SetDirty(button);
        }

        private static T Reference<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T;
        private static T Get<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }
        private static IEnumerable<T> All<T>(GameObject[] roots) where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true));
        private static void RequireEditMode()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before Audio UI assembly."); }
    }
}
