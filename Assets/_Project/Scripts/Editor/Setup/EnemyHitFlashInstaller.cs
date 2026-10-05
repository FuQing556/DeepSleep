using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式装配选定 Prefab/场景的受击短闪；不改原图、碰撞、战斗参数与主体层级。</summary>
    public static class EnemyHitFlashInstaller
    {
        private const string MaterialPath = "Assets/_Project/Art/Shaders/MAT_PlayerHitOverlay.mat";

        public static string InstallEnemyPrefab(string path, string bodyPath)
        {
            RequireEdit();
            Material material = Material();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing explicit prefab: " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform bodyTransform = string.IsNullOrEmpty(bodyPath) ? root.transform : root.transform.Find(bodyPath);
                SpriteRenderer body = bodyTransform != null ? bodyTransform.GetComponent<SpriteRenderer>() : null;
                if (body == null) throw new InvalidOperationException("Missing explicit body sprite: " + path + "/" + bodyPath);
                MonoBehaviour source = root.GetComponent<EnemyActor2D>()?.Health;
                if (source == null) source = root.GetComponent<DoubaoBoss2D>();
                if (source is not IDamageFeedbackSource) throw new InvalidOperationException("No supported actual-damage source: " + path);
                Install(root, body, source, false, material, null);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Hit flash installed: " + path;
        }

        public static string InstallMirrorPrefab(string path)
        {
            RequireEdit();
            Material material = Material();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<NetworkEntityView>();
                if (view == null || view.Layers == null || view.Layers.Length == 0 || view.Layers.Any(r => r == null))
                    throw new InvalidOperationException("Mirror prefab needs explicit base layers.");
                view.HitFlash = Install(root, view.Layers[0], null, true, material, view.Layers);
                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Local-only mirror flash installed: " + path;
        }

        /// <summary>调用方选定并保存场景；只接豆包实例、权限排除和当前统一版本，不打开其他场景。</summary>
        public static string InstallScene(Scene scene)
        {
            RequireEdit();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Select a loaded scene.");
            Material material = Material();
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            int bosses = 0;
            foreach (var channel in all.OfType<DoubaoEncounterNetworkChannel>())
            {
                var data = new SerializedObject(channel);
                var boss = (DoubaoBoss2D)data.FindProperty("_boss").objectReferenceValue;
                if (boss == null || boss.gameObject.scene != scene) throw new InvalidOperationException("Boss reference is missing or belongs to another scene.");
                var bossData = new SerializedObject(boss);
                var body = (SpriteRenderer)bossData.FindProperty("_renderer").objectReferenceValue;
                var flash = Install(boss.gameObject, body, boss, false, material, null);
                data.FindProperty("_bossHitFlash").objectReferenceValue = flash;
                data.ApplyModifiedProperties();
                bosses++;
            }
            foreach (var gate in all.OfType<NetworkAuthorityGate>())
            {
                DeepSleep.Editor.Networking.NetworkAuthorityRules.Apply(gate);
            }
            foreach (var session in all.OfType<CoopSessionController>())
            {
                DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(session.Config);
                EditorUtility.SetDirty(session.Config);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return scene.name + ": flash scene bindings ready; boss count=" + bosses;
        }

        private static SpriteHitFlash2D Install(GameObject root, SpriteRenderer body, MonoBehaviour source,
            bool replicaOnly, Material material, SpriteRenderer[] explicitSources)
        {
            if (body == null) throw new InvalidOperationException("Explicit primary body is required.");
            var flash = root.GetComponent<SpriteHitFlash2D>();
            if (flash == null) flash = Undo.AddComponent<SpriteHitFlash2D>(root);
            SpriteRenderer overlay = flash.Overlay;
            if (overlay == null)
            {
                // 单独子对象保留原有美术/碰撞层级；根身体（豆包）覆盖层直接置于其子节点。
                Transform parent = body.transform == root.transform ? body.transform : body.transform.parent;
                if (parent.Find("EnemyHitFlashOverlay") != null)
                    throw new InvalidOperationException("Unowned EnemyHitFlashOverlay exists; bind it explicitly before applying.");
                var overlayObject = new GameObject("EnemyHitFlashOverlay");
                Undo.RegisterCreatedObjectUndo(overlayObject, "Create explicit enemy hit overlay");
                overlayObject.transform.SetParent(parent, false);
                overlay = Undo.AddComponent<SpriteRenderer>(overlayObject);
            }
            Undo.RecordObject(flash, "Bind enemy hit feedback");
            Undo.RecordObject(overlay, "Configure independent hit overlay");
            flash.DamageSource = source;
            flash.ReplicaOnly = replicaOnly;
            flash.Overlay = overlay;
            var baseSprites = explicitSources ?? root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r != overlay).ToArray();
            flash.Sources = new[] { body }.Concat(baseSprites.Where(r => r != body)).ToArray();
            overlay.sharedMaterial = material;
            overlay.enabled = false;
            overlay.sprite = body.sprite;
            overlay.sortingLayerID = body.sortingLayerID;
            overlay.sortingOrder = body.sortingOrder + 2;
            overlay.gameObject.layer = body.gameObject.layer;
            if (!flash.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(flash);
                PrefabUtility.RecordPrefabInstancePropertyModifications(overlay);
            }
            EditorUtility.SetDirty(flash);
            EditorUtility.SetDirty(overlay);
            return flash;
        }

        private static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null || material.shader == null || material.shader.name != "DeepSleep/PlayerHitOverlay" ||
                ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("Existing player overlay material/shader is missing or invalid.");
            return material;
        }

        private static void RequireEdit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before explicitly installing enemy feedback.");
        }
    }
}
