using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class MetaProgressionSceneInstaller
    {
        private const string MetaConfigFolder =
            "Assets/_Project/Configs/Progression/Meta";
        private const string ProductFolder = MetaConfigFolder + "/Products";
        private const string LevelPath = MetaConfigFolder +
            "/CFG_META_Level_PrototypeSky.asset";
        private const string PrefabFolder =
            "Assets/_Project/Prefabs/UI/Meta";
        private const string CardPrefabPath = PrefabFolder +
            "/PF_UI_MetaProductCard.prefab";

        [MenuItem("DeepSleep/设置/装配关卡选择、鲸元券商店与背包")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式装配。");
            }

            EnsureFolder(ProductFolder);
            EnsureFolder(PrefabFolder);

            MetaLevelDefinition level = LoadOrCreateLevel();
            ShopProductDefinition[] products =
            {
                LoadOrCreateProduct("merit_small", "小功德", 5),
                LoadOrCreateProduct("merit_medium", "中功德", 10),
                LoadOrCreateProduct("merit_large", "大功德", 15)
            };
            MetaProductCardView cardPrefab = BuildOrReplaceCardPrefab();

            OpeningFrontEnd front = One<OpeningFrontEnd>();
            RectTransform uiRoot = front.transform as RectTransform;
            RectTransform safe = FindChild(uiRoot, "SafeArea") as RectTransform;
            if (safe == null)
            {
                throw new InvalidOperationException("UI_FrontEnd 缺少 SafeArea。");
            }

            LocalPlayerProfileStore profile =
                front.GetComponent<LocalPlayerProfileStore>();
            if (profile == null)
            {
                profile = Undo.AddComponent<LocalPlayerProfileStore>(
                    front.gameObject);
            }
            WhaleMetaMenuController meta =
                front.GetComponent<WhaleMetaMenuController>();
            if (meta == null)
            {
                meta = Undo.AddComponent<WhaleMetaMenuController>(
                    front.gameObject);
            }

            RemoveOldNavigation(front, safe);
            BuildHome(front, front.MainPanel.transform);

            RectTransform levelPanel = Panel("LevelSelection", safe);
            Label("选择关卡", levelPanel, new Vector2(0, 275),
                new Vector2(900, 70), 44);
            Text levelBalance = Label("鲸元券：0", levelPanel,
                new Vector2(360, 275), new Vector2(300, 55), 24);
            RectTransform prototypeCard = Card(levelPanel,
                new Vector2(0, 70), new Vector2(920, 250));
            Label("天空测试场", prototypeCard, new Vector2(0, 70),
                new Vector2(820, 55), 36);
            Label("三段战斗原型 · 首通 10 / 重复 5 鲸元券",
                prototypeCard, new Vector2(0, 15),
                new Vector2(820, 45), 23);
            front.PrototypeLevel = Button("进入关卡", prototypeCard,
                new Vector2(0, -65), new Vector2(360, 62));
            Button future = Button("后续关卡 · 暂未实现", levelPanel,
                new Vector2(0, -150), new Vector2(920, 105));
            future.interactable = false;

            RectTransform modePanel = Panel("ModeSelection", safe);
            Label("选择游玩方式", modePanel, new Vector2(0, 190),
                new Vector2(800, 70), 44);
            front.Solo = Button("单人游戏", modePanel,
                new Vector2(0, 45), new Vector2(540, 90));
            front.Online = Button("在线联机", modePanel,
                new Vector2(0, -75), new Vector2(540, 90));

            RectTransform shopPanel = Panel("WhaleVoucherShop", safe);
            Label("鲸元券商店", shopPanel, new Vector2(0, 300),
                new Vector2(700, 70), 44);
            Text shopBalance = Label("鲸元券：0", shopPanel,
                new Vector2(355, 300), new Vector2(300, 55), 24);
            Transform shopContent = Content("Products", shopPanel,
                new Vector2(0, 15));
            Text feedback = Label(string.Empty, shopPanel,
                new Vector2(0, -300), new Vector2(900, 45), 22);

            RectTransform inventoryPanel = Panel("Inventory", safe);
            Label("背包", inventoryPanel, new Vector2(0, 300),
                new Vector2(700, 70), 44);
            Text inventoryBalance = Label("鲸元券：0", inventoryPanel,
                new Vector2(355, 300), new Vector2(300, 55), 24);
            Transform inventoryContent = Content("OwnedProducts",
                inventoryPanel, new Vector2(0, 15));
            Text inventoryEmpty = Label("背包里暂时没有物品", inventoryPanel,
                new Vector2(0, 25), new Vector2(700, 55), 25);

            front.LevelSelectionPanel = levelPanel.gameObject;
            front.ModeSelectionPanel = modePanel.gameObject;
            front.ShopPanel = shopPanel.gameObject;
            front.InventoryPanel = inventoryPanel.gameObject;
            front.Back = Button("返回", safe, new Vector2(-730, 400),
                new Vector2(190, 70));

            SetReference(meta, "_profile", profile);
            SetArray(meta, "_products", products);
            SetReference(meta, "_cardPrefab", cardPrefab);
            SetReference(meta, "_shopContent", shopContent);
            SetReference(meta, "_inventoryContent", inventoryContent);
            SetReference(meta, "_shopBalance", shopBalance);
            SetReference(meta, "_inventoryBalance", inventoryBalance);
            SetReference(meta, "_levelSelectionBalance", levelBalance);
            SetReference(meta, "_inventoryEmpty", inventoryEmpty);
            SetReference(meta, "_feedback", feedback);

            ChapterRunController run = One<ChapterRunController>();
            SetReference(run, "_profile", profile);
            SetReference(run, "_level", level);
            Transform returnButton = FindChild(
                One<ChapterRunHudView>().transform.parent,
                "ReturnToOpening");
            if (returnButton != null)
            {
                Text returnLabel = returnButton.GetComponentInChildren<Text>();
                if (returnLabel != null) returnLabel.text = "返回关卡选择";
            }

            levelPanel.gameObject.SetActive(false);
            modePanel.gameObject.SetActive(false);
            shopPanel.gameObject.SetActive(false);
            inventoryPanel.gameObject.SetActive(false);

            EditorUtility.SetDirty(front);
            EditorUtility.SetDirty(meta);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(front.gameObject.scene);
            EditorSceneManager.SaveScene(front.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Meta] 已装配：首页、关卡选择、模式选择、商店、背包与本地档案。");
        }

        private static void BuildHome(OpeningFrontEnd front, Transform main)
        {
            front.StartGame = Button("开始游戏", main,
                new Vector2(0, 15), new Vector2(540, 78));
            front.Shop = Button("鲸元券商店", main,
                new Vector2(0, -85), new Vector2(540, 78));
            front.Inventory = Button("背包", main,
                new Vector2(0, -185), new Vector2(540, 78));
        }

        private static void RemoveOldNavigation(
            OpeningFrontEnd front,
            Transform safe)
        {
            var remove = new HashSet<GameObject>();
            if (front.StartGame != null) remove.Add(front.StartGame.gameObject);
            if (front.Shop != null) remove.Add(front.Shop.gameObject);
            if (front.Inventory != null) remove.Add(front.Inventory.gameObject);
            if (front.Solo != null) remove.Add(front.Solo.gameObject);
            if (front.Online != null) remove.Add(front.Online.gameObject);
            if (front.Back != null) remove.Add(front.Back.gameObject);
            string[] panels =
            {
                "LevelSelection", "ModeSelection",
                "WhaleVoucherShop", "Inventory"
            };
            for (int index = 0; index < panels.Length; index++)
            {
                Transform child = FindChild(safe, panels[index]);
                if (child != null) remove.Add(child.gameObject);
            }
            foreach (GameObject item in remove)
            {
                Undo.DestroyObjectImmediate(item);
            }
        }

        private static MetaLevelDefinition LoadOrCreateLevel()
        {
            EnsureFolder(MetaConfigFolder);
            MetaLevelDefinition level = AssetDatabase.LoadAssetAtPath<
                MetaLevelDefinition>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<MetaLevelDefinition>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }
            SerializedObject serialized = new SerializedObject(level);
            serialized.FindProperty("_levelId").stringValue = "prototype_sky";
            serialized.FindProperty("_displayName").stringValue = "天空测试场";
            serialized.FindProperty("_description").stringValue =
                "当前用于练习与系统验证的三段原型关卡。";
            serialized.FindProperty("_firstClearVoucherReward").intValue = 10;
            serialized.FindProperty("_repeatClearVoucherReward").intValue = 5;
            serialized.FindProperty("_implemented").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }

        private static ShopProductDefinition LoadOrCreateProduct(
            string id,
            string displayName,
            int price)
        {
            string path = ProductFolder + "/CFG_META_Product_" + id + ".asset";
            ShopProductDefinition product = AssetDatabase.LoadAssetAtPath<
                ShopProductDefinition>(path);
            if (product == null)
            {
                product = ScriptableObject.CreateInstance<
                    ShopProductDefinition>();
                AssetDatabase.CreateAsset(product, path);
            }
            SerializedObject serialized = new SerializedObject(product);
            serialized.FindProperty("_productId").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_description").stringValue =
                "简介暂未填写";
            serialized.FindProperty("_price").intValue = price;
            serialized.FindProperty("_repeatable").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(product);
            return product;
        }

        private static MetaProductCardView BuildOrReplaceCardPrefab()
        {
            GameObject root = new GameObject(
                "PF_UI_MetaProductCard", typeof(RectTransform));
            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(920, 132);
            Image background = root.AddComponent<Image>();
            background.color = new Color(0.055f, 0.11f, 0.2f, 0.96f);
            MetaProductCardView view = root.AddComponent<MetaProductCardView>();
            Image icon = Rect("Icon", rect).gameObject.AddComponent<Image>();
            Position(icon.rectTransform, new Vector2(-395, 0),
                new Vector2(92, 92));
            Text name = Label("商品", rect, new Vector2(-250, 29),
                new Vector2(230, 42), 28);
            Text description = Label("简介暂未填写", rect,
                new Vector2(-165, -25), new Vector2(400, 40), 20);
            Text owned = Label("持有 ×0", rect, new Vector2(95, 25),
                new Vector2(180, 40), 22);
            Text price = Label("5 鲸元券", rect, new Vector2(95, -25),
                new Vector2(180, 40), 20);
            Button purchase = Button("购买", rect, new Vector2(345, 0),
                new Vector2(170, 68));
            SetReference(view, "_icon", icon);
            SetReference(view, "_name", name);
            SetReference(view, "_description", description);
            SetReference(view, "_owned", owned);
            SetReference(view, "_price", price);
            SetReference(view, "_purchase", purchase);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root, CardPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<MetaProductCardView>();
        }

        private static RectTransform Panel(string name, Transform parent)
        {
            RectTransform panel = Rect(name, parent);
            Position(panel, Vector2.zero, new Vector2(1100, 760));
            Image image = Undo.AddComponent<Image>(panel.gameObject);
            image.color = new Color(0.025f, 0.055f, 0.11f, 0.96f);
            return panel;
        }

        private static RectTransform Card(
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            RectTransform card = Rect("Card", parent);
            Position(card, position, size);
            Image image = Undo.AddComponent<Image>(card.gameObject);
            image.color = new Color(0.06f, 0.13f, 0.24f, 1f);
            return card;
        }

        private static Transform Content(
            string name,
            Transform parent,
            Vector2 position)
        {
            RectTransform content = Rect(name, parent);
            Position(content, position, new Vector2(920, 430));
            VerticalLayoutGroup layout =
                Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
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
            Undo.RegisterCreatedObjectUndo(go, "Install meta progression");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Position(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot =
                new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Text Label(
            string value,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            RectTransform rect = Rect("Label", parent);
            Position(rect, position, size);
            Text text = Undo.AddComponent<Text>(rect.gameObject);
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
            string value,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect = Rect(value, parent);
            Position(rect, position, size);
            Image image = Undo.AddComponent<Image>(rect.gameObject);
            image.color = new Color(0.12f, 0.3f, 0.52f, 1f);
            Button button = Undo.AddComponent<Button>(rect.gameObject);
            button.targetGraphic = image;
            Label(value, rect, Vector2.zero, size, 27);
            return button;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private static T One<T>() where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in
                     EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }
            if (found.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 必须恰有一个，当前 {found.Count} 个。");
            }
            return found[0];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.name} 缺少字段 {propertyName}。");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(
            UnityEngine.Object target,
            string propertyName,
            T[] values) where T : UnityEngine.Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
