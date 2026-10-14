using System;
using System.Linq;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Presentation;
using DeepSleep.Runtime.Presentation.Accessories;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class PlaytestPlayerActionChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int count = 0;
            void Check(bool ok, string reason) { count++; if (!ok) throw new Exception(reason); }
            var catalog = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            var accessories = AssetDatabase.FindAssets("t:HeadwearDefinition").Select(g => AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:PlayerActionPoseSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<PlayerActionPoseSet>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var sprite in new[] { set.Movement, set.Backward, set.Hurt, set.Rescue, set.Celebrate })
                {
                    Check(sprite != null, set.name + " missing pose");
                    Check(catalog.Entries.Count(e => e.Sprite == sprite && e.Id > 0) == 1, "Pose has exactly one network ID");
                    foreach (var accessory in accessories) Check(accessory.TryGetPose(sprite, out _), accessory.name + " missing pose anchor");
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                    Check(importer.DoesSourceTextureHaveAlpha() && !importer.mipmapEnabled, "Transparent sprite import");
                }
                Check(PlayerActionPosePresenter.SelectMovementPose(set, set.Hurt, UnityEngine.Vector2.zero, 1) == set.Hurt, "Stationary returns selected idle");
                foreach (float facing in new[] { -1f, 1f })
                {
                    Check(PlayerActionPosePresenter.SelectMovementPose(set, set.Hurt, new UnityEngine.Vector2(facing * 3, 0), facing) == set.Movement, "Forward follows facing");
                    Check(PlayerActionPosePresenter.SelectMovementPose(set, set.Hurt, new UnityEngine.Vector2(-facing * 3, 0), facing) == set.Backward, "Backward follows facing");
                }
            }
            foreach (string level in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + level + ".unity");
                try
                {
                    var poses = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerActionPosePresenter>(true)).ToArray();
                    Check(poses.Length == 2, level + " requires both players");
                    foreach (var p in poses)
                    {
                        Check(p.Subject != null && p.Visual != null && p.Life != null && p.Motor != null && p.Damage != null && p.Revive != null, "Local player bindings survive reload");
                        Check(p.Session != null && p.Upgrades != null && p.Chapter != null, "Scene event/authority bindings survive reload");
                        Check(p.DefaultPoses != null, "Default appearance bound");
                        Check(p.Facing != null, "Facing bound");
                        Check(level != "World02_2066" || p.Claude != null, "Claude free-facing bound");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            return count + " action pose asset/binding checks passed";
        }
    }
}
