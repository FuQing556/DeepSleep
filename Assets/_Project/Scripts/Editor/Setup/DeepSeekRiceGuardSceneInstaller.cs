using System;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Setup
{
    /// <summary>
    /// 为当前玩法场景装配 DS 米饭护航。可重复执行，不会重复添加组件或数组项。
    /// 该工具规避 MCP v10.0.0 在 Unity 6.6 下无法通过 InstanceID 读取组件的问题。
    /// </summary>
    internal static class DeepSeekRiceGuardSceneInstaller
    {
        private const string MenuPath =
            "DeepSleep/设置/装配 DS 米饭护航";
        private const string ConfigPath =
            "Assets/_Project/Configs/Combat/DeepSeek/CFG_DS_RiceGuard_Default.asset";
        private const string BowlSpritePath =
            "Assets/_Project/Art/VFX/DeepSeek/RiceGuard/SPR_DS_RiceGuardBowl_v01.png";
        private const string BrokenBowlSpritePath =
            "Assets/_Project/Art/VFX/DeepSeek/RiceGuard/SPR_DS_RiceGuardBowlBroken_v01.png";
        private const string GuardCircleSpritePath =
            "Assets/_Project/Art/VFX/DeepSeek/RiceGuard/VFX_DS_RiceGuardCircle_v01.png";
        private const string VisualRootName = "RiceGuardVisual";
        private const string GuardCircleName = "GuardCircle";

        [MenuItem(MenuPath, priority = 40)]
        private static void Install()
        {
            PlayerActor[] actors = UnityEngine.Object.FindObjectsByType<PlayerActor>(
                FindObjectsInactive.Include);
            PlayerActor deepSeek = FindSingleActor(actors, PlayerRole.DeepSeek);
            PlayerActor harness = FindSingleActor(actors, PlayerRole.Harness);
            FixedSimulationLoop simulationLoop = FindSingleSimulationLoop();
            DeepSeekRiceGuardConfig config =
                AssetDatabase.LoadAssetAtPath<DeepSeekRiceGuardConfig>(ConfigPath);

            if (config == null)
                throw new InvalidOperationException($"找不到护航参数资产：{ConfigPath}");

            PlayerDamageReceiver2D deepSeekReceiver =
                RequireComponent<PlayerDamageReceiver2D>(deepSeek);
            PlayerDamageReceiver2D harnessReceiver =
                RequireComponent<PlayerDamageReceiver2D>(harness);
            PlayerLifeStateController2D deepSeekLifeState =
                RequireComponent<PlayerLifeStateController2D>(deepSeek);

            DeepSeekRiceGuardController controller =
                deepSeek.GetComponent<DeepSeekRiceGuardController>();
            if (controller == null)
                controller = Undo.AddComponent<DeepSeekRiceGuardController>(deepSeek.gameObject);

            ConfigureController(
                controller,
                config,
                deepSeekLifeState,
                deepSeekReceiver,
                harnessReceiver,
                deepSeek.transform,
                harness.transform);
            ConfigureSpriteImporter(BowlSpritePath);
            ConfigureSpriteImporter(BrokenBowlSpritePath);
            ConfigureSpriteImporter(GuardCircleSpritePath);
            Sprite bowlSprite = LoadSprite(BowlSpritePath);
            Sprite brokenBowlSprite = LoadSprite(BrokenBowlSpritePath);
            Sprite guardCircleSprite = LoadSprite(GuardCircleSpritePath);
            ConfigureOrbitView(
                deepSeek, controller, bowlSprite, brokenBowlSprite,
                guardCircleSprite);
            AppendUniqueReference(
                deepSeek.CommandDispatcher,
                "_commandConsumerComponents",
                controller);
            AppendUniqueReference(
                simulationLoop,
                "worldStepComponents",
                controller);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(deepSeek.CommandDispatcher);
            EditorUtility.SetDirty(simulationLoop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                deepSeek.CommandDispatcher);
            EditorSceneManager.MarkSceneDirty(deepSeek.gameObject.scene);
            EditorSceneManager.SaveScene(deepSeek.gameObject.scene);

            Debug.Log(
                "[DeepSleep 设置] DS 米饭护航已装配：PrimarySkill → 护航控制器；" +
                "DS/HS 受伤入口 → 共享六次拦截；固定帧循环 → 时长与冷却；" +
                "六碗表现 → 破碎旋转淡出后重排；法阵 → 反向旋转并与碗心轨道对齐。",
                controller);
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null
                ? sprite
                : throw new InvalidOperationException($"找不到已导入的护航素材：{path}");
        }

        private static void ConfigureSpriteImporter(string path)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    $"护航素材尚未被Unity识别：{path}");

            bool changed =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 512f) ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureOrbitView(
            PlayerActor deepSeek,
            DeepSeekRiceGuardController controller,
            Sprite bowlSprite,
            Sprite brokenBowlSprite,
            Sprite guardCircleSprite)
        {
            Transform visualRoot = deepSeek.transform.Find(VisualRootName);
            if (visualRoot == null)
            {
                GameObject rootObject = new GameObject(VisualRootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "创建米饭护航表现");
                visualRoot = rootObject.transform;
                visualRoot.SetParent(deepSeek.transform, false);
            }
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            DeepSeekRiceGuardOrbitView2D view =
                visualRoot.GetComponent<DeepSeekRiceGuardOrbitView2D>();
            if (view == null)
                view = Undo.AddComponent<DeepSeekRiceGuardOrbitView2D>(
                    visualRoot.gameObject);

            SpriteRenderer sourceRenderer =
                deepSeek.GetComponentInChildren<SpriteRenderer>(true);
            int sortingLayerId = sourceRenderer != null
                ? sourceRenderer.sortingLayerID
                : 0;
            SpriteRenderer[] bowls =
                new SpriteRenderer[controller.Capacity];
            for (int index = 0; index < bowls.Length; index++)
            {
                string name = $"Bowl_{index + 1:00}";
                Transform child = visualRoot.Find(name);
                if (child == null)
                {
                    GameObject childObject = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(
                        childObject,
                        "创建护航米饭视图");
                    child = childObject.transform;
                    child.SetParent(visualRoot, false);
                }

                SpriteRenderer renderer =
                    child.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    renderer = Undo.AddComponent<SpriteRenderer>(
                        child.gameObject);
                renderer.sprite = bowlSprite;
                renderer.sortingLayerID = sortingLayerId;
                renderer.enabled = false;
                bowls[index] = renderer;
            }

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("_controller").objectReferenceValue =
                controller;
            serialized.FindProperty("_intactSprite").objectReferenceValue =
                bowlSprite;
            serialized.FindProperty("_brokenSprite").objectReferenceValue =
                brokenBowlSprite;
            SerializedProperty bowlArray = serialized.FindProperty("_bowls");
            bowlArray.arraySize = bowls.Length;
            for (int index = 0; index < bowls.Length; index++)
                bowlArray.GetArrayElementAtIndex(index).objectReferenceValue =
                    bowls[index];
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

            ConfigureGuardCircle(
                visualRoot, controller, view, guardCircleSprite,
                sortingLayerId);
        }

        private static void ConfigureGuardCircle(
            Transform visualRoot,
            DeepSeekRiceGuardController controller,
            DeepSeekRiceGuardOrbitView2D orbitView,
            Sprite circleSprite,
            int sortingLayerId)
        {
            Transform circle = visualRoot.Find(GuardCircleName);
            if (circle == null)
            {
                GameObject circleObject = new GameObject(GuardCircleName);
                Undo.RegisterCreatedObjectUndo(circleObject, "创建米饭护航法阵");
                circle = circleObject.transform;
                circle.SetParent(visualRoot, false);
            }
            circle.localPosition = Vector3.zero;
            circle.localRotation = Quaternion.identity;

            SpriteRenderer renderer = circle.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(circle.gameObject);
            renderer.sprite = circleSprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = -20;
            renderer.enabled = false;

            DeepSeekRiceGuardCircleView2D circleView =
                circle.GetComponent<DeepSeekRiceGuardCircleView2D>();
            if (circleView == null)
                circleView = Undo.AddComponent<DeepSeekRiceGuardCircleView2D>(circle.gameObject);
            SerializedObject serialized = new SerializedObject(circleView);
            serialized.FindProperty("_controller").objectReferenceValue = controller;
            serialized.FindProperty("_orbitView").objectReferenceValue = orbitView;
            serialized.FindProperty("_renderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(circleView);
        }

        private static PlayerActor FindSingleActor(
            PlayerActor[] actors,
            PlayerRole role)
        {
            PlayerActor result = null;
            for (int index = 0; index < actors.Length; index++)
            {
                PlayerActor candidate = actors[index];
                if (candidate.Definition == null ||
                    candidate.Definition.Role != role)
                    continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"当前场景存在多个 {role} 玩家，拒绝猜测装配对象。");
                result = candidate;
            }

            return result != null
                ? result
                : throw new InvalidOperationException(
                    $"当前场景没有找到 {role} 玩家。");
        }

        private static FixedSimulationLoop FindSingleSimulationLoop()
        {
            FixedSimulationLoop[] loops =
                UnityEngine.Object.FindObjectsByType<FixedSimulationLoop>(
                    FindObjectsInactive.Include);
            if (loops.Length != 1)
                throw new InvalidOperationException(
                    $"当前场景应且只能有一个固定帧循环，实际为 {loops.Length} 个。");
            return loops[0];
        }

        private static T RequireComponent<T>(PlayerActor actor)
            where T : Component
        {
            T component = actor.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{actor.name} 缺少 {typeof(T).Name}。");
        }

        private static void ConfigureController(
            DeepSeekRiceGuardController controller,
            DeepSeekRiceGuardConfig config,
            PlayerLifeStateController2D lifeState,
            PlayerDamageReceiver2D deepSeekReceiver,
            PlayerDamageReceiver2D harnessReceiver,
            Transform deepSeekRoot,
            Transform harnessRoot)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.FindProperty("_ownerLifeState").objectReferenceValue =
                lifeState;

            SerializedProperty protectedPlayers =
                serialized.FindProperty("_protectedPlayers");
            protectedPlayers.arraySize = 2;
            SetProtectedPlayer(
                protectedPlayers.GetArrayElementAtIndex(0),
                deepSeekReceiver,
                deepSeekRoot);
            SetProtectedPlayer(
                protectedPlayers.GetArrayElementAtIndex(1),
                harnessReceiver,
                harnessRoot);
            serialized.ApplyModifiedProperties();
        }

        private static void SetProtectedPlayer(
            SerializedProperty element,
            PlayerDamageReceiver2D receiver,
            Transform root)
        {
            element.FindPropertyRelative("_receiver").objectReferenceValue =
                receiver;
            element.FindPropertyRelative("_root").objectReferenceValue = root;
        }

        private static void AppendUniqueReference(
            UnityEngine.Object owner,
            string propertyName,
            MonoBehaviour component)
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty array = serialized.FindProperty(propertyName);
            if (array == null || !array.isArray)
                throw new InvalidOperationException(
                    $"{owner.name} 找不到数组字段 {propertyName}。");

            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue ==
                    component)
                    return;
            }

            int newIndex = array.arraySize;
            array.InsertArrayElementAtIndex(newIndex);
            array.GetArrayElementAtIndex(newIndex).objectReferenceValue =
                component;
            serialized.ApplyModifiedProperties();
        }
    }
}
