using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Simulation;
using DeepSleep.Runtime.World.Playfield;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class DoubaoEncounterInstaller
    {
        private const int DESTRUCTIBLE_OBSTACLE_LAYER = 15;
        private const string SCENE_PATH = "Assets/Scenes/World01_EarlyInternet.unity";
        private const string BUBBLE_ART = "Assets/_Project/Art/VFX/Doubao/VFX_DB_WordBubble_v01.png";
        private const string DOUBAO_ART = "Assets/_Project/Art/Characters/Doubao/SPR_DB_RooftopExplain_v01.png";
        private const string FOREGROUND_ART = "Assets/_Project/Art/Foregrounds/World01/BG_W01_ForegroundCity_v01.png";
        private const string BUBBLE_PREFAB = "Assets/_Project/Prefabs/Combat/Enemies/PF_DB_WordBubble.prefab";
        private const string BUBBLE_REPLICA_PREFAB = "Assets/_Project/Prefabs/Networking/PF_NET_DB_WordBubbleView.prefab";
        private const string DOUBAO_PREFAB = "Assets/_Project/Prefabs/Combat/Enemies/PF_EN_Doubao.prefab";
        private const string CONFIG_PATH = "Assets/_Project/Configs/Combat/Enemies/CFG_DB_WordWall_Default.asset";

        [MenuItem("DeepSleep/开发/安装豆包词墙")]
        public static void Install()
        {
            ConfigureLayer();
            ConfigureBubbleTexture();
            ConfigureForegroundTexture();
            GameObject bubblePrefab = CreateBubblePrefab();
            GameObject bubbleReplicaPrefab = CreateBubbleReplicaPrefab();
            GameObject bossPrefab = CreateBossPrefab();
            DoubaoWordWallConfig config = CreateOrUpdateConfig();
            ConfigureWeaponLayers();
            ConfigureNetworkVersion();
            InstallScene(bubblePrefab, bubbleReplicaPrefab, bossPrefab, config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[豆包词墙] 正式素材、配置、Prefab 与第一世界第1战斗段装配完成。");
        }

        [MenuItem("DeepSleep/开发/修订豆包小词块阵")]
        public static void ReviseWordField()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            ConfigureBubbleTexture();
            CreateBubblePrefab();
            CreateBubbleReplicaPrefab();
            DoubaoWordWallConfig config = CreateOrUpdateConfig();
            ConfigureWeaponLayers();
            ConfigureNetworkVersion();
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            DoubaoEncounterNetworkChannel channel = FindInScene<DoubaoEncounterNetworkChannel>(scene);
            SerializedObject serialized = new SerializedObject(channel);
            serialized.FindProperty("_maximumViews").intValue = config.PoolCapacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[豆包词墙] 小词块、全域错位波次及公共受击入口已修订；前景与豆包摆位保留。");
        }

        private static void ConfigureLayer()
        {
            UnityEngine.Object tagManager =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject serialized = new SerializedObject(tagManager);
            SerializedProperty layers = serialized.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(DESTRUCTIBLE_OBSTACLE_LAYER);
            if (!string.IsNullOrEmpty(slot.stringValue) &&
                slot.stringValue != "DestructibleObstacle")
                throw new InvalidOperationException(
                    $"Physics Layer {DESTRUCTIBLE_OBSTACLE_LAYER} 已被 {slot.stringValue} 占用。");
            slot.stringValue = "DestructibleObstacle";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBubbleTexture()
        {
            AssetDatabase.ImportAsset(BUBBLE_ART, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(BUBBLE_ART) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("气泡 TextureImporter 不存在。");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            // 左侧覆盖气泡尾巴；中部只拉伸纯净文字面。
            settings.spriteBorder = new Vector4(210f, 150f, 150f, 135f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void ConfigureForegroundTexture()
        {
            AssetDatabase.ImportAsset(FOREGROUND_ART, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(FOREGROUND_ART) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("前景城市 TextureImporter 不存在。");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static GameObject CreateBubblePrefab()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BUBBLE_ART);
            GameObject root = new GameObject("PF_DB_WordBubble")
            {
                layer = DESTRUCTIBLE_OBSTACLE_LAYER
            };
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.22f, 0.66f);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = collider.size;
            renderer.sortingLayerName = "Gameplay";
            renderer.sortingOrder = 34;

            GameObject textObject = new GameObject("Phrase");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0.02f, 0.025f, -0.01f);
            TextMesh label = textObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "最直接";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            // 0.085 在实际战斗画面会让五字短语挤出气泡；0.06 保留移动端可读性并留出九宫格内边距。
            label.characterSize = 0.034f;
            label.lineSpacing = 0.85f;
            label.color = new Color(0.08f, 0.12f, 0.24f, 1f);
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = label.font.material;
            textRenderer.sortingLayerName = "Gameplay";
            textRenderer.sortingOrder = 35;

            DoubaoWordWallBlock2D block = root.AddComponent<DoubaoWordWallBlock2D>();
            DamageHitbox2D hitbox = root.AddComponent<DamageHitbox2D>();
            SerializedObject hitboxSerialized = new SerializedObject(hitbox);
            hitboxSerialized.FindProperty("_receiverComponent").objectReferenceValue = block;
            hitboxSerialized.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject serialized = new SerializedObject(block);
            serialized.FindProperty("_body").objectReferenceValue = body;
            serialized.FindProperty("_bodyCollider").objectReferenceValue = collider;
            serialized.FindProperty("_renderer").objectReferenceValue = renderer;
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.FindProperty("_damageHitbox").objectReferenceValue = hitbox;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BUBBLE_PREFAB);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBossPrefab()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DOUBAO_ART);
            GameObject root = new GameObject("PF_EN_Doubao") { layer = 8 };
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Gameplay";
            renderer.sortingOrder = 32;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.78f, 1.55f);
            collider.offset = new Vector2(0f, -0.05f);
            DoubaoBoss2D boss = root.AddComponent<DoubaoBoss2D>();
            SerializedObject bossSerialized = new SerializedObject(boss);
            bossSerialized.FindProperty("_hitCollider").objectReferenceValue = collider;
            bossSerialized.FindProperty("_renderer").objectReferenceValue = renderer;
            bossSerialized.ApplyModifiedPropertiesWithoutUndo();
            DamageHitbox2D hitbox = root.AddComponent<DamageHitbox2D>();
            SerializedObject hitboxSerialized = new SerializedObject(hitbox);
            hitboxSerialized.FindProperty("_receiverComponent").objectReferenceValue = boss;
            hitboxSerialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DOUBAO_PREFAB);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBubbleReplicaPrefab()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BUBBLE_ART);
            GameObject root = new GameObject("PF_NET_DB_WordBubbleView");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(1.22f, 0.66f);
            renderer.sortingLayerName = "Gameplay";
            renderer.sortingOrder = 34;

            GameObject textObject = new GameObject("Phrase");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0.02f, 0.025f, -0.01f);
            TextMesh label = textObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.034f;
            label.lineSpacing = 0.85f;
            label.color = new Color(0.08f, 0.12f, 0.24f, 1f);
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = label.font.material;
            textRenderer.sortingLayerName = "Gameplay";
            textRenderer.sortingOrder = 35;

            DoubaoWordWallReplicaView2D view = root.AddComponent<DoubaoWordWallReplicaView2D>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("_renderer").objectReferenceValue = renderer;
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EnsureFolder("Assets/_Project/Prefabs/Networking");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BUBBLE_REPLICA_PREFAB);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static DoubaoWordWallConfig CreateOrUpdateConfig()
        {
            DoubaoWordWallConfig config =
                AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DoubaoWordWallConfig>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
            }
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("_initialDelaySeconds").floatValue = 1f;
            serialized.FindProperty("_groupIntervalSeconds").floatValue = 1.35f;
            serialized.FindProperty("_bossRevealSeconds").floatValue = 8f;
            serialized.FindProperty("_fallSpeed").floatValue = 1.05f;
            serialized.FindProperty("_groupsPerWave").intValue = 4;
            serialized.FindProperty("_coverageWidth").floatValue = 17.2f;
            serialized.FindProperty("_horizontalJitter").floatValue = 0.45f;
            serialized.FindProperty("_verticalJitter").floatValue = 0.6f;
            serialized.FindProperty("_blockMaximumHealth").floatValue = 10f;
            serialized.FindProperty("_contactDamage").floatValue = 1f;
            serialized.FindProperty("_blockHeight").floatValue = 0.66f;
            serialized.FindProperty("_spawnTopPadding").floatValue = 0.6f;
            serialized.FindProperty("_despawnBottomPadding").floatValue = 0.6f;
            serialized.FindProperty("_poolCapacity").intValue = 160;
            // 首轮显式灰盒值，后续按实机节奏调；10 HP 只属于词块。
            serialized.FindProperty("_bossMaximumHealth").floatValue = 45f;

            string[] phrases =
            {
                "最直接", "最真相", "最透彻", "最尖锐", "最深刻", "最现实",
                "最不绕弯", "最不兜圈子", "最扎心", "最硬核", "最干脆", "最利落",
                "最不墨迹", "最一针见血", "最开门见山", "最不铺垫", "最不客套",
                "最不废话", "最不赘述", "最直白", "最坦诚", "最不加滤镜",
                "最毫无保留", "我没绷住"
            };
            SerializedProperty phraseProperty = serialized.FindProperty("_phrases");
            phraseProperty.arraySize = phrases.Length;
            for (int index = 0; index < phrases.Length; index++)
                phraseProperty.GetArrayElementAtIndex(index).stringValue = phrases[index];

            string[] names = { "小L", "反L", "错层", "斜阶" };
            Vector3[][] slots =
            {
                new[] { new Vector3(-0.65f,0f,1.22f), new Vector3(0.65f,0f,1.22f), new Vector3(-0.65f,0.76f,1.22f) },
                new[] { new Vector3(-0.65f,0f,1.22f), new Vector3(0.65f,0.76f,1.22f), new Vector3(0.65f,0f,1.22f) },
                new[] { new Vector3(-0.65f,0f,1.22f), new Vector3(0.65f,0.76f,1.22f), new Vector3(-0.65f,1.52f,1.22f), new Vector3(0.65f,1.52f,1.22f) },
                new[] { new Vector3(-0.65f,0f,1.22f), new Vector3(0f,0.76f,1.22f), new Vector3(0.65f,1.52f,1.22f) }
            };
            SerializedProperty patternProperty = serialized.FindProperty("_patterns");
            patternProperty.arraySize = names.Length;
            for (int patternIndex = 0; patternIndex < names.Length; patternIndex++)
            {
                SerializedProperty pattern = patternProperty.GetArrayElementAtIndex(patternIndex);
                pattern.FindPropertyRelative("_displayName").stringValue = names[patternIndex];
                SerializedProperty slotProperty = pattern.FindPropertyRelative("_slots");
                slotProperty.arraySize = slots[patternIndex].Length;
                for (int slotIndex = 0; slotIndex < slots[patternIndex].Length; slotIndex++)
                {
                    SerializedProperty slot = slotProperty.GetArrayElementAtIndex(slotIndex);
                    Vector3 value = slots[patternIndex][slotIndex];
                    slot.FindPropertyRelative("_offset").vector2Value = new Vector2(value.x, value.y);
                    slot.FindPropertyRelative("_width").floatValue = value.z;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void ConfigureWeaponLayers()
        {
            int bit = 1 << DESTRUCTIBLE_OBSTACLE_LAYER;
            AddMask("Assets/_Project/Configs/Combat/DeepSeek/CFG_DS_RiceWeapon_Default.asset", "_projectileCollisionLayers", bit);
            AddMask("Assets/_Project/Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset", "_environmentDamageLayers", bit);
            AddMask("Assets/_Project/Configs/Combat/Enemies/DataCrawlerSnake/CFG_EN_DataCrawlerSnake_Projectile_Default.asset", "_collisionLayers", bit);
        }

        private static void ConfigureNetworkVersion()
        {
            NetworkTuningConfig network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>(
                "Assets/_Project/Configs/Networking/CFG_Network.asset");
            if (network == null) throw new InvalidOperationException("缺少联机配置 CFG_Network。");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network);
            EditorUtility.SetDirty(network);
        }

        private static void AddMask(string path, string propertyName, int bit)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{path} 缺少 {propertyName}。");
            property.intValue |= bit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void InstallScene(
            GameObject bubblePrefab,
            GameObject bubbleReplicaPrefab,
            GameObject bossPrefab,
            DoubaoWordWallConfig config)
        {
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            GameObject previous = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "World01_DoubaoEncounter");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            GameObject previousForeground = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "World01_ForegroundCity");
            if (previousForeground != null) UnityEngine.Object.DestroyImmediate(previousForeground);

            FixedSimulationLoop loop = FindInScene<FixedSimulationLoop>(scene);
            ChapterRunController chapter = FindInScene<ChapterRunController>(scene);
            CoopSessionController session = FindInScene<CoopSessionController>(scene);
            var obstacleRegistry = session.Assignment.GetComponent<DeepSleep.Runtime.Players.Companion.CompanionObstacleRegistry2D>();
            if (obstacleRegistry == null) obstacleRegistry = session.Assignment.gameObject.AddComponent<DeepSleep.Runtime.Players.Companion.CompanionObstacleRegistry2D>();
            obstacleRegistry.Session = session;
            Camera camera = FindInScene<Camera>(scene);
            CombatPlayfieldConfig playfield = AssetDatabase.LoadAssetAtPath<CombatPlayfieldConfig>(
                "Assets/_Project/Configs/World/CFG_CombatPlayfield_Default.asset");
            GameObject root = new GameObject("World01_DoubaoEncounter");
            SceneManager.MoveGameObjectToScene(root, scene);
            GameObject foreground = new GameObject("World01_ForegroundCity");
            SceneManager.MoveGameObjectToScene(foreground, scene);
            SpriteRenderer foregroundRenderer = foreground.AddComponent<SpriteRenderer>();
            foregroundRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FOREGROUND_ART);
            foregroundRenderer.sortingLayerName = "Gameplay";
            foregroundRenderer.sortingOrder = 30;
            CameraLockedSpriteLayer2D cameraLock = foreground.AddComponent<CameraLockedSpriteLayer2D>();
            SerializedObject lockSerialized = new SerializedObject(cameraLock);
            lockSerialized.FindProperty("_camera").objectReferenceValue = camera;
            lockSerialized.FindProperty("_renderer").objectReferenceValue = foregroundRenderer;
            lockSerialized.FindProperty("_coverageMultiplier").floatValue = 1.015f;
            lockSerialized.ApplyModifiedPropertiesWithoutUndo();
            cameraLock.FitNow();
            Transform poolRoot = NewChild(root.transform, "WordBubblePool", Vector3.zero);
            Transform replicaRoot = NewChild(root.transform, "NetworkWordBubbleViews", Vector3.zero);
            Transform bossAnchor = NewChild(foreground.transform, "DoubaoRooftopAnchor", Vector3.zero);
            bossAnchor.localPosition = new Vector3(3.05f, -1.52f, 0f);
            Transform groupAnchor = NewChild(root.transform, "WordWallCenterAnchor", Vector3.zero);
            GameObject bossObject = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
            bossObject.name = "DoubaoBoss";
            bossObject.transform.SetParent(root.transform, true);
            DoubaoBoss2D boss = bossObject.GetComponent<DoubaoBoss2D>();

            DoubaoWordWallEncounter2D encounter = root.AddComponent<DoubaoWordWallEncounter2D>();
            SerializedObject encounterSerialized = new SerializedObject(encounter);
            encounterSerialized.FindProperty("_config").objectReferenceValue = config;
            encounterSerialized.FindProperty("_playfield").objectReferenceValue = playfield;
            encounterSerialized.FindProperty("_blockPrefab").objectReferenceValue = bubblePrefab.GetComponent<DoubaoWordWallBlock2D>();
            encounterSerialized.FindProperty("_blockPoolRoot").objectReferenceValue = poolRoot;
            encounterSerialized.FindProperty("_boss").objectReferenceValue = boss;
            encounterSerialized.FindProperty("_bossAnchor").objectReferenceValue = bossAnchor;
            encounterSerialized.FindProperty("_groupCenterAnchor").objectReferenceValue = groupAnchor;
            encounterSerialized.FindProperty("_obstacleRegistry").objectReferenceValue = obstacleRegistry;
            encounterSerialized.FindProperty("_beginOnEnable").boolValue = false;
            encounterSerialized.FindProperty("_deterministicSeed").intValue = 20260914;
            encounterSerialized.ApplyModifiedPropertiesWithoutUndo();

            DoubaoChapterEncounterDriver2D driver = root.AddComponent<DoubaoChapterEncounterDriver2D>();
            SerializedObject driverSerialized = new SerializedObject(driver);
            driverSerialized.FindProperty("_chapterRun").objectReferenceValue = chapter;
            driverSerialized.FindProperty("_encounter").objectReferenceValue = encounter;
            driverSerialized.FindProperty("_session").objectReferenceValue = session;
            SerializedProperty segments = driverSerialized.FindProperty("_segmentNumbers");
            segments.arraySize = 4;
            for (int i = 0; i < segments.arraySize; i++) segments.GetArrayElementAtIndex(i).intValue = i + 1;
            driverSerialized.FindProperty("_rewards").objectReferenceValue = FindInScene<EnemyTokenRewardController>(scene);
            driverSerialized.FindProperty("_tokenReward").intValue = 100;
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();

            DoubaoEncounterNetworkChannel network = root.AddComponent<DoubaoEncounterNetworkChannel>();
            SerializedObject networkSerialized = new SerializedObject(network);
            networkSerialized.FindProperty("_session").objectReferenceValue = session;
            networkSerialized.FindProperty("_encounter").objectReferenceValue = encounter;
            networkSerialized.FindProperty("_boss").objectReferenceValue = boss;
            networkSerialized.FindProperty("_viewPrefab").objectReferenceValue =
                bubbleReplicaPrefab.GetComponent<DoubaoWordWallReplicaView2D>();
            networkSerialized.FindProperty("_viewRoot").objectReferenceValue = replicaRoot;
            networkSerialized.FindProperty("_maximumViews").intValue = config.PoolCapacity;
            networkSerialized.ApplyModifiedPropertiesWithoutUndo();
            AppendUnique(loop, "worldStepComponents", driver);
            AppendUnique(chapter, "_additionalObjectiveComponents", driver);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Transform NewChild(Transform parent, string name, Vector3 position)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.position = position;
            return child.transform;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            throw new InvalidOperationException($"{SCENE_PATH} 缺少 {typeof(T).Name}。");
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void AppendUnique(Component owner, string field, MonoBehaviour value)
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty array = serialized.FindProperty(field);
            // 重装时旧遭遇根会先销毁；必须移除数组中遗留的 Missing 引用，否则固定模拟环会整段禁用。
            for (int index = array.arraySize - 1; index >= 0; index--)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue != null) continue;
                int previousSize = array.arraySize;
                array.DeleteArrayElementAtIndex(index);
                if (array.arraySize == previousSize) array.DeleteArrayElementAtIndex(index);
            }
            for (int index = 0; index < array.arraySize; index++)
                if (array.GetArrayElementAtIndex(index).objectReferenceValue == value) return;
            array.InsertArrayElementAtIndex(array.arraySize);
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
