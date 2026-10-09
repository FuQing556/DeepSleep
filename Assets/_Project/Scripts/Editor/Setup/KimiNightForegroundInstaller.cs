using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式装配夜晚堤岸；不重跑旧关卡安装器或覆盖用户摆位。</summary>
    public static class KimiNightForegroundInstaller
    {
        private const string Night = "Assets/_Project/Art/Foregrounds/World01/FG_W01_Riverbanks_Night_v01.png";
        private static readonly string[] Scenes = { "Assets/Scenes/World01_EarlyInternet.unity", "Assets/Scenes/World02_2066.unity" };

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            AssetDatabase.ImportAsset(Night, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Night);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100; // 与黄昏堤岸同 PPU 和切片，不更改 Transform。
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
#pragma warning disable CS0618
            importer.spritesheet = new[] {
                new SpriteMetaData { name="LeftBankNight", rect=new Rect(0,40,887,650), alignment=9, pivot=new Vector2(.5f,0) },
                new SpriteMetaData { name="RightBankNight", rect=new Rect(887,40,887,650), alignment=9, pivot=new Vector2(.5f,0) }
            };
#pragma warning restore CS0618
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Night).OfType<Sprite>().ToArray();
            foreach (string path in Scenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException(path + " has unsaved edits.");
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var driver = All<KimiChapterEncounterDriver2D>(scene).Single();
                    var banks = All<SpriteRenderer>(scene);
                    driver.Foregrounds = new[] { banks.Single(s => s.name == "LeftBank"), banks.Single(s => s.name == "RightBank") };
                    driver.NightForegrounds = new[] { sprites.Single(s => s.name == "LeftBankNight"), sprites.Single(s => s.name == "RightBankNight") };
                    driver.ForegroundTransitions = driver.Foregrounds.Select(Overlay).ToArray();
                    EditorUtility.SetDirty(driver);
                    if (PrefabUtility.IsPartOfPrefabInstance(driver)) PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            return "Night riverbanks installed in both Kimi assemblies; original transforms/collisions untouched.";
        }

        private static SpriteRenderer Overlay(SpriteRenderer bank)
        {
            var child = bank.transform.Find("NightTransition");
            if (child == null) { var go = new GameObject("NightTransition"); child = go.transform; child.SetParent(bank.transform, false); }
            var layer = child.GetComponent<SpriteRenderer>();
            if (layer == null) layer = child.gameObject.AddComponent<SpriteRenderer>();
            layer.sprite = bank.sprite;
            layer.sharedMaterial = bank.sharedMaterial;
            layer.sortingLayerID = bank.sortingLayerID;
            layer.sortingOrder = bank.sortingOrder + 1;
            layer.color = bank.color;
            layer.enabled = false;
            return layer;
        }

        public static string Verify()
        {
            int checks = 0;
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var driver = All<KimiChapterEncounterDriver2D>(scene).Single();
                    if (!driver.TryValidateConfiguration(out string reason)) throw new Exception(reason);
                    var day = driver.Foregrounds.Select(s => s.sprite).ToArray();
                    if (day.Length != 2) throw new Exception("Expected two banks.");
                    driver.ApplyReplica(true);
                    for (int i = 0; i < 2; i++)
                    {
                        var bank = driver.Foregrounds[i]; var overlay = driver.ForegroundTransitions[i];
                        if (bank.sprite != driver.NightForegrounds[i] || bank.sprite.rect != day[i].rect ||
                            bank.sprite.pivot != day[i].pivot || bank.sprite.pixelsPerUnit != day[i].pixelsPerUnit ||
                            overlay.sprite != day[i] || overlay.transform.parent != bank.transform ||
                            overlay.transform.localPosition != Vector3.zero || overlay.transform.localScale != Vector3.one ||
                            overlay.GetComponent<Collider2D>() != null) throw new Exception("Night geometry/assembly mismatch.");
                        checks++;
                    }
                    var type = typeof(KimiChapterEncounterDriver2D);
                    type.GetField("_backdropFadeAge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(driver, driver.BackdropFadeSeconds * .5f);
                    type.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, null);
                    for (int i = 0; i < 2; i++)
                    {
                        var overlay = driver.ForegroundTransitions[i];
                        if (!driver.Foregrounds[i].enabled)
                        { if (overlay.enabled) throw new Exception("Disabled bank became visible."); checks++; continue; }
                        if (Mathf.Abs(overlay.color.a / driver.Foregrounds[i].color.a - .5f) > .05f)
                            throw new Exception("Foreground transition midpoint mismatch.");
                        checks++;
                    }
                    float age = (float)type.GetField("_backdropFadeAge", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
                    driver.ApplyReplica(true);
                    if ((float)type.GetField("_backdropFadeAge", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver) != age)
                        throw new Exception("Duplicate replica restarted transition.");
                    checks++;
                    type.GetField("_backdropFadeAge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(driver, driver.BackdropFadeSeconds);
                    type.GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, null);
                    if (driver.ForegroundTransitions.Any(s => s.enabled)) throw new Exception("Night fade did not finish.");
                    checks++;
                    driver.ApplyReplica(false);
                    for (int i = 0; i < 2; i++)
                    { if (driver.Foregrounds[i].sprite != day[i] || driver.ForegroundTransitions[i].sprite != driver.NightForegrounds[i]) throw new Exception("Dusk restore failed."); checks++; }
                    type.GetMethod("SetNightForegrounds", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, new object[] { false, false });
                    if (driver.ForegroundTransitions.Any(s => s.enabled)) throw new Exception("Immediate cancellation failed.");
                    checks++;
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            return checks + " night foreground geometry/replica/fade/restore checks passed in isolated preview scenes.";
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        /// <summary>重放实际退出回调，覆盖前景先销毁、渐变子层先销毁和重复网络清理。</summary>
        public static string VerifyExitCleanup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int checks = 0;
            var errors = new System.Collections.Generic.List<string>();
            Application.LogCallback collect = (message, stack, type) =>
            { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message); };
            Application.logMessageReceived += collect;
            try
            {
                foreach (var path in Scenes)
                for (int mode = 0; mode < 3; mode++)
                {
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try
                    {
                        var driver = All<KimiChapterEncounterDriver2D>(scene).Single();
                        var original = driver.Foregrounds.Select(s => s.sprite).ToArray();
                        if (mode != 2) driver.ApplyReplica(true);
                        if (mode == 0) UnityEngine.Object.DestroyImmediate(driver.ForegroundTransitions[0].gameObject);
                        else foreach (var bank in driver.Foregrounds) UnityEngine.Object.DestroyImmediate(bank.gameObject);
                        driver.StopCombat(ChapterCombatStopReason.SceneExit);
                        driver.StopCombat(ChapterCombatStopReason.SceneExit);
                        typeof(KimiChapterEncounterDriver2D).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, null);
                        var channel = All<DeepSleep.Runtime.Networking.KimiEncounterNetworkChannel>(scene).Single();
                        channel.GetType().GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(channel, null);
                        checks += 4;
                        if (mode == 0)
                        {
                            for (int i = 0; i < 2; i++)
                            { if (driver.Foregrounds[i].sprite != original[i]) throw new Exception("Surviving bank did not restore."); checks++; }
                            if (driver.ForegroundTransitions[1].enabled) throw new Exception("Surviving overlay stayed enabled.");
                            checks++;
                        }
                    }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                    if (errors.Count != 0) throw new Exception("Exit emitted errors: " + string.Join("; ", errors));
                    checks++;
                }
            }
            finally { Application.logMessageReceived -= collect; }
            return checks + " foreground teardown checks passed; source/overlay destruction, pre-cache exit, repeated driver/network OnDisable and preview scene close emitted no errors.";
        }
    }
}
