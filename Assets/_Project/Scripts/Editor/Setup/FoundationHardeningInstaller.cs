using System;
using DeepSleep.Runtime.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    /// <summary>Legacy 兼容入口；现役关卡只能通过 LevelSceneInstaller 的登记规则派生。</summary>
    public static class FoundationHardeningInstaller
    {
        [MenuItem("DeepSleep/Legacy/基础加固（转调关卡登记）")]
        private static void InstallMenu() => Debug.Log(InstallAll());

        public static string InstallAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            string[] paths = { "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" };
            foreach (string path in paths)
            {
                var existing = SceneManager.GetSceneByPath(path);
                if (existing.isLoaded && existing.isDirty) throw new InvalidOperationException("Unsaved scene: " + path);
            }
            var config = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            if (config == null) throw new InvalidOperationException("Network config missing");
            foreach (string path in paths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (!LevelSceneInstaller.TryApplyRegisteredScene(scene, out _))
                        throw new InvalidOperationException("Legacy scene must first be explicitly imported with LevelSceneInstaller: " + path);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed: " + path);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return "Legacy entry delegated both scenes to level registration; protocol=" + config.ProtocolVersion + ", content=" + config.ContentVersion;
        }
    }
}
