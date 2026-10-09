using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class BossPresentationTimingInstaller
    {
        public const string TimingPath = "Assets/_Project/Configs/Combat/Encounters/CFG_BossPresentationTiming.asset";
        public static BossPresentationTiming GetTiming()
        {
            var timing = AssetDatabase.LoadAssetAtPath<BossPresentationTiming>(TimingPath);
            if (timing != null) return timing;
            timing = ScriptableObject.CreateInstance<BossPresentationTiming>();
            AssetDatabase.CreateAsset(timing, TimingPath);
            return timing;
        }
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var timing = GetTiming();
            var ki = AssetDatabase.LoadAssetAtPath<KimiEncounterConfig>("Assets/_Project/Configs/Combat/Encounters/Kimi/CFG_KI_Encounter.asset");
            var cl = AssetDatabase.LoadAssetAtPath<ClaudeEncounterConfig>("Assets/_Project/Configs/Combat/Encounters/Claude/CFG_CL_Encounter.asset");
            ki.Timing = cl.Timing = timing;
            EditorUtility.SetDirty(ki); EditorUtility.SetDirty(cl);
            var prefab = PrefabUtility.LoadPrefabContents(KimiContentInstaller.RigPath);
            try
            {
                prefab.GetComponentInChildren<KimiBossPresentation2D>(true).Timing = timing;
                PrefabUtility.SaveAsPrefabAsset(prefab, KimiContentInstaller.RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (string path in new[] { "Assets/Scenes/World01_EarlyInternet.unity", "Assets/Scenes/World02_2066.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var presentation in All<KimiBossPresentation2D>(scene))
                    {
                        presentation.Timing = timing; EditorUtility.SetDirty(presentation);
                        if (PrefabUtility.IsPartOfPrefabInstance(presentation)) PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
                    }
                    foreach (var presentation in All<ClaudeEncounterPresentation2D>(scene))
                    {
                        presentation.Timing = timing;
                        presentation.Barrier = All<BossBarrierFeedback2D>(scene).Single();
                        EditorUtility.SetDirty(presentation);
                    }
                    BindHuds(scene);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            Networking.NetworkBuildRevision.Apply(network); EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            return "Shared Boss timing installed; profile 3/1/.25/.5/2/1.5; protocol 17. No collision, health or skill-gap changes.";
        }
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void BindHuds(Scene scene)
        {
            foreach (var hud in All<KimiBossHudView>(scene))
            {
                hud.Presentation = hud.Boss.GetComponent<KimiBossPresentation2D>();
                EditorUtility.SetDirty(hud);
                if (PrefabUtility.IsPartOfPrefabInstance(hud)) PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            }
            foreach (var hud in All<BossHealthHudView>(scene))
            {
                hud.Barrier = All<BossBarrierFeedback2D>(scene).Single(b => b.Boss == hud.Boss);
                EditorUtility.SetDirty(hud);
            }
        }
        public static string InstallHudVisibility()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            foreach (string path in new[] { "Assets/Scenes/World01_EarlyInternet.unity", "Assets/Scenes/World02_2066.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    BindHuds(scene);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            return "Both Boss HUDs bound to shield entrance progress; no gameplay parameters changed.";
        }
    }
}
