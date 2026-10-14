using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Players.Presentation;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Accessories;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Setup
{
    public static class DirectionalFlightInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            const string art="Assets/_Project/Art/Characters/PlayerActions/";
            var names=new[]{"SPR_DS_Backflight_v01","SPR_HA_ForwardFlight_v01","SPR_DS_BorrowedBadge_Backflight_v01"};
            var ppu=new[]{560f,560f,512f};
            var heads=new[]{new Vector2(.55f,.81f),new Vector2(.76f,.81f),new Vector2(.65f,.78f)};
            var backs=new[]{new Vector2(.45f,.43f),new Vector2(.57f,.43f),new Vector2(.56f,.44f)};
            var sprites=new Sprite[3];
            for(int i=0;i<3;i++)
            {
                var path=art+names[i]+".png";
                if(!File.Exists(path))File.Copy("docs/ArtProduction/20261013_DirectionalFlight/raw/"+names[i]+".png",path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.spritePixelsPerUnit=ppu[i];imp.spritePivot=Vector2.one*.5f;imp.alphaIsTransparency=true;
                imp.mipmapEnabled=false;imp.maxTextureSize=2048;imp.filterMode=FilterMode.Bilinear;imp.SaveAndReimport();
                sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var sets=new PlayerActionPoseSet[3];
            for(int i=0;i<3;i++)
            {
                sets[i]=AssetDatabase.LoadAssetAtPath<PlayerActionPoseSet>("Assets/_Project/Configs/Presentation/CFG_PlayerActions_"+new[]{"DS","HS","BorrowedBadge"}[i]+".asset");
                sets[i].Movement=AssetDatabase.LoadAssetAtPath<Sprite>(art+new[]{"SPR_DS_Glide_v01","SPR_HA_ForwardFlight_v01","SPR_DS_BorrowedBadge_Glide_Hidden_v02"}[i]+".png");
                sets[i].Backward=i==1?AssetDatabase.LoadAssetAtPath<Sprite>(art+"SPR_HA_Hover_v01.png"):sprites[i];
                EditorUtility.SetDirty(sets[i]);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:HeadwearDefinition"))
            {
                var a=AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var poses=a.Poses.ToList();
                for(int i=0;i<3;i++)
                {
                    if(poses.Any(p=>p.Pose==sprites[i]))continue;
                    var role=i==1?PlayerRole.Harness:PlayerRole.DeepSeek;
                    var original=poses.First(p=>p.Role==role);
                    poses.Add(new HeadwearPose{Pose=sprites[i],Role=role,Anchor=a.Layer==AccessoryLayer.Back?backs[i]:heads[i],Angle=original.Angle,
                        WidthRatio=original.WidthRatio*original.Pose.bounds.size.x/sprites[i].bounds.size.x});
                }
                a.Poses=poses.ToArray();EditorUtility.SetDirty(a);
            }
            var catalog=AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            var entries=catalog.Entries.ToList();uint next=entries.Max(e=>e.Id)+1;
            foreach(var s in sprites)if(!entries.Any(e=>e.Sprite==s))entries.Add(new NetworkSpriteCatalog.Entry{Id=next++,Sprite=s});
            catalog.Entries=entries.ToArray();EditorUtility.SetDirty(catalog);
            var network=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            network.ContentVersion="20261013-directional-flight-1";EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            var active=SceneManager.GetActiveScene();
            foreach(var level in new[]{"Gameplay_Prototype","World01_EarlyInternet","World02_2066"})
            {
                var path="Assets/Scenes/"+level+".unity";var scene=SceneManager.GetSceneByPath(path);
                bool loaded=scene.IsValid()&&scene.isLoaded;
                if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    foreach(var p in all.OfType<PlayerActionPosePresenter>())
                    {
                        p.Facing=p.Life.GetComponentInChildren<PlayerFacingController2D>(true);
                        p.DeepSeekTarget=p.Life.GetComponentInChildren<DeepSeekManualTargetController>(true);
                        p.Claude=all.OfType<ClaudeChapterEncounterDriver2D>().SingleOrDefault();
                        EditorUtility.SetDirty(p);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                finally{if(!loaded)EditorSceneManager.CloseScene(scene,true);}
            }
            SceneManager.SetActiveScene(active);
            return "3 directional sprites, 3 pose sets, accessory anchors, stable network IDs and 3 scenes installed.";
        }
    }
}
