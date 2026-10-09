using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Setup
{
    /// <summary>仅用户授权调用的显式安装，不重建Kimi模块或覆盖碰撞体调参。</summary>
    public static class KimiPresentationInstaller
    {
        public const string ShieldPath = "Assets/_Project/Art/VFX/Kimi/VFX_KI_MoonShield_v01.png";

        public static void ConfigurePrefab()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出Play。");
            var importer = (TextureImporter)AssetImporter.GetAtPath(ShieldPath);
            if (importer == null) throw new InvalidOperationException("缺少月光罩素材。");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var root = PrefabUtility.LoadPrefabContents(KimiContentInstaller.RigPath);
            try
            {
                var boss = root.GetComponentInChildren<KimiBoss2D>(true);
                var hitbox = boss.GetComponent<DamageHitbox2D>();
                hitbox.UseReceiverHitFeedback = false;
                hitbox.DamageNumberAnchor = null;
                var view = boss.GetComponent<KimiBossPresentation2D>();
                if (view == null)
                {
                    view = boss.gameObject.AddComponent<KimiBossPresentation2D>();
                    view.Boss = boss; view.HitFlash = boss.GetComponent<SpriteHitFlash2D>();
                    // 同父节点保证退场影像复用人物实际等比变换，而非缩放物理根。
                    view.MoonShield = MakeRenderer(boss.Body.transform.parent, "MoonlightHitShield", boss.Body);
                    view.MoonShield.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShieldPath);
                    view.MoonShield.sortingOrder = boss.Body.sortingOrder + 3;
                    view.DepartingBody = MakeRenderer(boss.Body.transform.parent, "DepartingBody", boss.Body);
                    view.DepartingCloud = MakeRenderer(boss.Cloud.transform.parent, "DepartingCloud", boss.Cloud);
                    view.ShieldPeakAlpha = .5f;
                }
                var flash = new SerializedObject(view.HitFlash);
                ConfigureSphere(boss, view);
                view.Timing = BossPresentationTimingInstaller.GetTiming();
                flash.FindProperty("_duration").floatValue = .1f;
                flash.FindProperty("_peakAlpha").floatValue = 0;
                flash.FindProperty("_reducedAlpha").floatValue = 0;
                flash.ApplyModifiedPropertiesWithoutUndo();
                var curtain = root.GetComponentInChildren<KimiHitCurtain2D>(true);
                curtain.HitFlash = curtain.GetComponent<SpriteHitFlash2D>();
                curtain.IdleAlpha = .8f; curtain.HitAlpha = .5f;
                var curtainFlash = new SerializedObject(curtain.HitFlash);
                curtainFlash.FindProperty("_duration").floatValue = .1f;
                curtainFlash.FindProperty("_peakAlpha").floatValue = 0;
                curtainFlash.FindProperty("_reducedAlpha").floatValue = 0;
                curtainFlash.ApplyModifiedPropertiesWithoutUndo();
                if (!view.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
                PrefabUtility.SaveAsPrefabAsset(root, KimiContentInstaller.RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var laser = AssetDatabase.LoadAssetAtPath<KimiLaserConfig>(KimiContentInstaller.ConfigRoot + "/CFG_KI_Laser.asset");
            laser.PhaseTwoRayDelaySeconds = .08f; EditorUtility.SetDirty(laser);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSphere(KimiBoss2D boss, KimiBossPresentation2D view)
        {
            if (!(boss.HitCollider is CircleCollider2D))
            {
                var old = boss.HitCollider;
                var circle = boss.gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 2.6f;
                circle.offset = boss.transform.InverseTransformPoint(boss.Body.bounds.center);
                circle.enabled = false;
                boss.HitCollider = circle;
                boss.Perception.Shape = circle;
                UnityEngine.Object.DestroyImmediate(old);
            }
            boss.Perception.PassiveAttackTarget = false;
            view.ShieldIdleAlpha = .8f;
            view.ShieldPeakAlpha = .5f;
            var hitbox = boss.GetComponent<DamageHitbox2D>();
            hitbox.DamageNumberAnchor = null;
            hitbox.UseReceiverHitFeedback = false;
            hitbox.StopsPiercingBeams = true;
            var unusedAnchor = boss.transform.Find("DamageNumberAnchor");
            if (unusedAnchor != null) UnityEngine.Object.DestroyImmediate(unusedAnchor.gameObject);
            boss.Config.ContactDamageAmount = 1;
            boss.Config.ContactKnockbackDistance = .65f;
            boss.Config.ContactKnockbackSeconds = .18f;
            boss.Config.ContactIntervalSeconds = 1;
            EditorUtility.SetDirty(boss.Config);
        }

        public static void ConfigureChapter(KimiChapterEncounterDriver2D driver)
        {
            driver.Presentation = driver.Encounter.Boss.GetComponent<KimiBossPresentation2D>();
            if (driver.BackdropTransition == null)
            {
                var original = driver.Backdrop.GetComponent<FinitePanoramaLayer2D>();
                if (original == null) throw new InvalidOperationException("缺少正式全景适配器。");
                var go = new GameObject("KimiBackdropTransition");
                go.transform.SetParent(driver.Backdrop.transform.parent, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                EditorUtility.CopySerialized(driver.Backdrop, renderer);
                renderer.sortingOrder = driver.Backdrop.sortingOrder + 1; renderer.enabled = false;
                // 两个不同尺寸的全景各自等比覆盖超长屏和镜头移动，不拉伸位图。
                var fitter = go.AddComponent<FinitePanoramaLayer2D>();
                EditorUtility.CopySerialized(original, fitter);
                var serialized = new SerializedObject(fitter);
                serialized.FindProperty("_renderer").objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                driver.BackdropTransition = renderer;
            }
            EditorUtility.SetDirty(driver);
        }

        public static string Install()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("先退出Play并处理未保存场景。");
            ConfigurePrefab();
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/World01_EarlyInternet.unity");
            var driver = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KimiChapterEncounterDriver2D>(true)).Single();
            ConfigureChapter(driver);
            if (!driver.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
            foreach (string guid in AssetDatabase.FindAssets("t:NetworkTuningConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>(AssetDatabase.GUIDToAssetPath(guid));
                DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(config); EditorUtility.SetDirty(config);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && previous != scene.path) EditorSceneManager.OpenScene(previous);
            return "Kimi configured: spherical hit/contact shape, actual-point feedback, contact 1 HP/1s; shared skill colliders unchanged.";
        }

        private static SpriteRenderer MakeRenderer(Transform parent, string name, SpriteRenderer source)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source.sharedMaterial; renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder; renderer.enabled = false;
            return renderer;
        }
    }
}
