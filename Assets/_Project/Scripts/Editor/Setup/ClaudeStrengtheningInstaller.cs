using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class ClaudeStrengtheningInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            const string path = "Assets/Scenes/World02_2066.unity";
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Unsaved World02 scene.");
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var e = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClaudeEncounter2D>(true)).Single();
                if (e.SecondaryEnergy == null)
                {
                    var copy = UnityEngine.Object.Instantiate(e.Energy.gameObject, e.Energy.transform.parent);
                    copy.name = "CL_Energy_Secondary";
                    if(copy.scene != scene) SceneManager.MoveGameObjectToScene(copy, scene);
                    e.SecondaryEnergy = copy.GetComponent<ClaudeEnergyPattern2D>();
                }
                var cut = e.TrackingCut;
                if (cut.CrossBlades == null || cut.CrossBlades.Length != 6) cut.CrossBlades = new SpriteRenderer[6];
                for(int i=0;i<6;i++)
                {
                    if(cut.CrossBlades[i]!=null) continue;
                    var copy = UnityEngine.Object.Instantiate(cut.Blades[i].gameObject, cut.Blades[i].transform.parent);
                    copy.name="CrossBlade_"+i;cut.CrossBlades[i]=copy.GetComponent<SpriteRenderer>();cut.CrossBlades[i].enabled=false;
                }
                e.Config.PermissionRecoverySeconds=5f;
                e.Energy.Config.PhaseTwoHealth=999;
                EditorUtility.SetDirty(e.Config);EditorUtility.SetDirty(e.Energy.Config);
                EditorUtility.SetDirty(e);EditorUtility.SetDirty(cut);
                if(PrefabUtility.IsPartOfPrefabInstance(e)) PrefabUtility.RecordPrefabInstancePropertyModifications(e);
                if(PrefabUtility.IsPartOfPrefabInstance(cut)) PrefabUtility.RecordPrefabInstancePropertyModifications(cut);
                string reason;if(!e.TryValidateConfiguration(out reason))throw new Exception(reason);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                var network=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
                Networking.NetworkBuildRevision.Apply(network);EditorUtility.SetDirty(network);AssetDatabase.SaveAssets();
                return "Claude configured: books 1.5s cast + 5s recovery, phase2 cross cuts and two 999HP energy instances. Protocol18.";
            }
            finally { if(opened)EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
