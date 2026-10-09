using System;
using System.Linq;
using DeepSleep.Runtime.Presentation.Accessories;
using DeepSleep.Runtime.Progression.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户明确授权的背饰装配；只新增背饰引用，不重写皇冠动作参数。</summary>
    public static class BackwearInstaller
    {
        public const string DefinitionPath = "Assets/_Project/Configs/Progression/Meta/CFG_Accessory_LittleWings.asset";
        public const string ProductPath = "Assets/_Project/Configs/Progression/Meta/Products/CFG_META_Product_little_wings.asset";
        public const string ArtPath = "Assets/_Project/Art/Accessories/SPR_ACC_LittleWings_v01.png";
        public static string Install()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("请先退出播放并保存当前场景。");
            AssetDatabase.ImportAsset(ArtPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048; importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            var crown = AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(HeadwearInstaller.DefinitionPath);
            var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(ProductPath);
            if (product == null)
            {
                product = ScriptableObject.CreateInstance<ShopProductDefinition>();
                AssetDatabase.CreateAsset(product, ProductPath);
                var so = new SerializedObject(product);
                so.FindProperty("_productId").stringValue = "little_wings";
                so.FindProperty("_displayName").stringValue = "小翅膀";
                so.FindProperty("_description").stringValue = "永久背饰 · 无属性 · 可与皇冠同时佩戴";
                so.FindProperty("_price").intValue = 10;
                so.FindProperty("_repeatable").boolValue = false;
                so.FindProperty("_isBackwear").boolValue = true;
                so.FindProperty("_icon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var wings = AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(DefinitionPath);
            if (wings == null)
            {
                wings = ScriptableObject.CreateInstance<HeadwearDefinition>();
                wings.Product = product; wings.NetworkId = 2; wings.Sprite = sprite;
                wings.Layer = AccessoryLayer.Back; wings.BaseAnchor = new Vector2(.5f,.4f);
                // 起始背部锚点与头顶配置独立；用户可以用可视化编辑器逐动作调整。
                var anchors = new[] { new Vector2(.53f,.49f), new Vector2(.51f,.46f), new Vector2(.57f,.48f),
                    new Vector2(.56f,.48f), new Vector2(.52f,.48f), new Vector2(.53f,.50f),
                    new Vector2(.54f,.44f), new Vector2(.51f,.48f), new Vector2(.53f,.48f) };
                wings.Poses = crown.Poses.Select((p,i) => new HeadwearPose { Pose=p.Pose, Role=p.Role,
                    Anchor=anchors[i], Angle=0, WidthRatio=.72f }).ToArray();
                AssetDatabase.CreateAsset(wings, DefinitionPath);
            }
            FixCard();
            foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var roots = scene.GetRootGameObjects();
                var frontImages = roots.SelectMany(r => r.GetComponentsInChildren<HeadwearImageView>(true))
                    .Where(v => v.Slot == AccessorySlot.Front).ToArray();
                var allImages = frontImages.ToList();
                foreach (var front in frontImages)
                {
                    front.Catalog = new[] { crown, wings };
                    var back = front.GetComponents<HeadwearImageView>().FirstOrDefault(v => v.Slot == AccessorySlot.Back);
                    if (back == null)
                    {
                        back = front.gameObject.AddComponent<HeadwearImageView>(); back.Slot = AccessorySlot.Back;
                        var go = new GameObject("Backwear", typeof(RectTransform), typeof(Image));
                        go.transform.SetParent(front.Subject.transform, false);
                        back.Ornament = go.GetComponent<Image>(); back.Ornament.raycastTarget = false; back.Ornament.enabled = false;
                    }
                    back.Subject = front.Subject; back.Catalog = new[] { crown, wings }; allImages.Add(back);
                }
                if (path.EndsWith("MainMenu.unity"))
                {
                    var menu = roots.SelectMany(r => r.GetComponentsInChildren<WhaleMetaMenuController>(true)).Single();
                    menu.HeadwearPreviews = allImages.ToArray();
                    var so = new SerializedObject(menu); var list = so.FindProperty("_products");
                    var products = Enumerable.Range(0,list.arraySize).Select(i => (ShopProductDefinition)list.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
                    products.Remove(product); products.Insert(Math.Min(1,products.Count),product); list.arraySize=products.Count;
                    for (int i=0;i<products.Count;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=products[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var channel = roots.SelectMany(r => r.GetComponentsInChildren<HeadwearSessionPresenter>(true)).Single();
                    channel.Catalog = new[] { crown, wings }; channel.Previews = allImages.ToArray();
                    channel.DeepSeekBack = AddBack(channel.DeepSeek, channel.DeepSeekBack);
                    channel.HarnessBack = AddBack(channel.Harness, channel.HarnessBack);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            var net = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            Networking.NetworkBuildRevision.Apply(net); EditorUtility.SetDirty(net);
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            Accessories.AccessoryEditorWindow.Open(wings);
            return "Installed little_wings10, two independent slots, 9 wing poses, three scenes, protocol9. Crown untouched.";
        }

        private static HeadwearSpriteView AddBack(HeadwearSpriteView front, HeadwearSpriteView back)
        {
            if (back != null) return back;
            back = front.gameObject.AddComponent<HeadwearSpriteView>(); back.Subject = front.Subject;
            var go = new GameObject("Backwear", typeof(SpriteRenderer)); go.transform.SetParent(front.Subject.transform,false);
            back.Ornament = go.GetComponent<SpriteRenderer>(); back.Ornament.sharedMaterial = front.Ornament.sharedMaterial;
            back.Ornament.enabled = false; return back;
        }

        private static void FixCard()
        {
            const string path="Assets/_Project/Prefabs/UI/Meta/PF_UI_MetaProductCard.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so=new SerializedObject(root.GetComponent<MetaProductCardView>());
                var icon=(Image)so.FindProperty("_icon").objectReferenceValue;
                icon.rectTransform.anchoredPosition=new Vector2(-385,0); icon.rectTransform.sizeDelta=new Vector2(110,110); icon.preserveAspect=true;
                foreach (string field in new[]{"_name","_description"})
                {
                    var text=(Text)so.FindProperty(field).objectReferenceValue;
                    text.rectTransform.anchoredPosition=new Vector2(-155,text.rectTransform.anchoredPosition.y);
                    text.rectTransform.sizeDelta=new Vector2(300,text.rectTransform.sizeDelta.y);
                    text.horizontalOverflow=HorizontalWrapMode.Wrap;
                    if(field=="_description") {text.fontSize=18; text.resizeTextForBestFit=true; text.resizeTextMinSize=16; text.resizeTextMaxSize=18;}
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
