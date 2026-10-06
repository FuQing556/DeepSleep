using System;
using System.Linq;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.World.Scrolling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class CloudPresentationInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene changes first.");
            Sprite[] sprites = new Sprite[5];
            for (int i = 0; i < sprites.Length; i++)
            {
                string path = "Assets/_Project/Art/Backgrounds/Clouds/SPR_Cloud_0" + (i + 1) + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing cloud: " + path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 256; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_DecorativeCloud.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("Missing sprite-unlit shader.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, "Assets/_Project/Materials/MAT_DecorativeCloud.mat");
            }
            var active = SceneManager.GetActiveScene();
            foreach (string name in new[] { "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    if (name != "MainMenu")
                    {
                        var roots = scene.GetRootGameObjects();
                        var field = roots.SelectMany(r => r.GetComponentsInChildren<DecorativeCloudField>(true)).SingleOrDefault();
                        if (field == null) field = new GameObject("DecorativeClouds").AddComponent<DecorativeCloudField>();
                        field.ViewCamera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
                        field.Sprites = sprites;
                        ConfigureMotion(field, name == "World01_EarlyInternet");
                        if (field.Slots == null || field.Slots.Length == 0)
                        {
                            field.Slots = new SpriteRenderer[18];
                            for (int i = 0; i < field.Slots.Length; i++)
                            {
                                var slot = new GameObject("Cloud_" + i, typeof(SpriteRenderer));
                                slot.transform.SetParent(field.transform, false);
                                field.Slots[i] = slot.GetComponent<SpriteRenderer>();
                                field.Slots[i].sharedMaterial = material; field.Slots[i].enabled = false;
                                field.Slots[i].sortingLayerName = "Background";
                            }
                        }
                        EditorUtility.SetDirty(field);
                    }
                    AudioUiInstaller.InstallInActiveScene();
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active); AssetDatabase.SaveAssets();
            return "Five cloud sprites; two 18-slot collision-free fields; shared settings assembled in three scenes.";
        }
        public static void ConfigureMotion(DecorativeCloudField field, bool dusk)
        {
            field.Tint = dusk ? new Color(1, .93f, .90f, 1) : Color.white;
            field.Speed = new Vector2(.3f, .9f);
            field.RightEntryChance = .5f; field.EdgePadding = .15f;
            field.Bands = new[] {
                Band("Gameplay", 2, 10, .45f, 1f, 2.6f, 6.2f, .3f, .96f),
                Band("Gameplay", 22, 26, .35f, 1f, 3, 7, .25f, .95f),
                Band("Gameplay", 27, 29, .35f, 1f, 3, 7, .25f, .95f)
            };
            EditorUtility.SetDirty(field);
        }
        private static DecorativeCloudField.DepthBand Band(string layer, int low, int high, float minAlpha,
            float maxAlpha, float minWidth, float maxWidth, float lowY, float highY) => new DecorativeCloudField.DepthBand {
                SortingLayer = layer, Orders = new Vector2Int(low, high), Opacity = new Vector2(minAlpha, maxAlpha),
                Width = new Vector2(minWidth, maxWidth), ViewportY = new Vector2(lowY, highY)
            };
    }
}
