using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class RestNodeUpgradeSceneInstaller
    {
        private const string CatalogPath =
            "Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset";

        [MenuItem("DeepSleep/设置/装配休息节点三选一强化")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式装配。");
            }

            RestNodePrototypeController2D node = One<RestNodePrototypeController2D>();
            if (node.GetComponent<RestNodeUpgradeController>() != null)
            {
                throw new InvalidOperationException("三选一强化已经装配，未重复创建。");
            }

            UpgradeCatalog catalog = CreateOrUpdateCatalog();
            PlayerControlAssignment assignment = One<PlayerControlAssignment>();
            CoopSessionController session = One<CoopSessionController>();
            Canvas canvas = FindNamed<Canvas>("UI_CombatHUD");
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
            }
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(
                    FindObjectsInactive.Include) == null)
            {
                throw new InvalidOperationException("场景缺少 EventSystem，未装配伪按钮。");
            }

            RectTransform safe = FindChild(canvas.transform, "SafeArea")
                as RectTransform;
            if (safe == null)
            {
                throw new InvalidOperationException("UI_CombatHUD 下缺少 SafeArea。");
            }

            RestNodeUpgradePanelView panel = BuildPanel(safe);
            PlayerUpgradeRuntimeState runtimeState =
                Undo.AddComponent<PlayerUpgradeRuntimeState>(node.gameObject);
            RestNodeUpgradeController upgradeController =
                Undo.AddComponent<RestNodeUpgradeController>(node.gameObject);

            SetReference(runtimeState, "_catalog", catalog);
            SetReference(upgradeController, "_runtimeState", runtimeState);
            SetReference(upgradeController, "_panel", panel);
            SetReference(upgradeController, "_controlAssignment", assignment);
            SetReference(upgradeController, "_session", session);
            SetReference(node, "_upgradeController", upgradeController);

            DeepSeekRiceAutoShooter dsWeapon = One<DeepSeekRiceAutoShooter>();
            HarnessTerminalLaserController hsWeapon =
                One<HarnessTerminalLaserController>();
            SetReference(dsWeapon, "_upgradeState", runtimeState);
            SetReference(hsWeapon, "_upgradeState", runtimeState);

            EditorSceneManager.MarkSceneDirty(node.gameObject.scene);
            EditorSceneManager.SaveScene(node.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[RestNodeUpgrade] 已装配角色独立三选一、每节点一次免费刷新、" +
                "主机校验同步，以及首批伤害/攻速强化。");
        }

        private static UpgradeCatalog CreateOrUpdateCatalog()
        {
            const string directory = "Assets/_Project/Configs/Progression";
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project/Configs",
                    "Progression");
            }

            UpgradeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            Undo.RecordObject(catalog, "Configure upgrade catalog");
            catalog.SetDefinitions(new[]
            {
                new UpgradeDefinition(
                    UpgradeCardId.DataCompression,
                    "数据压缩",
                    "当前角色的所有基础武器伤害 +15%。",
                    PlayerRoleMask.Both,
                    UpgradeOwnershipScope.Role,
                    5,
                    12,
                    3,
                    UpgradeEffectKind.WeaponDamage,
                    0.15f),
                new UpgradeDefinition(
                    UpgradeCardId.RuntimeOverclock,
                    "运行时超频",
                    "当前角色的基础攻击频率 +12%。",
                    PlayerRoleMask.Both,
                    UpgradeOwnershipScope.Role,
                    5,
                    12,
                    3,
                    UpgradeEffectKind.AttackRate,
                    0.12f),
                new UpgradeDefinition(
                    UpgradeCardId.GiantRiceBall,
                    "大饭团",
                    "DS 饭团伤害 +25%；后续等级将扩展小范围溅射。",
                    PlayerRoleMask.DeepSeek,
                    UpgradeOwnershipScope.Role,
                    5,
                    15,
                    4,
                    UpgradeEffectKind.WeaponDamage,
                    0.25f),
                new UpgradeDefinition(
                    UpgradeCardId.RiceStorm,
                    "米粒风暴",
                    "DS 饭团发射频率 +18%。",
                    PlayerRoleMask.DeepSeek,
                    UpgradeOwnershipScope.Role,
                    5,
                    15,
                    4,
                    UpgradeEffectKind.AttackRate,
                    0.18f),
                new UpgradeDefinition(
                    UpgradeCardId.TerminalAmplifier,
                    "终端增幅",
                    "HS 贯穿激光伤害 +25%。",
                    PlayerRoleMask.Harness,
                    UpgradeOwnershipScope.Role,
                    5,
                    15,
                    4,
                    UpgradeEffectKind.WeaponDamage,
                    0.25f),
                new UpgradeDefinition(
                    UpgradeCardId.CoolingCircuit,
                    "冷却回路",
                    "HS 激光冷却速度 +18%。",
                    PlayerRoleMask.Harness,
                    UpgradeOwnershipScope.Role,
                    5,
                    15,
                    4,
                    UpgradeEffectKind.AttackRate,
                    0.18f)
            });
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static RestNodeUpgradePanelView BuildPanel(RectTransform parent)
        {
            RectTransform panelRect = Rect("RestNodeUpgradePanel", parent);
            panelRect.anchorMin = new Vector2(0.08f, 0.12f);
            panelRect.anchorMax = new Vector2(0.92f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image background = Undo.AddComponent<Image>(panelRect.gameObject);
            background.color = new Color(0.015f, 0.025f, 0.06f, 0.96f);
            CanvasGroup group = Undo.AddComponent<CanvasGroup>(panelRect.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Text title = Label(panelRect, "Title", 34, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, 0.04f, 0.87f, 0.96f, 0.98f);
            Text status = Label(panelRect, "Status", 22, TextAnchor.MiddleCenter);
            status.color = new Color(0.6f, 0.85f, 1f);
            Anchor(status.rectTransform, 0.06f, 0.78f, 0.94f, 0.87f);

            var buttons = new Button[3];
            var titles = new Text[3];
            var descriptions = new Text[3];
            for (int index = 0; index < 3; index++)
            {
                float left = 0.035f + index * 0.325f;
                RectTransform card = Rect($"Card_{index + 1}", panelRect);
                Anchor(card, left, 0.20f, left + 0.295f, 0.76f);
                Image cardImage = Undo.AddComponent<Image>(card.gameObject);
                cardImage.color = index switch
                {
                    0 => new Color(0.08f, 0.23f, 0.38f, 0.96f),
                    1 => new Color(0.18f, 0.13f, 0.35f, 0.96f),
                    _ => new Color(0.32f, 0.09f, 0.18f, 0.96f)
                };
                buttons[index] = Undo.AddComponent<Button>(card.gameObject);
                buttons[index].targetGraphic = cardImage;
                titles[index] = Label(card, "Name", 27, TextAnchor.MiddleCenter);
                Anchor(titles[index].rectTransform, 0.06f, 0.65f, 0.94f, 0.94f);
                descriptions[index] = Label(
                    card, "Description", 21, TextAnchor.UpperLeft);
                descriptions[index].horizontalOverflow =
                    HorizontalWrapMode.Wrap;
                descriptions[index].verticalOverflow =
                    VerticalWrapMode.Truncate;
                Anchor(descriptions[index].rectTransform, 0.09f, 0.10f, 0.91f, 0.62f);
            }

            Button refresh = SimpleButton(
                panelRect, "Refresh", "刷新（本节点免费 1 次)",
                0.07f, 0.045f, 0.47f, 0.16f,
                out Text refreshLabel);
            Button close = SimpleButton(
                panelRect, "Close", "关闭商店", 0.68f, 0.045f, 0.93f, 0.16f,
                out _);

            RestNodeUpgradePanelView view =
                Undo.AddComponent<RestNodeUpgradePanelView>(panelRect.gameObject);
            SetReference(view, "_panel", group);
            SetReference(view, "_title", title);
            SetReference(view, "_status", status);
            SetArray(view, "_cardButtons", buttons);
            SetArray(view, "_cardTitles", titles);
            SetArray(view, "_cardDescriptions", descriptions);
            SetReference(view, "_refreshButton", refresh);
            SetReference(view, "_refreshLabel", refreshLabel);
            SetReference(view, "_closeButton", close);
            return view;
        }

        private static Button SimpleButton(
            RectTransform parent,
            string name,
            string value,
            float minX,
            float minY,
            float maxX,
            float maxY,
            out Text label)
        {
            RectTransform rect = Rect(name, parent);
            Anchor(rect, minX, minY, maxX, maxY);
            Image image = Undo.AddComponent<Image>(rect.gameObject);
            image.color = new Color(0.12f, 0.34f, 0.55f, 1f);
            Button button = Undo.AddComponent<Button>(rect.gameObject);
            button.targetGraphic = image;
            label = Label(rect, "Label", 21, TextAnchor.MiddleCenter);
            label.text = value;
            Anchor(label.rectTransform, 0f, 0f, 1f, 1f);
            return button;
        }

        private static Text Label(
            RectTransform parent,
            string name,
            int size,
            TextAnchor alignment)
        {
            RectTransform rect = Rect(name, parent);
            Text text = Undo.AddComponent<Text>(rect.gameObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, "Install upgrade UI");
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        private static void Anchor(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static T FindNamed<T>(string name) where T : Component
        {
            foreach (T item in All<T>())
            {
                if (item.name == name)
                {
                    return item;
                }
            }
            throw new InvalidOperationException($"未找到 {name} ({typeof(T).Name})。");
        }

        private static T One<T>() where T : Component
        {
            List<T> found = All<T>();
            if (found.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 必须恰有一个，当前为 {found.Count} 个。");
            }
            return found[0];
        }

        private static List<T> All<T>() where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in
                     EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return result;
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} 缺少字段 {propertyName}。");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetArray<T>(
            UnityEngine.Object target,
            string propertyName,
            T[] values) where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
            serialized.ApplyModifiedProperties();
        }
    }
}
