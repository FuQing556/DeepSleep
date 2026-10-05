using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式一次性装配生图 Buff；不修改数值、网络、按钮根矩形或玩法对象。</summary>
    public static class UiBuffArtworkInstaller
    {
        private const string ART = "Assets/_Project/Art/UI/Buffs/ICO_BUFF_";
        private const string CATALOG = "Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset";
        private static readonly string[] SCENES = { "Gameplay_Prototype", "World01_EarlyInternet" };

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before Buff artwork assembly.");
            foreach (string sceneName in SCENES)
            {
                Scene target = SceneManager.GetSceneByPath("Assets/Scenes/" + sceneName + ".unity");
                if (target.IsValid() && target.isLoaded && target.isDirty)
                    throw new InvalidOperationException("Save target scene changes before assembly: " + target.path);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CATALOG);
            if (catalog == null || !catalog.TryValidate(out _)) throw new InvalidOperationException("Existing upgrade catalog is invalid.");
            var sprites = new Dictionary<UpgradeCardId, Sprite>();
            foreach (var definition in catalog.Definitions)
                sprites.Add(definition.Id, Load(ART + definition.Id + ".png"));
            Sprite guard = Load(ART + "RiceGuard.png"), sword = Load(ART + "QuantumSword.png");
            Sprite protection = Load("Assets/_Project/Art/UI/Status/ICO_SH_ReviveProtection_v01.png");

            // 全部现役图标必须齐全才修改目录；保留现有定义、顺序与所有玩法参数。
            var catalogData = new SerializedObject(catalog);
            var definitions = catalogData.FindProperty("_definitions");
            for (int i = 0; i < definitions.arraySize; i++)
            {
                var definition = definitions.GetArrayElementAtIndex(i);
                var id = (UpgradeCardId)definition.FindPropertyRelative("_id").intValue;
                definition.FindPropertyRelative("_icon").objectReferenceValue = sprites[id];
            }
            catalogData.ApplyModifiedPropertiesWithoutUndo();
            Scene active = SceneManager.GetActiveScene();
            try
            {
                foreach (string sceneName in SCENES)
                {
                    string path = "Assets/Scenes/" + sceneName + ".unity";
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool loaded = scene.IsValid() && scene.isLoaded;
                    if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var states = All<PlayerUpgradeRuntimeState>(scene);
                        var shops = All<RestNodeUpgradePanelView>(scene);
                        var huds = All<PlayerCombatHudView>(scene);
                        if (states.Count != 1 || shops.Count != 1 || huds.Count != 2 || states[0].Catalog != catalog)
                            throw new InvalidOperationException("Expected one shared upgrade state/shop and two HUDs in " + path);
                        InstallShop(shops[0]);
                        foreach (var hud in huds) InstallHud(hud, states[0], guard, sword, protection);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
            return "Buff artwork installed in both scenes: 10 shared upgrade icons, DS 6 / HS 5 earned slots, active skill and protection icons. Gameplay values and button hit rectangles unchanged.";
        }

        private static void InstallShop(RestNodeUpgradePanelView shop)
        {
            var data = new SerializedObject(shop);
            var cards = data.FindProperty("_cardButtons");
            var titles = data.FindProperty("_cardTitles");
            var descriptions = data.FindProperty("_cardDescriptions");
            var icons = data.FindProperty("_cardIcons");
            icons.arraySize = cards.arraySize;
            for (int i = 0; i < cards.arraySize; i++)
            {
                var card = (Button)cards.GetArrayElementAtIndex(i).objectReferenceValue;
                var title = (Text)titles.GetArrayElementAtIndex(i).objectReferenceValue;
                var description = (Text)descriptions.GetArrayElementAtIndex(i).objectReferenceValue;
                if (card == null || title == null || description == null)
                    throw new InvalidOperationException("Incomplete upgrade card at " + shop.name + " [" + i + "]");
                var motion = card.GetComponent<UiButtonMotion>();
                if (motion == null || motion.Visual == null || motion.Visual == card.transform || !motion.Visual.IsChildOf(card.transform))
                    throw new InvalidOperationException("Upgrade card requires its existing visual motion root: " + card.name);
                Transform legacyIcon = card.transform.Find("UpgradeIcon");
                if (legacyIcon != null)
                {
                    if (motion.Visual.Find("UpgradeIcon") != null)
                        throw new InvalidOperationException("Duplicate UpgradeIcon layers on " + card.name);
                    // 从上一批卡片根迁入现有视觉根，复用原 Image 与引用，不重建第二张图。
                    Undo.SetTransformParent(legacyIcon, motion.Visual, "Move upgrade icon into button artwork");
                }
                Image icon = ImageChild(motion.Visual, "UpgradeIcon");
                Position(icon.rectTransform, new Vector2(.5f, 1), new Vector2(0, -76), new Vector2(96, 96));
                icon.enabled = false; // Show() 按真实候选写入，不伪造默认强化。
                icons.GetArrayElementAtIndex(i).objectReferenceValue = icon;
                Position(title.rectTransform, new Vector2(.5f, 1), new Vector2(0, -151), new Vector2(344, 46));
                title.fontSize = 25; title.alignment = TextAnchor.MiddleCenter;
                Position(description.rectTransform, new Vector2(.5f, 1), new Vector2(0, -270), new Vector2(344, 186));
                description.fontSize = 21; description.alignment = TextAnchor.UpperLeft;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallHud(PlayerCombatHudView hud, PlayerUpgradeRuntimeState state,
            Sprite guard, Sprite sword, Sprite protection)
        {
            // SourceComponent 可能是网络适配器；角色身份取显式装配在同一 HUD 的原本地源。
            var source = hud.GetComponent<PlayerCombatHudSource>();
            if (source == null) throw new InvalidOperationException("HUD has no explicit local source: " + hud.name);
            bool ds = source.Role == PlayerRole.DeepSeek;
            var panel = (RectTransform)hud.transform;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 230);
            Position(hud.HealthLabel.rectTransform, new Vector2(0, 1), new Vector2(112, -59), new Vector2(176, 25), new Vector2(0, .5f));
            Position(hud.WeaponLabel.rectTransform, new Vector2(0, 1), new Vector2(292, -59), new Vector2(164, 25), new Vector2(0, .5f));
            hud.WeaponLabel.fontSize = 18; hud.WeaponLabel.alignment = TextAnchor.MiddleRight;
            Position(hud.SkillLabel.rectTransform, new Vector2(0, 1), new Vector2(46, -143), new Vector2(410, 26), new Vector2(0, .5f));
            hud.ActiveSkillIcon = ImageChild(panel, "ActiveSkillIcon");
            Position(hud.ActiveSkillIcon.rectTransform, new Vector2(0, 1), new Vector2(25, -143), new Vector2(28, 28));
            hud.ActiveSkillIcon.sprite = ds ? guard : sword;
            hud.ActiveSkillIcon.enabled = false;
            hud.ProtectionIcon.sprite = protection;
            hud.ProtectionIcon.type = Image.Type.Simple; hud.ProtectionIcon.preserveAspect = true;
            hud.ProtectionIcon.raycastTarget = false; hud.ProtectionIcon.enabled = false;

            RectTransform row = RectChild(panel, "EarnedUpgrades");
            Position(row, new Vector2(0, 1), new Vector2(18, -186), new Vector2(444, 38), new Vector2(0, .5f));
            var view = row.GetComponent<PlayerUpgradeHudView>();
            if (view == null) view = Undo.AddComponent<PlayerUpgradeHudView>(row.gameObject);
            view.State = state; view.Role = source.Role;
            int count = 0;
            foreach (var definition in state.Catalog.Definitions) if (definition.Supports(source.Role)) count++;
            view.Icons = new Image[count]; view.Ranks = new Text[count];
            for (int i = 0; i < count; i++)
            {
                Image icon = ImageChild(row, "Slot_" + i);
                Position(icon.rectTransform, new Vector2(0, .5f), new Vector2(19 + i * 52, 0), new Vector2(38, 38));
                RectTransform rankRect = RectChild(icon.transform, "Rank");
                var rank = rankRect.GetComponent<Text>();
                if (rank == null)
                {
                    if (rankRect.GetComponent<CanvasRenderer>() == null) Undo.AddComponent<CanvasRenderer>(rankRect.gameObject);
                    rank = Undo.AddComponent<Text>(rankRect.gameObject);
                }
                rank.font = hud.HealthLabel.font; rank.fontSize = 18; rank.fontStyle = FontStyle.Bold;
                rank.alignment = TextAnchor.LowerRight; rank.color = Color.white;
                rank.raycastTarget = false; rank.text = "";
                Position(rankRect, new Vector2(1, 0), new Vector2(2, -1), new Vector2(20, 22), new Vector2(1, 0));
                var shadow = rank.GetComponent<Shadow>();
                if (shadow == null) shadow = Undo.AddComponent<Shadow>(rank.gameObject);
                shadow.effectColor = new Color(0, 0, 0, .9f); shadow.effectDistance = new Vector2(1, -1);
                view.Icons[i] = icon; view.Ranks[i] = rank;
                icon.gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(view); EditorUtility.SetDirty(hud);
        }

        private static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Import production image as Sprite first: " + path);
            return sprite;
        }

        private static RectTransform RectChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return (RectTransform)existing;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Install Buff artwork");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image ImageChild(Transform parent, string name)
        {
            RectTransform rect = RectChild(parent, name);
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                if (rect.GetComponent<CanvasRenderer>() == null) Undo.AddComponent<CanvasRenderer>(rect.gameObject);
                image = Undo.AddComponent<Image>(rect.gameObject);
            }
            image.color = Color.white; image.type = Image.Type.Simple;
            image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        private static void Position(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot ?? new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        }

        private static List<T> All<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }
    }
}
