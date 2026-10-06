using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Perception;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class DoubaoTargetingInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            foreach (string name in new[] { "PF_EN_Doubao", "PF_DB_WordBubble" })
            {
                string path = "Assets/_Project/Prefabs/Combat/Enemies/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { ConfigureBody(root, name == "PF_DB_WordBubble"); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AddLayer("Assets/_Project/Configs/Combat/DeepSeek/CFG_DS_ManualTargeting_Default.asset", "_targetLayers");
            AddLayer("Assets/_Project/Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset", "_targetLayers");
            AddLayer("Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset", "PerceptionLayers");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/World01_EarlyInternet.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var registry = roots.SelectMany(r => r.GetComponentsInChildren<CombatPerceptionRegistry2D>(true)).Single();
                foreach (var encounter in roots.SelectMany(r => r.GetComponentsInChildren<DoubaoWordWallEncounter2D>(true)))
                {
                    ConfigureBody(encounter.Boss.gameObject, false);
                    var serialized = new SerializedObject(encounter);
                    serialized.FindProperty("_perceptionRegistry").objectReferenceValue = registry;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
            return "Doubao and bubble perception wired; DS/HS manual targeting and AI layers updated.";
        }

        public static void ConfigureBody(GameObject root, bool bubble)
        {
            var body = root.GetComponent<CombatPerceptionBody2D>();
            if (body == null) body = root.AddComponent<CombatPerceptionBody2D>();
            body.Shape = root.GetComponent<Collider2D>();
            body.Body = root.GetComponent<Rigidbody2D>();
            body.Hitbox = root.GetComponent<DamageHitbox2D>();
            body.TargetValue = bubble ? 0f : 3f;
            body.ThreatTrackedAsObstacle = bubble;
            EditorUtility.SetDirty(body);
        }

        private static void AddLayer(string path, string property)
        {
            int layer = LayerMask.NameToLayer("DestructibleObstacle");
            if (layer < 0) throw new InvalidOperationException("Missing DestructibleObstacle layer.");
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty(property).intValue |= 1 << layer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
