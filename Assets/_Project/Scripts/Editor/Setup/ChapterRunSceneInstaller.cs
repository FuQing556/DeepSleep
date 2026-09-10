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
        private const string ChannelFolder =
            "Assets/_Project/Configs/Combat/Enemies/SpawnChannels";
        private const string WindowChannelPath = ChannelFolder +
            "/CFG_EN_SpawnChannel_404Window.asset";
        private const string SnakeChannelPath = ChannelFolder +
            "/CFG_EN_SpawnChannel_DataCrawlerSnake.asset";

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
            SetArray(controller, "_spawnDirectors",
                All<EnemySpawnDirector2D>().ToArray());
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

            UpgradePrototypeSettlement();

            EditorSceneManager.MarkSceneDirty(node.gameObject.scene);
            EditorSceneManager.SaveScene(node.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[ChapterRun] 已装配三段差异化战斗、" +
                "失败检查点与原型结算。");
        }

        [MenuItem("DeepSleep/设置/升级三段差异化原型")]
        public static void UpgradePrototypeSettlement()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式装配。");
            }

            ChapterRunConfig config = LoadOrCreateConfig();
            EnemySpawnChannelDefinition windowChannel = LoadOrCreateChannel(
                WindowChannelPath,
                "404 漂流窗口");
            EnemySpawnChannelDefinition snakeChannel = LoadOrCreateChannel(
                SnakeChannelPath,
                "数据爬虫机械蛇");
            ConfigurePrototypeSegments(config, windowChannel, snakeChannel);

            List<EnemySpawnDirector2D> directors = All<EnemySpawnDirector2D>();
            for (int index = 0; index < directors.Count; index++)
            {
                SerializedObject directorObject =
                    new SerializedObject(directors[index]);
                SerializedProperty schedule =
                    directorObject.FindProperty("_schedule");
                string schedulePath = schedule.objectReferenceValue == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(schedule.objectReferenceValue);
                EnemySpawnChannelDefinition channel =
                    schedulePath.Contains("DataCrawlerSnake")
                        ? snakeChannel
                        : schedulePath.Contains("404Window")
                            ? windowChannel
                            : null;
                if (channel == null)
                {
                    throw new InvalidOperationException(
                        $"无法识别刷怪器 {directors[index].name} 的频道。");
                }
                directorObject.FindProperty("_channel").objectReferenceValue =
                    channel;
                directorObject.ApplyModifiedPropertiesWithoutUndo();
            }

            ChapterRunController controller = One<ChapterRunController>();
            SetArray(controller, "_spawnDirectors", directors.ToArray());

            ChapterRunHudView hud = One<ChapterRunHudView>();
            Transform existing = FindChild(hud.transform.parent,
                "ChapterSettlementPanel");
            if (existing == null)
            {
                BuildSettlementPanel(hud, hud.transform.parent as RectTransform);
            }
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[ChapterRun] 已配置：漂流碎屑 → 爬虫接入 → 数据拥塞。");
        }

        private static EnemySpawnChannelDefinition LoadOrCreateChannel(
            string path,
            string displayName)
        {
            EnsureFolder(ChannelFolder);
            EnemySpawnChannelDefinition channel =
                AssetDatabase.LoadAssetAtPath<EnemySpawnChannelDefinition>(path);
            if (channel == null)
            {
                channel = ScriptableObject.CreateInstance<
                    EnemySpawnChannelDefinition>();
                AssetDatabase.CreateAsset(channel, path);
            }

            SerializedObject serialized = new SerializedObject(channel);
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(channel);
            return channel;
        }

        private static void ConfigurePrototypeSegments(
            ChapterRunConfig config,
            EnemySpawnChannelDefinition windowChannel,
            EnemySpawnChannelDefinition snakeChannel)
        {
            SerializedObject serialized = new SerializedObject(config);
            SerializedProperty segments = serialized.FindProperty("_segments");
            segments.arraySize = 3;
            ConfigureSegment(
                segments.GetArrayElementAtIndex(0),
                "漂流碎屑", 45f, 10,
                windowChannel, true, 0.8f, 1.15f, 5,
                snakeChannel, false, 0f, 1f, 1);
            ConfigureSegment(
                segments.GetArrayElementAtIndex(1),
                "爬虫接入", 55f, 12,
                windowChannel, true, 0.8f, 2.2f, 3,
                snakeChannel, true, 2.5f, 1f, 3);
            ConfigureSegment(
                segments.GetArrayElementAtIndex(2),
                "数据拥塞", 60f, 16,
                windowChannel, true, 0.35f, 0.85f, 7,
                snakeChannel, true, 1.5f, 0.75f, 4);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureSegment(
            SerializedProperty segment,
            string displayName,
            float duration,
            int defeats,
            EnemySpawnChannelDefinition firstChannel,
            bool firstEnabled,
            float firstDelay,
            float firstIntervalMultiplier,
            int firstMaximumAlive,
            EnemySpawnChannelDefinition secondChannel,
            bool secondEnabled,
            float secondDelay,
            float secondIntervalMultiplier,
            int secondMaximumAlive)
        {
            segment.FindPropertyRelative("_displayName").stringValue = displayName;
            segment.FindPropertyRelative("_durationSeconds").floatValue = duration;
            segment.FindPropertyRelative("_requiredDefeats").intValue = defeats;
            SerializedProperty rules = segment.FindPropertyRelative("_spawnRules");
            rules.arraySize = 2;
            ConfigureRule(
                rules.GetArrayElementAtIndex(0),
                firstChannel,
                firstEnabled,
                firstDelay,
                firstIntervalMultiplier,
                firstMaximumAlive);
            ConfigureRule(
                rules.GetArrayElementAtIndex(1),
                secondChannel,
                secondEnabled,
                secondDelay,
                secondIntervalMultiplier,
                secondMaximumAlive);
        }

        private static void ConfigureRule(
            SerializedProperty rule,
            EnemySpawnChannelDefinition channel,
            bool enabled,
            float delay,
            float intervalMultiplier,
            int maximumAlive)
        {
            rule.FindPropertyRelative("_channel").objectReferenceValue = channel;
            rule.FindPropertyRelative("_enabled").boolValue = enabled;
            rule.FindPropertyRelative("_initialDelaySeconds").floatValue = delay;
            rule.FindPropertyRelative("_intervalMultiplier").floatValue =
                intervalMultiplier;
            rule.FindPropertyRelative("_maximumAliveCount").intValue =
                maximumAlive;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
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
            buttonLabel.text = "返回关卡选择";
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
