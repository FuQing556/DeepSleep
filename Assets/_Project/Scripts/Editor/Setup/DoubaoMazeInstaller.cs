using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class DoubaoMazeInstaller
    {
        public const string ConfigPath = "Assets/_Project/Configs/Combat/Enemies/CFG_DB_WordWall_Default.asset";
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            var config = AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(ConfigPath);
            var so = new SerializedObject(config);
            so.FindProperty("_useMaze").boolValue = true;
            so.FindProperty("_mazeColumns").intValue = 7;
            so.FindProperty("_mazeRowPitch").floatValue = 2.36f;
            so.FindProperty("_mazeSentenceGap").floatValue = .12f;
            so.FindProperty("_mazeHoldRows").intValue = 3;
            so.FindProperty("_mazeRouteRadius").floatValue = .75f;
            so.FindProperty("_poolCapacity").intValue = 240;
            so.FindProperty("_blockHeight").floatValue = 1.24f;
            so.FindProperty("_spawnTopPadding").floatValue = 1.6f;
            var patterns = so.FindProperty("_patterns");
            for(int i=0;i<patterns.arraySize;i++)
            {
                var slots=patterns.GetArrayElementAtIndex(i).FindPropertyRelative("_slots");
                for(int j=0;j<slots.arraySize;j++)
                {
                    var slot=slots.GetArrayElementAtIndex(j);
                    var offset=slot.FindPropertyRelative("_offset");
                    offset.vector2Value=new Vector2(offset.vector2Value.x<0?-.6f:.6f,
                        offset.vector2Value.y>0?.5f:-.5f);
                    slot.FindPropertyRelative("_width").floatValue=1.24f;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if(!config.TryValidate(out string reason))throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(config);
            var effect = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectConfig>(
                "Assets/_Project/Configs/Combat/Enemies/CFG_DB_BubbleBreak.asset");
            var effectSo = new SerializedObject(effect);
            // 覆盖最大整场清除，并给同帧普通破裂留余量。
            effectSo.FindProperty("_maximumCount").intValue = 288;
            effectSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            var melee=AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleeConfig>(
                "Assets/_Project/Configs/Combat/Harness/Melee/CFG_HA_Melee_Default.asset");
            // 这里只是刀刃/剑气伤害查询层，索敌仍使用敌人感知注册表。
            melee.EnemyLayers = melee.EnemyLayers.value | (1 << 15);
            EditorUtility.SetDirty(melee);
            var scene=SceneManager.GetSceneByPath(DuskPanoramaInstaller.ScenePath);
            bool opened=!scene.isLoaded;
            if(!opened && scene.isDirty)throw new InvalidOperationException("World01 has unsaved changes");
            if(opened)scene=EditorSceneManager.OpenScene(DuskPanoramaInstaller.ScenePath,OpenSceneMode.Additive);
            try
            {
                var channel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DoubaoEncounterNetworkChannel>(true)).Single();
                var net=new SerializedObject(channel);
                net.FindProperty("_maximumViews").intValue=config.PoolCapacity;
                net.ApplyModifiedPropertiesWithoutUndo();
                var driver=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DeepSleep.Runtime.Progression.Run.DoubaoChapterEncounterDriver2D>(true)).Single();
                var directors=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DeepSleep.Runtime.Combat.Enemies.EnemySpawnDirector2D>(true)).ToArray();
                var driverSo=new SerializedObject(driver);
                var refs=driverSo.FindProperty("_spawnDirectors");
                refs.arraySize=directors.Length;
                for(int i=0;i<directors.Length;i++)refs.GetArrayElementAtIndex(i).objectReferenceValue=directors[i];
                driverSo.FindProperty("_bossMaximumAlivePerChannel").intValue=3;
                driverSo.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            var network=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network);
            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            return "7-column route-protected maze installed; both pools 240; network content version advanced.";
        }

        // 仅更新本次间距，不重装场景或覆盖后续调过的其他玩法参数。
        public static string InstallSentenceSpacing()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            var config = AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(ConfigPath);
            var so = new SerializedObject(config);
            so.FindProperty("_mazeSentenceGap").floatValue = .12f;
            so.FindProperty("_mazeRowPitch").floatValue = config.MaximumGroupSize.y + .12f;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!config.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(config);
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network);
            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            return "Sentence spacing updated: pitch=" + config.MazeRowPitch + ", gap=" + config.MazeSentenceGap;
        }
    }
}
