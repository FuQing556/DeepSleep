using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Presentation;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Presentation.Accessories;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class PlaytestPlayerActionsInstaller
    {
        private const string Art = "Assets/_Project/Art/Characters/PlayerActions";
        private const string Config = "Assets/_Project/Configs/Presentation";
        private static readonly string[] Names = {
            "SPR_DS_Glide_v01", "SPR_DS_Hurt_v01", "SPR_DS_Rescue_v01", "SPR_DS_Celebrate_v01",
            "SPR_HA_Hover_v01", "SPR_HA_Hurt_v01", "SPR_HA_Rescue_v01", "SPR_HA_Celebrate_v01",
            "SPR_DS_BorrowedBadge_Glide_Hidden_v02", "SPR_DS_BorrowedBadge_Hurt_Hidden_v01",
            "SPR_DS_BorrowedBadge_Rescue_Hidden_v01", "SPR_DS_BorrowedBadge_Celebrate_Hidden_v01" };
        // 按头部标尺做等比导入；不对图像非等比拉伸，不改角色物理根。
        private static readonly float[] Ppu = { 576, 560, 576, 550, 540, 540, 540, 540, 512, 560, 575, 575 };
        private static readonly Vector2[] Heads = {
            new(.74f,.82f),new(.61f,.86f),new(.69f,.86f),new(.58f,.86f),
            new(.69f,.85f),new(.69f,.85f),new(.72f,.86f),new(.69f,.86f),
            new(.76f,.80f),new(.58f,.85f),new(.63f,.87f),new(.57f,.87f) };
        private static readonly Vector2[] Backs = {
            new(.59f,.43f),new(.55f,.46f),new(.57f,.47f),new(.54f,.48f),
            new(.60f,.46f),new(.60f,.46f),new(.57f,.46f),new(.59f,.48f),
            new(.61f,.42f),new(.55f,.46f),new(.55f,.45f),new(.55f,.47f) };

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
            if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder("Assets/_Project/Art/Characters", "PlayerActions");
            var sprites = new Sprite[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = Art + "/" + Names[i] + ".png";
                if (!File.Exists(path)) File.Copy("docs/ArtProduction/20261012_PlayerActions/raw/" + Names[i] + ".png", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu[i]; importer.spritePivot = Vector2.one * .5f;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
                importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var sets = new PlayerActionPoseSet[3];
            for (int i = 0; i < sets.Length; i++)
            {
                string path = Config + "/CFG_PlayerActions_" + new[] { "DS", "HS", "BorrowedBadge" }[i] + ".asset";
                sets[i] = AssetDatabase.LoadAssetAtPath<PlayerActionPoseSet>(path);
                if (sets[i] == null) { sets[i] = ScriptableObject.CreateInstance<PlayerActionPoseSet>(); AssetDatabase.CreateAsset(sets[i], path); }
                if (sets[i].Backward == null) sets[i].Movement = sprites[i*4];
                sets[i].Hurt = sprites[i*4+1];
                sets[i].Rescue = sprites[i*4+2]; sets[i].Celebrate = sprites[i*4+3];
                EditorUtility.SetDirty(sets[i]);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:HeadwearDefinition"))
            {
                var accessory = AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var list = accessory.Poses.ToList();
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (list.Any(p => p.Pose == sprites[i])) continue; // 保留后续用户逐姿态调参。
                    var role = i >= 4 && i < 8 ? PlayerRole.Harness : PlayerRole.DeepSeek;
                    var original = list.First(p => p.Role == role);
                    list.Add(new HeadwearPose { Pose = sprites[i], Role = role,
                        Anchor = accessory.Layer == AccessoryLayer.Back ? Backs[i] : Heads[i], Angle = original.Angle,
                        WidthRatio = original.WidthRatio * original.Pose.bounds.size.x / sprites[i].bounds.size.x });
                }
                accessory.Poses = list.ToArray(); EditorUtility.SetDirty(accessory);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            var entries = catalog.Entries.ToList(); uint next = entries.Max(e => e.Id) + 1;
            foreach (var sprite in sprites) if (!entries.Any(e => e.Sprite == sprite)) entries.Add(new NetworkSpriteCatalog.Entry { Id = next++, Sprite = sprite });
            catalog.Entries = entries.ToArray(); EditorUtility.SetDirty(catalog);
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            network.ContentVersion = "20261012-playtest-actions-1"; EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            var active = SceneManager.GetActiveScene();
            foreach (string level in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                string path = "Assets/Scenes/" + level + ".unity";
                var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    foreach (var visual in all.OfType<PlayerDownedVisual2D>())
                    {
                        var presenter = visual.GetComponent<PlayerActionPosePresenter>();
                        if (presenter == null) presenter = visual.gameObject.AddComponent<PlayerActionPosePresenter>();
                        var root = visual.GetComponentInParent<PlayerLifeStateController2D>();
                        if (root == null) throw new InvalidOperationException("Player visual must belong to a life controller.");
                        presenter.Life = root; presenter.Visual = visual;
                        presenter.Subject = (SpriteRenderer)new SerializedObject(visual).FindProperty("_characterRenderer").objectReferenceValue;
                        presenter.Laser = root.GetComponentInChildren<HarnessTerminalLaserPresenter>(true);
                        presenter.Melee = root.GetComponentInChildren<HarnessMeleeController>(true);
                        presenter.Role = presenter.Laser != null ? PlayerRole.Harness : PlayerRole.DeepSeek;
                        presenter.Motor = root.GetComponentInChildren<PlayerMovementMotor2D>(true);
                        presenter.Damage = root.GetComponentInChildren<PlayerDamageReceiver2D>(true);
                        presenter.Revive = root.GetComponentInChildren<PlayerReviveActionChannel>(true);
                        presenter.Session = all.OfType<CoopSessionController>().Single();
                        presenter.Chapter = all.OfType<ChapterRunController>().Single();
                        presenter.Upgrades = all.OfType<PlayerUpgradeRuntimeState>().Single();
                        presenter.DefaultPoses = sets[presenter.Role == PlayerRole.Harness ? 1 : 0];
                        presenter.SkinPoses = presenter.Role == PlayerRole.DeepSeek ? sets[2] : null;
                        EditorUtility.SetDirty(presenter);
                        if (PrefabUtility.IsPartOfPrefabInstance(presenter)) PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            return "12 poses imported; three appearance sets, accessory anchors and network sprite IDs bound in three battle scenes.";
        }
    }
}
