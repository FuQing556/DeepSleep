using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Progression.Bestiary;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户授权后显式安装首个图鉴页面；复用生产UI素材，不重建关卡或碰撞。</summary>
    public static class BestiaryInstaller
    {
        public const string EntryPath = "Assets/_Project/Configs/Progression/Bestiary/CFG_Bestiary_Kimi.asset";

        /// <summary>显式复制玩家已调好的第一关节点布局，不修改两张正式地图。</summary>
        public static string CapturePreparationLayout()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("先退出Play，处理未保存场景。");
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay_Prototype.unity");
                var node = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RestNodePrototypeController2D>(true)).Single();
                var so = new SerializedObject(node);
                var visual = (SpriteRenderer)so.FindProperty("_templeRenderer").objectReferenceValue;
                var hotspots = so.FindProperty("_hotspots");
                var layout = new RestNodePreparationLayout
                {
                    BackgroundPosition = visual.transform.position,
                    BackgroundScale = visual.transform.localScale,
                    Hotspots = new RestNodePreparationLayout.Hotspot[hotspots.arraySize]
                };
                for (int i = 0; i < hotspots.arraySize; i++)
                {
                    var hotspot = (RestNodeHotspot2D)hotspots.GetArrayElementAtIndex(i).objectReferenceValue;
                    var box = hotspot.GetComponent<BoxCollider2D>();
                    layout.Hotspots[i] = new RestNodePreparationLayout.Hotspot
                    {
                        Kind = hotspot.Kind, Position = hotspot.transform.position, Scale = hotspot.transform.localScale,
                        Size = box.size, Offset = box.offset, Enabled = box.enabled
                    };
                }
                var entry = AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>(EntryPath);
                entry.PreparationBackdrop = visual.sprite;
                entry.PreparationLayout = layout;
                EditorUtility.SetDirty(entry);
                AssetDatabase.SaveAssets();
                return "挑战配装背景与四个交互区域已复制自第一关节点。";
            }
            finally { if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous); }
        }

        public static string Install()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("先退出Play，处理未保存场景。");
            KimiPresentationInstaller.ConfigurePrefab();
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var menu = all.OfType<MainMenuController>().Single();
            var so = new SerializedObject(menu);
            var entry = AssetDatabase.LoadAssetAtPath<BestiaryEntryDefinition>(EntryPath);
            if (entry == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Configs/Progression/Bestiary"))
                    AssetDatabase.CreateFolder("Assets/_Project/Configs/Progression", "Bestiary");
                entry = ScriptableObject.CreateInstance<BestiaryEntryDefinition>();
                entry.EntryId = "kimi"; entry.DisplayName = "Kimi"; entry.Subtitle = "月之暗面 · 黄昏故都的月夜试炼者";
                entry.Description = "踩在云端的试炼者，以月光、棱镜和笛声编织战场。生命降至一半后，在招式间隙进入第二阶段。";
                entry.SkillNotes = "月光刃：十条横向通道，留意来自左右的预警。\n\n棱光：击破任意镜边，让反弹光球自行逸出。\n\n锁向激光：蓄力锁定方向；二阶段五束从上向下递进。\n\n笛声与潮汐：寻找角度击破次数盾，打断蓄力。";
                entry.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Kimi/SPR_KI_Idle_v01.png");
                entry.PreparationBackdrop = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Backgrounds/BG_P0_RestNode_DataTemple_v01.png");
                entry.Level = (MetaLevelDefinition)so.FindProperty("_world01Level").objectReferenceValue;
                entry.CombatSegment = 4; entry.StartingTokensPerRole = 1000; entry.PreludeSeconds = 3;
                entry.PreparationPrompt = "Kimi 快速挑战 · 双角色各1000 Token · 配装后进入传送门开始试炼";
                AssetDatabase.CreateAsset(entry, EntryPath);
            }
            if (entry.PreparationLayout == null)
            {
                EditorSceneManager.SaveScene(scene);
                CapturePreparationLayout();
                scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                menu = all.OfType<MainMenuController>().Single(); so = new SerializedObject(menu);
            }
            if (!entry.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            if (so.FindProperty("_bestiary").objectReferenceValue == null)
            {
                var home = (GameObject)so.FindProperty("_home").objectReferenceValue;
                var levels = (GameObject)so.FindProperty("_levelSelection").objectReferenceValue;
                var card = levels.transform.GetChild(2);
                var art = card.GetChild(0).gameObject;
                var textTemplate = card.GetComponentsInChildren<Text>(true).First();
                var buttonTemplate = (Button)so.FindProperty("_startGame").objectReferenceValue;
                var theme = all.OfType<UiThemeView>().First(t => t.DeepSeek != null && t.Harness != null);
                var homeButton = CloneButton(buttonTemplate, home.transform, "图鉴", new Vector2(-455, -7), new Vector2(510, 64));
                MakeTheme(homeButton.gameObject, theme);
                var positions = new[] { "_startGame", "_shopButton", "_inventoryButton", "_achievementsButton", "_quitButton" };
                var y = new[] { 65f, -79f, -151f, -223f, -367f };
                for (int i = 0; i < positions.Length; i++)
                {
                    var button = (Button)so.FindProperty(positions[i]).objectReferenceValue;
                    ((RectTransform)button.transform).anchoredPosition = new Vector2(-455, y[i]);
                }
                var audio = home.transform.Find("AudioSettingsButton") as RectTransform;
                if (audio != null) audio.anchoredPosition = new Vector2(-455, -295);
                var page = Rect(home.transform.parent, "Bestiary", new Vector2(1400, 760), Vector2.zero);
                page.gameObject.SetActive(false);
                Label(page, textTemplate, "图鉴", new Vector2(700, 65), new Vector2(-310, 330), 42, TextAnchor.MiddleLeft);
                Label(page, textTemplate, "首版收录 · Kimi", new Vector2(400, 40), new Vector2(460, 330), 24, TextAnchor.MiddleRight);
                var portraitCard = Panel(page, art, "PortraitCard", new Vector2(520, 610), new Vector2(-390, -25));
                var image = Rect(portraitCard, "KimiPortrait", new Vector2(470, 470), new Vector2(0, 35)).gameObject.AddComponent<Image>();
                image.sprite = entry.Portrait; image.preserveAspect = true; image.raycastTarget = false;
                Label(portraitCard, textTemplate, "月夜试炼者", new Vector2(450, 45), new Vector2(0, -242), 30, TextAnchor.MiddleCenter);
                var detail = Panel(page, art, "DetailCard", new Vector2(760, 610), new Vector2(270, -25));
                var view = page.gameObject.AddComponent<BestiaryMenuView>(); view.Entry = entry; view.Portrait = image;
                view.NameLabel = Label(detail, textTemplate, entry.DisplayName, new Vector2(660, 60), new Vector2(0, 242), 44, TextAnchor.MiddleLeft);
                view.SubtitleLabel = Label(detail, textTemplate, entry.Subtitle, new Vector2(660, 40), new Vector2(0, 192), 24, TextAnchor.MiddleLeft);
                view.DescriptionLabel = Label(detail, textTemplate, entry.Description, new Vector2(660, 90), new Vector2(0, 123), 25, TextAnchor.UpperLeft);
                view.SkillsLabel = Label(detail, textTemplate, entry.SkillNotes, new Vector2(660, 260), new Vector2(0, -70), 24, TextAnchor.UpperLeft);
                view.ChallengeButton = CloneButton(buttonTemplate, detail, "快速挑战", new Vector2(0, -252), new Vector2(650, 66));
                view.ChallengeLabel = view.ChallengeButton.GetComponentInChildren<Text>(true);
                view.ChallengeLabel.text = "快速挑战 · 领取 1000 Token";
                MakeTheme(page.gameObject, theme);
                so.FindProperty("_bestiary").objectReferenceValue = page.gameObject;
                so.FindProperty("_bestiaryButton").objectReferenceValue = homeButton;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:NetworkTuningConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>(AssetDatabase.GUIDToAssetPath(guid));
                DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(config); EditorUtility.SetDirty(config);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene("Assets/Scenes/World01_EarlyInternet.unity");
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChapterRunHudView>(true)).Single();
            var hs = new SerializedObject(hud);
            var returnButton = (Button)hs.FindProperty("_returnButton").objectReferenceValue;
            hs.FindProperty("_returnButtonLabel").objectReferenceValue = returnButton.GetComponentInChildren<Text>(true);
            hs.FindProperty("_challengeReturnPosition").vector2Value = new Vector2(185, -202);
            hs.FindProperty("_challengeReturnSize").vector2Value = new Vector2(330, 72);
            if (hs.FindProperty("_retryButton").objectReferenceValue == null)
            {
                var retry = CloneButton(returnButton, returnButton.transform.parent, "重新挑战", new Vector2(-185, -202), new Vector2(330, 72));
                hs.FindProperty("_retryButton").objectReferenceValue = retry;
                retry.gameObject.SetActive(false);
                var theme = hud.GetComponentsInParent<UiThemeView>(true).FirstOrDefault(t => t.DeepSeek != null);
                if (theme == null) theme = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UiThemeView>(true)).First(t => t.DeepSeek != null);
                MakeTheme(retry.gameObject, theme);
            }
            hs.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && previous != scene.path) EditorSceneManager.OpenScene(previous);
            return "Kimi bestiary, themed menu, preparation and challenge settlement references saved; no new gameplay scene.";
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }

        private static RectTransform Panel(Transform parent, GameObject artwork, string name, Vector2 size, Vector2 position)
        {
            var panel = Rect(parent, name, size, position);
            Object.Instantiate(artwork, panel, false).name = "ThemeArtwork";
            return panel;
        }

        private static Text Label(Transform parent, Text template, string value, Vector2 size, Vector2 position, int fontSize, TextAnchor alignment)
        {
            var text = Rect(parent, "Label", size, position).gameObject.AddComponent<Text>();
            text.font = template.font; text.fontSize = fontSize; text.alignment = alignment; text.color = template.color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value; text.raycastTarget = false; return text;
        }

        private static Button CloneButton(Button template, Transform parent, string label, Vector2 position, Vector2 size)
        {
            var button = Object.Instantiate(template, parent, false); button.name = label;
            var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            button.onClick = new Button.ButtonClickedEvent(); button.GetComponentInChildren<Text>(true).text = label;
            return button;
        }

        private static void MakeTheme(GameObject root, UiThemeView template)
        {
            var theme = root.AddComponent<UiThemeView>(); theme.DeepSeek = template.DeepSeek; theme.Harness = template.Harness;
            var artwork = new List<UiThemeView.ArtworkBinding>();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            foreach (UiArtworkKind kind in Enum.GetValues(typeof(UiArtworkKind)))
                if (image.sprite != null && (image.sprite == theme.DeepSeek.GetArtwork(kind) || image.sprite == theme.Harness.GetArtwork(kind)))
                { artwork.Add(new UiThemeView.ArtworkBinding { Target = image, Kind = kind }); break; }
            theme.Artwork = artwork.ToArray();
            theme.Graphics = root.GetComponentsInChildren<Text>(true).Select(t => new UiThemeView.GraphicBinding
                { Target = t, Tone = UiThemeView.GraphicTone.Text }).ToArray();
        }
    }
}
