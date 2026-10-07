using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation.Accessories;
using DeepSleep.Runtime.Progression.Meta;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    public static class HeadwearChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static string RenderPoses(bool includeWings = false)
        {
            var crown=AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(HeadwearInstaller.DefinitionPath);
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root=new GameObject("HeadwearPosePreview");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var rt=new RenderTexture(1200,1200,24);Texture2D image=null;
            var old=RenderTexture.active;
            try
            {
                var camera=root.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=5.1f;
                camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.075f,.12f,.19f);camera.cullingMask=1<<31;camera.targetTexture=rt;
                for(int i=0;i<crown.Poses.Length;i++)
                {
                    var go=new GameObject("Pose",typeof(SpriteRenderer));go.layer=31;go.transform.SetParent(root.transform,false);
                    go.transform.position=new Vector3((i%3-1)*3.3f,(1-i/3)*3.3f,0);
                    var subject=go.GetComponent<SpriteRenderer>();subject.sprite=crown.Poses[i].Pose;
                    go.transform.localScale=Vector3.one*(2.65f/subject.sprite.bounds.size.y);
                    var acc=new GameObject("Headwear",typeof(SpriteRenderer));acc.layer=31;acc.transform.SetParent(go.transform,false);
                    var view=go.AddComponent<HeadwearSpriteView>();view.Subject=subject;view.Ornament=acc.GetComponent<SpriteRenderer>();view.SetEquipped(crown);
                    Call(view,"LateUpdate");
                    if(includeWings)
                    {
                        var wings=AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(BackwearInstaller.DefinitionPath);
                        var wingGo=new GameObject("Backwear",typeof(SpriteRenderer));wingGo.layer=31;wingGo.transform.SetParent(go.transform,false);
                        var wingView=go.AddComponent<HeadwearSpriteView>();wingView.Subject=subject;wingView.Ornament=wingGo.GetComponent<SpriteRenderer>();wingView.SetEquipped(wings);
                        Call(wingView,"LateUpdate");
                    }
                }
                camera.Render();RenderTexture.active=rt;image=new Texture2D(1200,1200,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,1200,1200),0,0);image.Apply();
                string path="docs/ImplementationEvidence/20261006_Headwear/"+(includeWings?"crown_wings_preview.png":"pose_preview.png");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());return Path.GetFullPath(path);
            }
            finally {RenderTexture.active=old;if(image!=null)Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        }
        public static string Run()
        {
            int checks=0; void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
            var crown=AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(HeadwearInstaller.DefinitionPath);
            Check(crown!=null && crown.Product.Price==20 && !crown.Product.Repeatable && crown.Product.IsHeadwear,"Product config");
            var root=new GameObject("HeadwearChecks");root.SetActive(false);
            string dir=Path.GetFullPath(Path.Combine("Temp","HeadwearChecks",Guid.NewGuid().ToString("N")));
            try
            {
                var profile=root.AddComponent<LocalPlayerProfileStore>();
                Set(profile,"TestSaveDirectory",dir);
                Type dataType=typeof(LocalPlayerProfileStore).Assembly.GetType("DeepSleep.Runtime.Progression.Meta.LocalPlayerProfileData");
                Set(profile,"_data",JsonUtility.FromJson("{\"version\":2,\"whaleVoucherBalance\":19}",dataType));
                var normalize=typeof(LocalPlayerProfileStore).GetMethod("TryNormalize",BindingFlags.Static|BindingFlags.NonPublic);
                Check((bool)normalize.Invoke(null,new[]{Get(profile,"_data")}),"Migrate v2");
                Check(profile.GetHeadwear(PlayerRole.DeepSeek)=="","Old profile no headwear");
                Check(!profile.TrySetHeadwear(PlayerRole.DeepSeek,crown.Product,out _),"Reject unowned equipment");
                Check(!profile.TryPurchase(crown.Product,out _) && profile.WhaleVoucherBalance==19,"Reject insufficient currency");
                Get(profile,"_data").GetType().GetField("whaleVoucherBalance").SetValue(Get(profile,"_data"),20);
                Check(profile.TryPurchase(crown.Product,out _) && profile.WhaleVoucherBalance==0 && profile.GetOwnedCount(crown.Product.ProductId)==1,"Buy for20");
                Check(!profile.TryPurchase(crown.Product,out _),"Reject duplicate");
                Check(profile.TrySetHeadwear(PlayerRole.DeepSeek,crown.Product,out _),"Equip DS");
                Check(profile.GetHeadwear(PlayerRole.Harness)=="","Role isolation");
                Check(profile.TrySetHeadwear(PlayerRole.Harness,crown.Product,out _),"Equip HS");
                Check(profile.TrySetHeadwear(PlayerRole.DeepSeek,null,out _) && profile.GetOwnedCount(crown.Product.ProductId)==1,"Unequip keeps ownership");
                Check(profile.TrySetHeadwear(PlayerRole.DeepSeek,crown.Product,out _),"Reequip");
                var reloaded=JsonUtility.FromJson(File.ReadAllText(profile.SavePath),dataType);
                Set(profile,"_data",reloaded);Check((bool)normalize.Invoke(null,new[]{reloaded}),"Saved profile valid");
                Check(profile.GetHeadwear(PlayerRole.DeepSeek)==crown.Product.ProductId && profile.GetHeadwear(PlayerRole.Harness)==crown.Product.ProductId,"Both roles survive reload");
                var wings=AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(BackwearInstaller.DefinitionPath);
                Check(wings!=null && wings.Product.Price==20 && wings.Product.Slot==AccessorySlot.Back && !wings.Product.Repeatable,"Wings product");
                Check(!profile.TrySetAccessory(PlayerRole.DeepSeek,AccessorySlot.Back,wings.Product,out _),"Unowned backwear rejected");
                Get(profile,"_data").GetType().GetField("whaleVoucherBalance").SetValue(Get(profile,"_data"),20);
                Check(profile.TryPurchase(wings.Product,out _) && profile.WhaleVoucherBalance==0,"Wings cost20");
                Check(!profile.TryPurchase(wings.Product,out _),"Wings permanent nonrepeatable");
                foreach(var role in new[]{PlayerRole.DeepSeek,PlayerRole.Harness})
                {
                    Check(profile.TrySetAccessory(role,AccessorySlot.Back,wings.Product,out _),"Equip backwear");
                    Check(profile.GetHeadwear(role)==crown.Product.ProductId,"Backwear preserves crown");
                    Check(!profile.TrySetAccessory(role,AccessorySlot.Front,wings.Product,out _),"Wrong slot rejected");
                    Check(profile.TrySetHeadwear(role,null,out _) && profile.GetAccessory(role,AccessorySlot.Back)==wings.Product.ProductId,"Remove crown keeps wings");
                    Check(profile.TrySetHeadwear(role,crown.Product,out _),"Reequip crown with wings");
                }
                Set(profile,"_data",JsonUtility.FromJson(File.ReadAllText(profile.SavePath),dataType));
                Check(profile.GetAccessory(PlayerRole.DeepSeek,AccessorySlot.Back)==wings.Product.ProductId && profile.GetAccessory(PlayerRole.Harness,AccessorySlot.Back)==wings.Product.ProductId,"Four equipped fields persist");

                var subject=new GameObject("Subject",typeof(SpriteRenderer));subject.transform.SetParent(root.transform);
                var renderer=subject.GetComponent<SpriteRenderer>();var view=subject.AddComponent<HeadwearSpriteView>();
                var accessory=new GameObject("Headwear",typeof(SpriteRenderer));accessory.transform.SetParent(subject.transform,false);
                view.Subject=renderer;view.Ornament=accessory.GetComponent<SpriteRenderer>();view.SetEquipped(crown);
                // 隔离根不启用；逐姿态检查数据及公式，不启动任何AI或战斗。
                foreach(var pose in crown.Poses)
                {
                    Check(pose.Pose!=null && pose.WidthRatio>0 && pose.Anchor.x>=0 && pose.Anchor.x<=1 && pose.Anchor.y>=0 && pose.Anchor.y<=1,"Pose anchor valid");
                    Check(crown.TryGetPose(pose.Pose,out var found) && found.Anchor==pose.Anchor,"Pose lookup");
                }
                Check(crown.Poses.Select(p=>p.Pose).Distinct().Count()==9,"Nine unique gameplay poses");
                var channel=root.AddComponent<HeadwearSessionPresenter>();var session=root.AddComponent<CoopSessionController>();
                var transport=new Capture();Set(session,"_transport",transport);
                session.Config=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
                channel.Session=session;channel.Catalog=new[]{crown,wings};channel.DeepSeek=view;channel.Harness=accessory.AddComponent<HeadwearSpriteView>();channel.Previews=Array.Empty<HeadwearImageView>();
                channel.DeepSeekBack=subject.AddComponent<HeadwearSpriteView>();channel.HarnessBack=accessory.AddComponent<HeadwearSpriteView>();
                Set(session,"<Phase>k__BackingField",SessionPhase.Playing);Set(session,"_hasPeer",true);
                channel.Bind(profile);
                Check(transport.Last!=null && transport.Last.Length==9,"Compact equipment packet");
                foreach(var hostRole in new[]{PlayerRole.DeepSeek,PlayerRole.Harness})
                {
                    Set(session,"<HostRole>k__BackingField",hostRole);Call(channel,"Refresh");
                    Read(channel,"ReadPeer",NetworkMessageCatalog.Peer.Headwear,0,0);
                    Check((ushort)Get(channel,hostRole==PlayerRole.DeepSeek?"_ds":"_hs")==1,"Peer cannot overwrite host");
                    Check((ushort)Get(channel,hostRole==PlayerRole.DeepSeek?"_hs":"_ds")==0,"Peer unequip");
                    Read(channel,"ReadPeer",NetworkMessageCatalog.Peer.Headwear,1,1);
                    Check(channel.DeepSeek.Equipped==crown && channel.Harness.Equipped==crown,"Both equipped");
                    Read(channel,"ReadPeer",NetworkMessageCatalog.Peer.Headwear,999,999);
                    Check(channel.DeepSeek.Equipped==crown && channel.Harness.Equipped==crown,"Unknown catalog ID ignored");
                    Read(channel,"ReadPeer",NetworkMessageCatalog.Peer.Headwear,1,1,2,2);
                    Check(channel.DeepSeekBack.Equipped==wings && channel.HarnessBack.Equipped==wings,"Both roles stack both accessories");
                    Read(channel,"ReadPeer",NetworkMessageCatalog.Peer.Headwear,1,1,0,0);
                    Check((hostRole==PlayerRole.DeepSeek?channel.DeepSeekBack:channel.HarnessBack).Equipped==wings,"Peer cannot remove host wings");
                    Check((hostRole==PlayerRole.DeepSeek?channel.HarnessBack:channel.DeepSeekBack).Equipped==null,"Peer removes own wings only");
                    Read(channel,"ReadPeer",NetworkMessageCatalog.Peer.Headwear,2,2,1,1);
                    Check(channel.DeepSeek.Equipped==crown && channel.Harness.Equipped==crown,"Wrong-slot network IDs rejected");
                }
                transport.IsServer=false;
                Read(channel,"ReadAuthority",NetworkMessageCatalog.Authority.Headwear,1,0,2,2);
                Check(channel.DeepSeek.Equipped==crown && channel.Harness.Equipped==null,"Authority replica applies");
                Check(channel.DeepSeekBack.Equipped==wings && channel.HarnessBack.Equipped==wings,"Authority replicates wings independently");
                channel.SetEnabledForTestCleanup();
                return "HEADWEAR PASS "+checks+" checks: isolated purchase/save/reload, ownership, role equip,9 pose bindings,host-role protection/replica/unknown IDs. Temporary save: "+dir+"\n"+NetworkProtocolChecks.RunCatalog();
            }
            finally {Object.DestroyImmediate(root);}
        }
        static void Read(object channel,string method,byte kind,ushort ds,ushort hs,ushort dsBack=0,ushort hsBack=0)
        {
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(ds);writer.Write(hs);writer.Write(dsBack);writer.Write(hsBack);writer.Flush();stream.Position=0;
            using var reader=new BinaryReader(stream);channel.GetType().GetMethod(method,Private).Invoke(channel,new object[]{kind,reader});
        }
        static void Call(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
        static object Get(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static void SetEnabledForTestCleanup(this HeadwearSessionPresenter channel)=>Call(channel,"OnDisable");
        sealed class Capture:ITransportAdapter
        {
            public byte[] Last; public bool IsServer{get;set;}=true; public bool IsConnected=>true;public string DisconnectReason=>"";
            public event Action<ulong> Connected{add{} remove{}}public event Action<ulong> Disconnected{add{} remove{}}public event Action<ulong,byte[]> Received{add{} remove{}}
            public bool StartHost()=>true;public bool StartClient(string address)=>true;public void Stop(){}
            public void Send(ulong peer,byte[] payload,bool reliable)=>Last=payload;
        }
    }
}
