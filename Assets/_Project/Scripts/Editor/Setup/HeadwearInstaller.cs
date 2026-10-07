using System;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation.Accessories;
using DeepSleep.Runtime.Progression.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    public static class HeadwearInstaller
    {
        const string Root="Assets/_Project/";
        public const string DefinitionPath=Root+"Configs/Progression/Meta/CFG_Headwear_LittleCrown.asset";
        public const string ProductPath=Root+"Configs/Progression/Meta/Products/CFG_META_Product_little_crown.asset";
        public static string Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before installing.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save current scene first.");
            var product=AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(ProductPath);
            if(product==null)
            {
                product=ScriptableObject.CreateInstance<ShopProductDefinition>(); AssetDatabase.CreateAsset(product,ProductPath);
                Set(product,"_productId","little_crown"); Set(product,"_displayName","小皇冠");
                Set(product,"_description","永久头饰 · 无属性加成 · 双角色独立佩戴");
                Set(product,"_price",20); Set(product,"_repeatable",false); Set(product,"_isHeadwear",true);
                Set(product,"_icon",SpriteAt(Root+"Art/Accessories/SPR_ACC_LittleCrown_v01.png"));
            }
            var crown=AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(DefinitionPath);
            if(crown==null)
            {
                crown=ScriptableObject.CreateInstance<HeadwearDefinition>(); AssetDatabase.CreateAsset(crown,DefinitionPath);
                crown.NetworkId=1; crown.Product=product; crown.Sprite=product.Icon; crown.BaseAnchor=new Vector2(.5f,.355f);
                crown.Poses=new[]{
                    Pose("DeepSeek/SPR_DS_Idle_Base_v01.png",.568f,.866f,0,PlayerRole.DeepSeek),
                    Pose("DeepSeek/SPR_DS_DownedSleep_v01.png",.755f,.758f,-18,PlayerRole.DeepSeek),
                    Pose("Harness/SPR_HA_IdleFly_v03.png",.662f,.866f,7),
                    Pose("Harness/SPR_HA_LaserFire_v01.png",.713f,.785f,3),
                    Pose("Harness/SPR_HA_DownedSleep_v01.png",.395f,.866f,18),
                    Pose("Harness/Melee/SPR_HA_MeleeIdle_v01.png",.651f,.875f,4),
                    Pose("Harness/Melee/SPR_HA_MeleeDownCommand_v01.png",.771f,.747f,-14),
                    Pose("Harness/Melee/SPR_HA_MeleeUpCommand_v02.png",.641f,.859f,11),
                    Pose("Harness/Melee/SPR_HA_MeleeSweepCommand_v03.png",.668f,.854f,0)
                }; EditorUtility.SetDirty(crown);
            }
            InstallCard();
            foreach(string scenePath in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/Gameplay_Prototype.unity","Assets/Scenes/World01_EarlyInternet.unity"})
            {
                var scene=EditorSceneManager.OpenScene(scenePath);
                var roots=scene.GetRootGameObjects();
                var previews=roots.SelectMany(r=>r.GetComponentsInChildren<Image>(true)).Where(i=>
                    i.sprite!=null && crown.TryGetPose(i.sprite,out _)).Select(i=>InstallImage(i,crown)).ToArray();
                if(scenePath.EndsWith("MainMenu.unity"))
                {
                    var menu=roots.SelectMany(r=>r.GetComponentsInChildren<WhaleMetaMenuController>(true)).Single();
                    var so=new SerializedObject(menu); var products=so.FindProperty("_products");
                    var all=Enumerable.Range(0,products.arraySize).Select(i=>(ShopProductDefinition)products.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
                    all.Remove(product);all.Insert(0,product);products.arraySize=all.Count;
                    for(int i=0;i<all.Count;i++) products.GetArrayElementAtIndex(i).objectReferenceValue=all[i];
                    so.ApplyModifiedPropertiesWithoutUndo(); menu.HeadwearPreviews=previews;
                    Scroll((RectTransform)so.FindProperty("_shopContent").objectReferenceValue);
                    Scroll((RectTransform)so.FindProperty("_inventoryContent").objectReferenceValue);
                }
                else
                {
                    var entry=roots.SelectMany(r=>r.GetComponentsInChildren<GameplayEntryFlow>(true)).Single();
                    var session=roots.SelectMany(r=>r.GetComponentsInChildren<CoopSessionController>(true)).Single();
                    var channel=entry.GetComponent<HeadwearSessionPresenter>();
                    if(channel==null) channel=entry.gameObject.AddComponent<HeadwearSessionPresenter>();
                    channel.Session=session;channel.Catalog=new[]{crown};channel.Previews=previews;
                    var replicas=roots.SelectMany(r=>r.GetComponentsInChildren<NetworkPlayerReplica>(true)).ToArray();
                    channel.DeepSeek=InstallSprite(replicas.Single(r=>r.Role==PlayerRole.DeepSeek).Visual);
                    channel.Harness=InstallSprite(replicas.Single(r=>r.Role==PlayerRole.Harness).Visual);
                    entry.Headwear=channel;
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var net=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>(Root+"Configs/Networking/CFG_Network.asset");
            Networking.NetworkBuildRevision.Apply(net);EditorUtility.SetDirty(net);
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            return "Crown price20, nonrepeatable; card equip buttons; 9 pose anchors; menu/two gameplay scenes; protocol8 saved.";
        }
        static HeadwearPose Pose(string file,float x,float y,float angle,PlayerRole role=PlayerRole.Harness) =>
            new(){Pose=SpriteAt(Root+"Art/Characters/"+file),Anchor=new Vector2(x,y),Angle=angle,WidthRatio=.32f,Role=role};
        static Sprite SpriteAt(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException(path);
        static HeadwearSpriteView InstallSprite(SpriteRenderer subject)
        {
            var view=subject.GetComponent<HeadwearSpriteView>();
            if(view!=null) return view;
            view=subject.gameObject.AddComponent<HeadwearSpriteView>();view.Subject=subject;
            var go=new GameObject("Headwear",typeof(SpriteRenderer));go.transform.SetParent(subject.transform,false);
            view.Ornament=go.GetComponent<SpriteRenderer>();view.Ornament.sharedMaterial=subject.sharedMaterial;view.Ornament.enabled=false;
            return view;
        }
        static HeadwearImageView InstallImage(Image image,HeadwearDefinition crown)
        {
            var view=image.GetComponent<HeadwearImageView>();
            if(view==null)
            {
                view=image.gameObject.AddComponent<HeadwearImageView>();view.Subject=image;
                var go=new GameObject("Headwear",typeof(RectTransform),typeof(Image));go.transform.SetParent(image.transform,false);
                view.Ornament=go.GetComponent<Image>();view.Ornament.raycastTarget=false;view.Ornament.enabled=false;
            }
            view.Catalog=new[]{crown}; return view;
        }
        static void Scroll(RectTransform content)
        {
            if(content.parent.GetComponent<ScrollRect>()!=null) return;
            var go=new GameObject(content.name+"Scroll",typeof(RectTransform),typeof(RectMask2D),typeof(ScrollRect));
            var rect=(RectTransform)go.transform;rect.SetParent(content.parent,false);
            rect.anchoredPosition=content.anchoredPosition;rect.sizeDelta=content.sizeDelta;
            var scroll=go.GetComponent<ScrollRect>();scroll.viewport=rect;scroll.content=content;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            content.SetParent(rect,false);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);
            content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(0,content.sizeDelta.y);
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        }
        static void InstallCard()
        {
            const string path=Root+"Prefabs/UI/Meta/PF_UI_MetaProductCard.prefab";
            var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var card=go.GetComponent<MetaProductCardView>();var so=new SerializedObject(card);
                var buy=(Button)so.FindProperty("_purchase").objectReferenceValue;
                so.FindProperty("_purchaseLabel").objectReferenceValue=buy.GetComponentInChildren<Text>(true);
                foreach(bool ds in new[]{true,false})
                {
                    var field=so.FindProperty(ds?"_equipDeepSeek":"_equipHarness");var button=(Button)field.objectReferenceValue;
                    if(button==null) {button=Object.Instantiate(buy,buy.transform.parent);button.name=ds?"EquipDS":"EquipHS";field.objectReferenceValue=button;}
                    var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(345,ds?30:-30);rect.sizeDelta=new Vector2(170,52);
                    var label=button.GetComponentInChildren<Text>(true);label.text=ds?"DS · 佩戴":"HS · 佩戴";label.fontSize=22;
                    label.rectTransform.sizeDelta=rect.sizeDelta;
                    so.FindProperty(ds?"_equipDeepSeekLabel":"_equipHarnessLabel").objectReferenceValue=label;
                    button.gameObject.SetActive(false);
                }
                so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(go);}
        }
        static void Set(Object target,string name,object value)
        {
            var so=new SerializedObject(target);var p=so.FindProperty(name);
            if(value is string s)p.stringValue=s;else if(value is int i)p.intValue=i;else if(value is bool b)p.boolValue=b;else p.objectReferenceValue=(Object)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
