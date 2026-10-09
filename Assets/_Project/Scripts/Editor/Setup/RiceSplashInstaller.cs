using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Setup
{
    public static class RiceSplashInstaller
    {
        private const string Root = "Assets/_Project/";
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            string image = Root + "Art/VFX/DeepSeek/Rice/VFX_DS_RiceSplash_v01.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(image);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            string vfxPath = Root + "Prefabs/Combat/VFX/DeepSeek/PF_VFX_DS_RiceSplash.prefab";
            if (AssetDatabase.LoadMainAssetAtPath(vfxPath) == null)
                AssetDatabase.CopyAsset(Root + "Prefabs/Combat/VFX/DeepSeek/PF_VFX_DS_RiceHit.prefab", vfxPath);
            var vfx = PrefabUtility.LoadPrefabContents(vfxPath);
            try
            {
                vfx.name = "PF_VFX_DS_RiceSplash";
                vfx.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(image);
                PrefabUtility.SaveAsPrefabAsset(vfx, vfxPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(vfx); }
            var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath).GetComponent<OneShotSpriteEffect2D>();
            string configPath = Root + "Configs/Presentation/DeepSeek/CFG_VFX_DS_RiceSplash_Default.asset";
            if (AssetDatabase.LoadMainAssetAtPath(configPath) == null)
                AssetDatabase.CopyAsset(Root + "Configs/Presentation/DeepSeek/CFG_VFX_DS_RiceHit_Default.asset", configPath);
            var config = AssetDatabase.LoadAssetAtPath<OneShotSpriteEffectConfig>(configPath);
            var values = new SerializedObject(config);
            values.FindProperty("_durationSeconds").floatValue = .36f;
            values.FindProperty("_worldDiameter").floatValue = 1f;
            values.FindProperty("_startScaleMultiplier").floatValue = .35f;
            values.FindProperty("_endScaleMultiplier").floatValue = 1f;
            values.FindProperty("_fadeStart01").floatValue = .2f;
            values.FindProperty("_minimumRotationDegrees").floatValue = 12f;
            values.FindProperty("_maximumRotationDegrees").floatValue = 28f;
            values.FindProperty("_maximumCount").intValue = 128;
            values.FindProperty("_prewarmCount").intValue = 32;
            values.ApplyModifiedPropertiesWithoutUndo();
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(Root + "Configs/Progression/CFG_UpgradeCatalog_Default.asset");
            if (!catalog.TryGet(UpgradeCardId.RiceSplash, out _))
            {
                var cards = new List<UpgradeDefinition>(catalog.Definitions);
                cards.Add(new UpgradeDefinition(UpgradeCardId.RiceSplash, "饭团溅射",
                    "命中时在1.2单位半径内爆炸，周围敌人受50%伤害（向下取整，最低1）；直击目标不重复受伤。",
                    PlayerRoleMask.DeepSeek, UpgradeOwnershipScope.Role, 1, 36, 0, UpgradeEffectKind.RiceSplash, 1));
                catalog.SetDefinitions(cards.ToArray());
                EditorUtility.SetDirty(catalog);
            }
            string playerPath = Root + "Prefabs/Players/PF_Player_DeepSeek.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                Wire(player.GetComponent<DeepSeekRiceHitEffectPresenter2D>(), effectPrefab, config);
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            string hsPath = Root + "Prefabs/Players/PF_Player_Harness.prefab";
            var hs = PrefabUtility.LoadPrefabContents(hsPath);
            try
            {
                foreach (var beam in hs.GetComponentsInChildren<BeamTiledMeshView2D>(true)) Tighten(beam);
                PrefabUtility.SaveAsPrefabAsset(hs, hsPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hs); }
            var scene = EditorSceneManager.GetActiveScene();
            bool openedScene = scene.path != "Assets/Scenes/Gameplay_Prototype.unity";
            if (openedScene)
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay_Prototype.unity", OpenSceneMode.Additive);
            var session = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CoopSessionController>(true)).Single();
            foreach (var presenter in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DeepSeekRiceHitEffectPresenter2D>(true)))
            {
                var pool = Wire(presenter, effectPrefab, config);
                var channel = pool.GetComponent<NetworkEffectEventChannel>();
                if (channel == null) channel = pool.gameObject.AddComponent<NetworkEffectEventChannel>();
                channel.Session = session; channel.Pool = pool; channel.EffectId = 110;
                EditorUtility.SetDirty(channel);
            }
            foreach (var beam in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BeamTiledMeshView2D>(true))) Tighten(beam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>(Root + "Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network);
            EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            return "溅射贴图、Prefab、配置、卡池、DS及场景和网络特效110已装配；HS叠层已收紧。";
        }
        private static void Tighten(BeamTiledMeshView2D beam)
        {
            var so = new SerializedObject(beam);
            so.FindProperty("_secondLayerWidth").floatValue = 1.15f;
            so.FindProperty("_thirdLayerWidth").floatValue = 1.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static OneShotSpriteEffectPool2D Wire(DeepSeekRiceHitEffectPresenter2D presenter,
            OneShotSpriteEffect2D prefab, OneShotSpriteEffectConfig config)
        {
            var so = new SerializedObject(presenter);
            var field = so.FindProperty("_splashEffectPool");
            var pool = field.objectReferenceValue as OneShotSpriteEffectPool2D;
            if (pool == null)
            {
                var child = new GameObject("RiceSplashEffectPoolRoot");
                child.transform.SetParent(presenter.transform, false);
                pool = child.AddComponent<OneShotSpriteEffectPool2D>();
            }
            var ps = new SerializedObject(pool);
            ps.FindProperty("_effectPrefab").objectReferenceValue = prefab;
            ps.FindProperty("_config").objectReferenceValue = config;
            ps.FindProperty("_poolRoot").objectReferenceValue = pool.transform;
            ps.ApplyModifiedPropertiesWithoutUndo();
            field.objectReferenceValue = pool;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pool;
        }
    }
}
