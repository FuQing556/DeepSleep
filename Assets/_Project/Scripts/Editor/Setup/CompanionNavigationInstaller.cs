using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class CompanionNavigationInstaller
    {
        [MenuItem("DeepSleep/设置/装配AI节点与动态导航")]
        public static string InstallAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            foreach (string path in new[] { "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try { Install(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            var net = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(net);
            EditorUtility.SetDirty(net);
            AssetDatabase.SaveAssets();
            return "AI node goals and visible hazard navigation wired in both gameplay scenes; ready rules unchanged.";
        }

        public static void Install(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var session = all.OfType<CoopSessionController>().Single();
            var assignment = all.OfType<PlayerControlAssignment>().Single();
            var node = all.OfType<RestNodePrototypeController2D>().Single();
            var registry = Get<CompanionObstacleRegistry2D>(assignment.gameObject);
            registry.Session = session;
            var squad = Get<CompanionSquadAnchor2D>(assignment.gameObject);
            squad.Session = session; squad.DeepSeek = session.DeepSeek.transform; squad.Harness = session.Harness.transform;
            foreach (var brain in all.OfType<CompanionCommandSource2D>())
            {
                brain.Sensor.ObstacleRegistry = registry;
                var goal = Get<CompanionNodeGoal2D>(brain.gameObject);
                goal.Node = node; goal.Session = session; goal.Assignment = assignment;
                goal.Actor = brain.Owner.GetComponent<DeepSleep.Runtime.Players.Identity.PlayerActor>();
                goal.Shape = brain.Shape; goal.Config = brain.Config;
                if (!goal.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
                brain.NodeGoal = goal; brain.SquadAnchor = squad;
                EditorUtility.SetDirty(goal); EditorUtility.SetDirty(brain); EditorUtility.SetDirty(brain.Sensor);
                EditorUtility.SetDirty(brain.Config);
            }
            foreach (var encounter in all.OfType<DoubaoWordWallEncounter2D>())
            {
                var so = new SerializedObject(encounter);
                so.FindProperty("_obstacleRegistry").objectReferenceValue = registry;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var gate = all.OfType<NetworkAuthorityGate>().Single();
            DeepSleep.Editor.Networking.NetworkAuthorityRules.Apply(gate, new Behaviour[] { registry, squad }
                .Concat(all.OfType<CompanionCommandSource2D>().Select(b => (Behaviour)b.NodeGoal)).ToArray());
            EditorUtility.SetDirty(registry); EditorUtility.SetDirty(squad); EditorUtility.SetDirty(gate);
        }

        private static T Get<T>(GameObject go) where T : Component
        { var c = go.GetComponent<T>(); if (c == null) c = go.AddComponent<T>(); return c; }
    }
}
