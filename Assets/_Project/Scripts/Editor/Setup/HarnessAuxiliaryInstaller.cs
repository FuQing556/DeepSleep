using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class HarnessAuxiliaryInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first");
            const string root="Assets/_Project/";
            var cfg=AssetDatabase.LoadAssetAtPath<HarnessTerminalLaserConfig>(root+"Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset");
            cfg.AuxiliaryVerticalInset=.28f; cfg.AuxiliaryEdgeMargin=.5f; EditorUtility.SetDirty(cfg);
            var cat=AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(root+"Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            var so=new SerializedObject(cat);var defs=so.FindProperty("_definitions");
            for(int i=0;i<defs.arraySize;i++)
            {
                var d=defs.GetArrayElementAtIndex(i);
                if(d.FindPropertyRelative("_id").intValue!=(int)UpgradeCardId.TerminalArray)continue;
                d.FindPropertyRelative("_maximumRank").intValue=4;
                d.FindPropertyRelative("_baseTokenCost").intValue=50;d.FindPropertyRelative("_additionalCostPerRank").intValue=25;
                d.FindPropertyRelative("_description").stringValue="自动副炮+1，最多4门；随HS漂浮，攻击驻留、结束归队，自动分火、手动锁定集火。近战前2级各增加一道50%伤害剑气残响，后2级只增加副炮。";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var net=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>(root+"Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(net);EditorUtility.SetDirty(net);
            string path=root+"Prefabs/Players/PF_Player_Harness.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try { Bind(prefab.GetComponent<HarnessTerminalLaserPresenter>());PrefabUtility.SaveAsPrefabAsset(prefab,path); }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
            var active=SceneManager.GetActiveScene();
            foreach(string level in new[]{"Gameplay_Prototype","World01_EarlyInternet","World02_2066"})
            {
                var scenePath="Assets/Scenes/"+level+".unity";var scene=SceneManager.GetSceneByPath(scenePath);
                bool loaded=scene.IsValid()&&scene.isLoaded;if(!loaded)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
                try {foreach(var p in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HarnessTerminalLaserPresenter>(true)))Bind(p);
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                finally {if(!loaded)EditorSceneManager.CloseScene(scene,true);}
            }
            SceneManager.SetActiveScene(active);AssetDatabase.SaveAssets();return "Four auxiliary muzzles + below-character beams in prefab/3 scenes; array 50/75/100/125, DS=HS=1340; protocol20.";
        }
        private static void Bind(HarnessTerminalLaserPresenter presenter)
        {
            var so=new SerializedObject(presenter);
            var template=(SpriteRenderer)so.FindProperty("_shotView._muzzle").objectReferenceValue;
            var slots=so.FindProperty("_auxiliaryMuzzles");slots.arraySize=4;
            for(int i=0;i<4;i++)
            {
                if(slots.GetArrayElementAtIndex(i).objectReferenceValue!=null)continue;
                var muzzle=UnityEngine.Object.Instantiate(template,template.transform.parent);
                muzzle.name="AuxiliaryMuzzle_"+i;muzzle.enabled=false;muzzle.sortingOrder=-1;
                slots.GetArrayElementAtIndex(i).objectReferenceValue=muzzle;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach(var lane in presenter.GetComponentsInChildren<BeamTiledMeshView2D>(true))
            {var view=new SerializedObject(lane);view.FindProperty("_orderOffset").intValue=-4;view.ApplyModifiedPropertiesWithoutUndo();}
            EditorUtility.SetDirty(presenter);
            if(PrefabUtility.IsPartOfPrefabInstance(presenter))PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
        }
    }
}
