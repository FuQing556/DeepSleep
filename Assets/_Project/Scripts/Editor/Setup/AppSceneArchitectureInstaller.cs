using System;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class AppSceneArchitectureInstaller
    {
        private const string BootPath = "Assets/Scenes/Boot.unity";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath =
            "Assets/Scenes/Gameplay_Prototype.unity";
        private const string LevelPath =
            "Assets/_Project/Configs/Progression/Meta/" +
            "CFG_META_Level_PrototypeSky.asset";
        private const string CardPrefabPath =
            "Assets/_Project/Prefabs/UI/Meta/PF_UI_MetaProductCard.prefab";
        private const string ProductFolder =
            "Assets/_Project/Configs/Progression/Meta/Products";
        private const string AchievementFolder =
            "Assets/_Project/Configs/Progression/Meta/Achievements";
        private const string AchievementCardPrefabPath =
            "Assets/_Project/Prefabs/UI/Meta/PF_UI_AchievementCard.prefab";

        [MenuItem("DeepSleep/设置/拆分 Boot、主菜单与玩法场景")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("请在编辑模式装配。");

            EditorSceneManager.SaveOpenScenes();
            EnsureFolder(AchievementFolder);
            AchievementDefinition[] achievements = BuildAchievements();
            BuildAchievementCard();
            BuildBootScene(achievements);
            BuildMainMenuScene();
            UpgradeGameplayScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[AppScenes] Boot → MainMenu → Gameplay_Prototype 已装配完成。");
        }

        private static void BuildBootScene(AchievementDefinition[] achievements)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            BuildCameraAndLight();

            GameObject root = new GameObject("AppRoot");
            LocalPlayerProfileStore profile =
                root.AddComponent<LocalPlayerProfileStore>();
            GameLaunchContext context = root.AddComponent<GameLaunchContext>();
            GameSceneRouter router = root.AddComponent<GameSceneRouter>();
            AchievementService achievementService =
                root.AddComponent<AchievementService>();
            GameAppRoot app = root.AddComponent<GameAppRoot>();
            SetReference(router, "_launchContext", context);
            SetReference(app, "_profile", profile);
            SetReference(app, "_launchContext", context);
            SetReference(app, "_sceneRouter", router);
            SetReference(app, "_achievements", achievementService);
            SetReference(achievementService, "_profile", profile);
            SetArray(achievementService, "_definitions", achievements);

            RectTransform toastCanvasRoot = Rect("UI_AchievementToast", root.transform);
            Canvas toastCanvas = toastCanvasRoot.gameObject.AddComponent<Canvas>();
            toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            toastCanvas.sortingOrder = 5000;
            CanvasScaler toastScaler =
                toastCanvasRoot.gameObject.AddComponent<CanvasScaler>();
            toastScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            toastScaler.referenceResolution = new Vector2(1920, 1080);
            toastScaler.matchWidthOrHeight = 0.5f;
            toastCanvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform toastSafe = Rect("SafeArea", toastCanvasRoot);
            Stretch(toastSafe);
            SafeAreaRectFitter toastFitter =
                toastSafe.gameObject.AddComponent<SafeAreaRectFitter>();
            SetReference(toastFitter, "_target", toastSafe);
            RectTransform toastCard = Rect("AchievementToast", toastSafe);
            toastCard.anchorMin = toastCard.anchorMax = toastCard.pivot =
                new Vector2(1f, 0f);
            toastCard.anchoredPosition = new Vector2(-70f, 70f);
            toastCard.sizeDelta = new Vector2(520f, 112f);
            Image toastBackground = toastCard.gameObject.AddComponent<Image>();
            toastBackground.color = new Color(0.035f, 0.075f, 0.14f, 0.96f);
            toastBackground.raycastTarget = false;
            CanvasGroup toastGroup = toastCard.gameObject.AddComponent<CanvasGroup>();
            Text toastTitle = Label("成就解锁", toastCard,
                new Vector2(0f, 23f), new Vector2(470f, 42f), 27);
            Text toastDescription = Label(string.Empty, toastCard,
                new Vector2(0f, -22f), new Vector2(470f, 38f), 21);
            AchievementToastView toast =
                toastCard.gameObject.AddComponent<AchievementToastView>();
            SetReference(toast, "_service", achievementService);
            SetReference(toast, "_group", toastGroup);
            SetReference(toast, "_card", toastCard);
            SetReference(toast, "_title", toastTitle);
            SetReference(toast, "_description", toastDescription);
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            BuildCameraAndLight();
            BuildEventSystem();

            RectTransform root = Rect("UI_MainMenu", null);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            MainMenuController menu = root.gameObject.AddComponent<
                MainMenuController>();
            WhaleMetaMenuController meta = root.gameObject.AddComponent<
                WhaleMetaMenuController>();

            RectTransform background = Rect("Background", root);
            Stretch(background);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.025f, 0.05f, 0.105f, 1f);

            RectTransform safe = Rect("SafeArea", root);
            Stretch(safe);
            SafeAreaRectFitter fitter =
                safe.gameObject.AddComponent<SafeAreaRectFitter>();
            SetReference(fitter, "_target", safe);

            RectTransform home = Panel("Home", safe);
            Label("DeepSleep", home, new Vector2(0, 190),
                new Vector2(800, 100), 64);
            Label("A journey for two", home, new Vector2(0, 105),
                new Vector2(800, 55), 28);
            Button start = Button("开始游戏", home,
                new Vector2(0, 45), new Vector2(540, 72));
            Button shopButton = Button("鲸元券商店", home,
                new Vector2(0, -45), new Vector2(540, 72));
            Button inventoryButton = Button("背包", home,
                new Vector2(0, -135), new Vector2(540, 72));
            Button achievementsButton = Button("成就", home,
                new Vector2(0, -225), new Vector2(540, 72));

            RectTransform levels = Panel("LevelSelection", safe);
            Label("选择关卡", levels, new Vector2(0, 275),
                new Vector2(900, 70), 44);
            Text levelBalance = Label("鲸元券：0", levels,
                new Vector2(360, 275), new Vector2(300, 55), 24);
            RectTransform levelCard = Card(levels,
                new Vector2(0, 70), new Vector2(920, 250));
            Label("天空测试场", levelCard, new Vector2(0, 70),
                new Vector2(820, 55), 36);
            Label("三段战斗原型 · 首通 10 / 重复 5 鲸元券",
                levelCard, new Vector2(0, 15),
                new Vector2(820, 45), 23);
            Button prototype = Button("进入关卡", levelCard,
                new Vector2(0, -65), new Vector2(360, 62));
            Button future = Button("后续关卡 · 暂未实现", levels,
                new Vector2(0, -150), new Vector2(920, 105));
            future.interactable = false;

            RectTransform modes = Panel("ModeSelection", safe);
            Label("选择游玩方式", modes, new Vector2(0, 190),
                new Vector2(800, 70), 44);
            Button solo = Button("单人游戏", modes,
                new Vector2(0, 45), new Vector2(540, 90));
            Button online = Button("在线联机", modes,
                new Vector2(0, -75), new Vector2(540, 90));

            RectTransform shop = Panel("WhaleVoucherShop", safe);
            Label("鲸元券商店", shop, new Vector2(0, 300),
                new Vector2(700, 70), 44);
            Text shopBalance = Label("鲸元券：0", shop,
                new Vector2(355, 300), new Vector2(300, 55), 24);
            Transform shopContent = Content("Products", shop,
                new Vector2(0, 15));
            Text feedback = Label(string.Empty, shop,
                new Vector2(0, -300), new Vector2(900, 45), 22);

            RectTransform inventory = Panel("Inventory", safe);
            Label("背包", inventory, new Vector2(0, 300),
                new Vector2(700, 70), 44);
            Text inventoryBalance = Label("鲸元券：0", inventory,
                new Vector2(355, 300), new Vector2(300, 55), 24);
            Transform inventoryContent = Content("OwnedProducts", inventory,
                new Vector2(0, 15));
            Text inventoryEmpty = Label("背包里暂时没有物品", inventory,
                new Vector2(0, 25), new Vector2(700, 55), 25);

            RectTransform achievements = Panel("Achievements", safe);
            Label("成就", achievements, new Vector2(0, 320),
                new Vector2(700, 65), 44);
            Text achievementSummary = Label("已解锁 0/5", achievements,
                new Vector2(355, 320), new Vector2(300, 50), 24);
            Transform achievementContent = Content("AchievementCards",
                achievements, new Vector2(0, -5));
            ((RectTransform)achievementContent).sizeDelta =
                new Vector2(920, 570);
            VerticalLayoutGroup achievementLayout =
                achievementContent.GetComponent<VerticalLayoutGroup>();
            achievementLayout.spacing = 10f;
            AchievementMenuController achievementMenu =
                achievements.gameObject.AddComponent<AchievementMenuController>();
            AchievementCardView achievementCard = AssetDatabase
                .LoadAssetAtPath<GameObject>(AchievementCardPrefabPath)
                .GetComponent<AchievementCardView>();
            SetReference(achievementMenu, "_cardPrefab", achievementCard);
            SetReference(achievementMenu, "_content", achievementContent);
            SetReference(achievementMenu, "_summary", achievementSummary);

            Button back = Button("返回", safe,
                new Vector2(-730, 400), new Vector2(190, 70));

            MetaLevelDefinition level = AssetDatabase.LoadAssetAtPath<
                MetaLevelDefinition>(LevelPath);
            MetaProductCardView card = AssetDatabase.LoadAssetAtPath<
                GameObject>(CardPrefabPath).GetComponent<MetaProductCardView>();
            ShopProductDefinition[] products = Array.ConvertAll(
                AssetDatabase.FindAssets(
                    "t:ShopProductDefinition", new[] { ProductFolder }),
                guid => AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)));
            Array.Sort(products, (a, b) => a.Price.CompareTo(b.Price));

            SetReference(menu, "_prototypeLevel", level);
            SetReference(menu, "_home", home.gameObject);
            SetReference(menu, "_levelSelection", levels.gameObject);
            SetReference(menu, "_modeSelection", modes.gameObject);
            SetReference(menu, "_shop", shop.gameObject);
            SetReference(menu, "_inventory", inventory.gameObject);
            SetReference(menu, "_achievements", achievements.gameObject);
            SetReference(menu, "_startGame", start);
            SetReference(menu, "_shopButton", shopButton);
            SetReference(menu, "_inventoryButton", inventoryButton);
            SetReference(menu, "_achievementsButton", achievementsButton);
            SetReference(menu, "_prototypeButton", prototype);
            SetReference(menu, "_soloButton", solo);
            SetReference(menu, "_onlineButton", online);
            SetReference(menu, "_backButton", back);

            SetArray(meta, "_products", products);
            SetReference(meta, "_cardPrefab", card);
            SetReference(meta, "_shopContent", shopContent);
            SetReference(meta, "_inventoryContent", inventoryContent);
            SetReference(meta, "_shopBalance", shopBalance);
            SetReference(meta, "_inventoryBalance", inventoryBalance);
            SetReference(meta, "_levelSelectionBalance", levelBalance);
            SetReference(meta, "_inventoryEmpty", inventoryEmpty);
            SetReference(meta, "_feedback", feedback);

            levels.gameObject.SetActive(false);
            modes.gameObject.SetActive(false);
            shop.gameObject.SetActive(false);
            inventory.gameObject.SetActive(false);
            achievements.gameObject.SetActive(false);
            back.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        private static void UpgradeGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameplayPath, OpenSceneMode.Single);
            GameObject oldFront = GameObject.Find("UI_FrontEnd");
            if (oldFront != null) UnityEngine.Object.DestroyImmediate(oldFront);
            GameObject oldEntry = GameObject.Find("UI_GameplayEntry");
            if (oldEntry != null) UnityEngine.Object.DestroyImmediate(oldEntry);

            CoopSessionController session = One<CoopSessionController>();
            OpeningCharacterSelectionController selection =
                One<OpeningCharacterSelectionController>();
            CoopSessionMenu networkMenu = One<CoopSessionMenu>();
            TouchCommandSource touch = One<TouchCommandSource>();
            Canvas combatHud = FindNamedCanvas("UI_CombatHUD");
            Canvas selectionCanvas =
                selection.GetComponent<Canvas>();

            RectTransform root = Rect("UI_GameplayEntry", null);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            GameplayEntryFlow entry =
                root.gameObject.AddComponent<GameplayEntryFlow>();
            RectTransform safe = Rect("SafeArea", root);
            Stretch(safe);
            SafeAreaRectFitter fitter =
                safe.gameObject.AddComponent<SafeAreaRectFitter>();
            SetReference(fitter, "_target", safe);
            Button back = Button("返回关卡选择", safe,
                new Vector2(-700, 400), new Vector2(260, 70));

            SetReference(entry, "_session", session);
            SetReference(entry, "_selection", selection);
            SetReference(entry, "_networkMenu", networkMenu);
            SetReference(entry, "_selectionCanvas", selectionCanvas);
            SetReference(entry, "_combatHud", combatHud);
            SetReference(entry, "_touchInput", touch);
            SetReference(entry, "_backButton", back);

            ChapterRunController run = One<ChapterRunController>();
            SetReference(run, "_profile", null);
            GameplayAchievementReporter reporter =
                root.gameObject.AddComponent<GameplayAchievementReporter>();
            PlayerReviveCoordinator2D[] reviveSystems =
                UnityEngine.Object.FindObjectsByType<PlayerReviveCoordinator2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            SetArray(reporter, "_reviveSystems", reviveSystems);
            EditorSceneManager.SaveScene(scene, GameplayPath);
        }

        private static AchievementDefinition[] BuildAchievements()
        {
            return new[]
            {
                Achievement("FirstFlight", "first_flight", "最初的飞行",
                    "未来的神明将因果投向过去。", true,
                    AchievementTriggerIds.JourneyStarted),
                Achievement("FirstClear", "first_clear", "第一段旅程",
                    "完成天空测试场。", false,
                    AchievementTriggerIds.LevelCleared),
                Achievement("FirstRevive", "first_revive", "不要睡啦",
                    "首次成功复活队友。", false,
                    AchievementTriggerIds.TeammateRevived),
                Achievement("FirstMerit", "first_merit", "一点功德",
                    "首次购买任意功德。", false,
                    AchievementTriggerIds.ProductPurchased),
                Achievement("FlawlessClear", "flawless_clear", "形影不离",
                    "无人进入倒地状态完成天空测试场。", false,
                    AchievementTriggerIds.FlawlessLevelCleared)
            };
        }

        private static AchievementDefinition Achievement(
            string fileName, string id, string displayName,
            string description, bool hidden, string triggerId)
        {
            string path = $"{AchievementFolder}/CFG_META_Achievement_{fileName}.asset";
            AchievementDefinition definition =
                AssetDatabase.LoadAssetAtPath<AchievementDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<AchievementDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            SetString(definition, "_achievementId", id);
            SetString(definition, "_displayName", displayName);
            SetString(definition, "_description", description);
            SetBool(definition, "_hidden", hidden);
            SetString(definition, "_triggerId", triggerId);
            SetInt(definition, "_targetCount", 1);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static AchievementCardView BuildAchievementCard()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                AchievementCardPrefabPath);
            if (existing != null)
                return existing.GetComponent<AchievementCardView>();

            GameObject root = new GameObject("PF_UI_AchievementCard",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(900f, 102f);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.055f, 0.12f, 0.22f, 0.96f);
            background.raycastTarget = false;
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 900f;
            layout.preferredHeight = 102f;

            RectTransform iconRect = Rect("Icon", rect);
            Position(iconRect, new Vector2(-390f, 0f), new Vector2(72f, 72f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Text name = Label("成就名称", rect, new Vector2(-225f, 22f),
                new Vector2(250f, 38f), 25);
            name.alignment = TextAnchor.MiddleLeft;
            Text description = Label("成就描述", rect,
                new Vector2(95f, -22f), new Vector2(610f, 36f), 20);
            description.alignment = TextAnchor.MiddleLeft;
            Text progress = Label("0/1", rect, new Vector2(365f, 22f),
                new Vector2(130f, 36f), 20);
            AchievementCardView view = root.AddComponent<AchievementCardView>();
            SetReference(view, "_icon", icon);
            SetReference(view, "_name", name);
            SetReference(view, "_description", description);
            SetReference(view, "_progress", progress);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root, AchievementCardPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved.GetComponent<AchievementCardView>();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GameplayPath, true)
            };
        }

        private static void BuildCameraAndLight()
        {
            GameObject cameraObject = new GameObject(
                "Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.05f, 0.105f, 1f);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            GameObject light = new GameObject("Main Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
        }

        private static void BuildEventSystem()
        {
            new GameObject("UI_EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
        }

        private static RectTransform Panel(string name, Transform parent)
        {
            RectTransform panel = Rect(name, parent);
            Position(panel, Vector2.zero, new Vector2(1100, 760));
            return panel;
        }

        private static RectTransform Card(
            Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform card = Rect("Card", parent);
            Position(card, position, size);
            card.gameObject.AddComponent<Image>().color =
                new Color(0.06f, 0.13f, 0.24f, 1f);
            return card;
        }

        private static Transform Content(
            string name, Transform parent, Vector2 position)
        {
            RectTransform content = Rect(name, parent);
            Position(content, position, new Vector2(920, 430));
            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            return content;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Position(
            RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot =
                new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text Label(
            string value, Transform parent, Vector2 position,
            Vector2 size, int fontSize)
        {
            RectTransform rect = Rect("Label", parent);
            Position(rect, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(
            string value, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(value, parent);
            Position(rect, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.3f, 0.52f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(value, rect, Vector2.zero, size, 27);
            return button;
        }

        private static Canvas FindNamedCanvas(string name)
        {
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<
                         Canvas>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (canvas.name == name) return canvas;
            throw new InvalidOperationException("未找到 " + name + " Canvas。");
        }

        private static T One<T>() where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length != 1)
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 必须恰有一个，当前 {found.Length} 个。");
            return found[0];
        }

        private static void SetReference(
            UnityEngine.Object target, string field,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException(
                    $"{target.name} 缺少字段 {field}。");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(
            UnityEngine.Object target, string field, T[] values)
            where T : UnityEngine.Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(
            UnityEngine.Object target, string field, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(
            UnityEngine.Object target, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(
            UnityEngine.Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
