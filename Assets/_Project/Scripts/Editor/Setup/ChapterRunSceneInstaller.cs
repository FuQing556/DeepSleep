using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class ChapterRunSceneInstaller
    {
        private const string ConfigFolder =
            "Assets/_Project/Configs/Progression";
        private const string ConfigPath = ConfigFolder +
            "/CFG_ChapterRun_Prototype.asset";

        [MenuItem("DeepSleep/设置/装配章节循环与失败检查点")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式装配。");
            }

            RestNodePrototypeController2D node =
                One<RestNodePrototypeController2D>();
            if (node.GetComponent<ChapterRunController>() != null)
            {
                throw new InvalidOperationException(
                    "章节循环已经装配，未重复创建。");
            }

            ChapterRunConfig config = LoadOrCreateConfig();
            ChapterRunHudView hud = BuildHud();
            ChapterRunController controller =
                Undo.AddComponent<ChapterRunController>(node.gameObject);

            SetReference(controller, "_config", config);
            SetReference(controller, "_restNode", node);
            SetReference(controller, "_upgradeController",
                One<RestNodeUpgradeController>());
            SetReference(controller, "_selection",
                One<OpeningCharacterSelectionController>());
            SetReference(controller, "_session", One<CoopSessionController>());
            SetReference(controller, "_hud", hud);
            SetArray(controller, "_enemyPools",
                All<EnemyActorPool2D>().ToArray());

            PlayerLifeStateController2D deepSeek = null;
            PlayerLifeStateController2D harness = null;
            foreach (PlayerLifeStateController2D life in
                     All<PlayerLifeStateController2D>())
            {
                PlayerActor actor = life.GetComponentInParent<PlayerActor>();
                if (actor == null) continue;
                if (actor.Definition.Role == PlayerRole.DeepSeek)
                    deepSeek = life;
                else if (actor.Definition.Role == PlayerRole.Harness)
                    harness = life;
            }
            if (deepSeek == null || harness == null)
            {
                throw new InvalidOperationException(
                    "无法按角色找到 DS/HS 生命周期组件。");
            }
            SetReference(controller, "_deepSeekLife", deepSeek);
            SetReference(controller, "_harnessLife", harness);

            EditorSceneManager.MarkSceneDirty(node.gameObject.scene);
            EditorSceneManager.SaveScene(node.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[ChapterRun] 已装配 60 秒战斗、10 杀任务、" +
                "10 秒单人数据丢失、2 秒全队失败与节点检查点回滚。");
        }

        [MenuItem("DeepSleep/设置/升级三段原型结算页")]
        public static void UpgradePrototypeSettlement()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式装配。");
            }

            ChapterRunConfig config = LoadOrCreateConfig();
            SerializedObject configObject = new SerializedObject(config);
            configObject.FindProperty("_combatSegmentCount").intValue = 3;
            configObject.ApplyModifiedPropertiesWithoutUndo();

            ChapterRunHudView hud = One<ChapterRunHudView>();
            Transform existing = FindChild(hud.transform.parent,
                "ChapterSettlementPanel");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            BuildSettlementPanel(hud, hud.transform.parent as RectTransform);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChapterRun] 已升级为三段原型闭环并装配结算页。");
        }

        private static ChapterRunConfig LoadOrCreateConfig()
        {
            ChapterRunConfig config =
                AssetDatabase.LoadAssetAtPath<ChapterRunConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }
            if (!AssetDatabase.IsValidFolder(ConfigFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project/Configs",
                    "Progression");
            }
            config = ScriptableObject.CreateInstance<ChapterRunConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static ChapterRunHudView BuildHud()
        {
            Canvas canvas = FindNamed<Canvas>("UI_CombatHUD");
            RectTransform safe = FindChild(canvas.transform, "SafeArea")
                as RectTransform;
            if (safe == null)
            {
                throw new InvalidOperationException(
                    "UI_CombatHUD 下缺少 SafeArea。");
            }

            RectTransform root = Rect("ChapterRunHUD", safe);
            root.anchorMin = new Vector2(0.31f, 0.77f);
            root.anchorMax = new Vector2(0.69f, 0.865f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            Image background = Undo.AddComponent<Image>(root.gameObject);
            background.color = new Color(0.02f, 0.035f, 0.07f, 0.78f);
            background.raycastTarget = false;
            CanvasGroup group = Undo.AddComponent<CanvasGroup>(root.gameObject);

            RectTransform labelRect = Rect("Label", root);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-12f, -4f);
            Text label = Undo.AddComponent<Text>(labelRect.gameObject);
            label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            ChapterRunHudView view =
                Undo.AddComponent<ChapterRunHudView>(root.gameObject);
            SetReference(view, "_panel", group);
            SetReference(view, "_label", label);
            return view;
        }

        private static void BuildSettlementPanel(
            ChapterRunHudView view,
            RectTransform parent)
        {
            if (parent == null)
            {
                throw new InvalidOperationException("结算页缺少 SafeArea 父节点。");
            }

            RectTransform panel = Rect("ChapterSettlementPanel", parent);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.SetAsLastSibling();

            Image shade = Undo.AddComponent<Image>(panel.gameObject);
            shade.color = new Color(0.015f, 0.025f, 0.065f, 0.9f);
            shade.raycastTarget = true;
            CanvasGroup group = Undo.AddComponent<CanvasGroup>(panel.gameObject);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform card = Rect("SummaryCard", panel);
            card.anchorMin = new Vector2(0.3f, 0.23f);
            card.anchorMax = new Vector2(0.7f, 0.77f);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            Image cardImage = Undo.AddComponent<Image>(card.gameObject);
            cardImage.color = new Color(0.04f, 0.09f, 0.18f, 0.96f);
            cardImage.raycastTarget = true;

            RectTransform labelRect = Rect("Summary", card);
            labelRect.anchorMin = new Vector2(0.08f, 0.25f);
            labelRect.anchorMax = new Vector2(0.92f, 0.9f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = Undo.AddComponent<Text>(labelRect.gameObject);
            label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            RectTransform buttonRect = Rect("ReturnToOpening", card);
            buttonRect.anchorMin = new Vector2(0.24f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.76f, 0.22f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            Image buttonImage = Undo.AddComponent<Image>(buttonRect.gameObject);
            buttonImage.color = new Color(0.15f, 0.48f, 0.78f, 1f);
            Button button = Undo.AddComponent<Button>(buttonRect.gameObject);
            button.targetGraphic = buttonImage;

            RectTransform buttonLabelRect = Rect("Label", buttonRect);
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = Vector2.zero;
            buttonLabelRect.offsetMax = Vector2.zero;
            Text buttonLabel = Undo.AddComponent<Text>(
                buttonLabelRect.gameObject);
            buttonLabel.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            buttonLabel.fontSize = 24;
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            buttonLabel.color = Color.white;
            buttonLabel.text = "返回开局";
            buttonLabel.raycastTarget = false;

            SetReference(view, "_settlementPanel", group);
            SetReference(view, "_settlementLabel", label);
            SetReference(view, "_returnButton", button);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, "Install chapter run");
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private static T FindNamed<T>(string name) where T : Component
        {
            foreach (T item in All<T>())
            {
                if (item.name == name) return item;
            }
            throw new InvalidOperationException(
                $"未找到 {name} ({typeof(T).Name})。");
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
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.name} 缺少字段 {propertyName}。");
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
