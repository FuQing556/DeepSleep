using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>重建伤害跳字的资源与 Gameplay 场景装配。</summary>
    public static class DamageNumberSceneInstaller
    {
        private const string PrefabFolder =
            "Assets/_Project/Prefabs/Presentation/DamageNumbers";
        private const string ConfigFolder =
            "Assets/_Project/Configs/Presentation/DamageNumbers";
        private const string PrefabPath =
            PrefabFolder + "/PF_DamageNumberEntry.prefab";
        private const string ArtFolder =
            "Assets/_Project/Art/UI/DamageNumbers";

        [MenuItem("Tools/DeepSleep/Setup/Install Damage Numbers")]
        public static void Install()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(ConfigFolder);
            ImportGlyphTextures();

            DamageNumberStyleConfig deepSeekStyle =
                LoadOrCreateStyle("CFG_DamageNumberStyle_DS", true);
            DamageNumberStyleConfig harnessStyle =
                LoadOrCreateStyle("CFG_DamageNumberStyle_HS", false);
            DamageNumberEntryView entryPrefab = CreateEntryPrefab();
            InstallSceneRoot(deepSeekStyle, harnessStyle, entryPrefab);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[DamageNumbers] 两套跳字资源与场景装配已更新。");
        }

        private static DamageNumberStyleConfig LoadOrCreateStyle(
            string fileName,
            bool deepSeek)
        {
            string path = $"{ConfigFolder}/{fileName}.asset";
            DamageNumberStyleConfig style =
                AssetDatabase.LoadAssetAtPath<DamageNumberStyleConfig>(path);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<
                    DamageNumberStyleConfig>();
                AssetDatabase.CreateAsset(style, path);
            }

            SerializedObject serialized = new(style);
            string role = deepSeek ? "DeepSeek" : "Harness";
            string prefix = deepSeek ? "DS" : "HS";
            SerializedProperty digits = serialized.FindProperty("_digits");
            digits.arraySize = 10;
            for (int index = 0; index < 10; index++)
            {
                digits.GetArrayElementAtIndex(index).objectReferenceValue =
                    LoadGlyph(role, prefix, index.ToString());
            }
            serialized.FindProperty("_decimalPoint").objectReferenceValue =
                LoadGlyph(role, prefix, "Decimal");
            serialized.FindProperty("_glyphHeightPixels").floatValue = 71f;
            serialized.FindProperty("_glyphSpacingPixels").floatValue = -6f;
            // HS 的外围碎晶面积更大、白色字芯更窄；只按透明轮廓等高会
            // 在实机中显得比 DS 小。这里保留一层最终视觉补偿。
            serialized.FindProperty("_visualScale").floatValue =
                deepSeek ? 1f : 1.10f;
            serialized.FindProperty("_durationSeconds").floatValue =
                deepSeek ? 0.74f : 0.68f;
            serialized.FindProperty("_fadeStart01").floatValue =
                deepSeek ? 0.50f : 0.44f;
            serialized.FindProperty("_screenOffset").vector2Value =
                new Vector2(0f, 24f);
            serialized.FindProperty("_horizontalScatterPixels").floatValue =
                deepSeek ? 11f : 9f;
            serialized.FindProperty("_risePixels").floatValue =
                deepSeek ? 52f : 58f;
            serialized.FindProperty("_rotationRangeDegrees").vector2Value =
                deepSeek ? new Vector2(-2f, 2f) :
                new Vector2(-4f, 4f);
            serialized.FindProperty("_startScale").floatValue = 0.72f;
            serialized.FindProperty("_peakScale").floatValue = 1.12f;
            serialized.FindProperty("_endScale").floatValue = 0.94f;
            serialized.FindProperty("_peakTime01").floatValue =
                deepSeek ? 0.20f : 0.15f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            return style;
        }

        private static DamageNumberEntryView CreateEntryPrefab()
        {
            GameObject template = new(
                "PF_DamageNumberEntry", typeof(RectTransform));
            RectTransform rect = (RectTransform)template.transform;
            rect.sizeDelta = new Vector2(180f, 80f);
            rect.anchorMin = rect.anchorMax = rect.pivot =
                new Vector2(0.5f, 0.5f);

            Image[] glyphImages = new Image[8];
            for (int index = 0; index < glyphImages.Length; index++)
            {
                GameObject glyph = new(
                    $"Glyph_{index}", typeof(RectTransform), typeof(Image));
                glyph.transform.SetParent(template.transform, false);
                RectTransform glyphRect = (RectTransform)glyph.transform;
                glyphRect.anchorMin = glyphRect.anchorMax = glyphRect.pivot =
                    new Vector2(0.5f, 0.5f);
                Image image = glyph.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                glyphImages[index] = image;
            }

            DamageNumberEntryView entry =
                template.AddComponent<DamageNumberEntryView>();
            SerializedObject serialized = new(entry);
            serialized.FindProperty("_rectTransform").objectReferenceValue =
                rect;
            SerializedProperty glyphs =
                serialized.FindProperty("_glyphImages");
            glyphs.arraySize = glyphImages.Length;
            for (int index = 0; index < glyphImages.Length; index++)
            {
                glyphs.GetArrayElementAtIndex(index).objectReferenceValue =
                    glyphImages[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                template, PrefabPath);
            Object.DestroyImmediate(template);
            return saved.GetComponent<DamageNumberEntryView>();
        }

        private static void ImportGlyphTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:Texture2D", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static Sprite LoadGlyph(
            string role,
            string prefix,
            string token)
        {
            string path = $"{ArtFolder}/{role}/" +
                $"SPR_{prefix}_DamageDigit_{token}_v01.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException(
                    $"伤害数字字形未导入：{path}");
            }

            return sprite;
        }

        private static void InstallSceneRoot(
            DamageNumberStyleConfig deepSeekStyle,
            DamageNumberStyleConfig harnessStyle,
            DamageNumberEntryView entryPrefab)
        {
            GameObject previous = GameObject.Find("UI_DamageNumbers");
            if (previous != null)
            {
                Object.DestroyImmediate(previous);
            }

            GameObject root = new(
                "UI_DamageNumbers",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            RectTransform canvasRect = (RectTransform)root.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject poolObject = new("Pool", typeof(RectTransform));
            poolObject.transform.SetParent(root.transform, false);
            RectTransform poolRect =
                (RectTransform)poolObject.transform;
            poolRect.anchorMin = Vector2.zero;
            poolRect.anchorMax = Vector2.one;
            poolRect.offsetMin = poolRect.offsetMax = Vector2.zero;

            CombatDamageNumberPresenter2D presenter =
                root.AddComponent<CombatDamageNumberPresenter2D>();
            SerializedObject serialized = new(presenter);
            serialized.FindProperty("_riceProjectilePool")
                .objectReferenceValue = Object.FindAnyObjectByType<
                    RiceProjectilePool>(FindObjectsInactive.Include);
            serialized.FindProperty("_laserDamage").objectReferenceValue =
                Object.FindAnyObjectByType<
                    HarnessTerminalLaserDamageExecutor2D>(
                    FindObjectsInactive.Include);
            serialized.FindProperty("_meleeDamage").objectReferenceValue =
                Object.FindAnyObjectByType<
                    HarnessMeleeDamageExecutor2D>(
                    FindObjectsInactive.Include);
            serialized.FindProperty("_worldCamera").objectReferenceValue =
                Camera.main;
            serialized.FindProperty("_canvas").objectReferenceValue = canvas;
            serialized.FindProperty("_canvasRect").objectReferenceValue =
                canvasRect;
            serialized.FindProperty("_poolRoot").objectReferenceValue =
                poolObject.transform;
            serialized.FindProperty("_entryPrefab").objectReferenceValue =
                entryPrefab;
            serialized.FindProperty("_deepSeekStyle").objectReferenceValue =
                deepSeekStyle;
            serialized.FindProperty("_harnessStyle").objectReferenceValue =
                harnessStyle;
            serialized.FindProperty("_prewarmCount").intValue = 16;
            serialized.FindProperty("_maximumCount").intValue = 48;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
