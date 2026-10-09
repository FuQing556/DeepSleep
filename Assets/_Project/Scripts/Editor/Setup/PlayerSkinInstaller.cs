using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Presentation.Accessories;
using DeepSleep.Runtime.Presentation.Skins;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式装配皮肤，不重建既有商店/UI/角色或覆盖用户饰品参数。</summary>
    public static class PlayerSkinInstaller
    {
        public const string ArtRoot = "Assets/_Project/Art/Characters/DeepSeek/Skins/BorrowedBadge/";
        public const string DefinitionPath = "Assets/_Project/Configs/Progression/Meta/CFG_Skin_DS_BorrowedBadge.asset";
        public const string ProductPath = "Assets/_Project/Configs/Progression/Meta/Products/CFG_META_Product_ds_borrowed_badge.asset";
        public static string Install()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("请先退出播放并保存当前场景。");
            var sprites = new Sprite[4];
            string[] files = { "SPR_DS_BorrowedBadge_Idle_Hidden.png", "SPR_DS_BorrowedBadge_Idle_Visible.png",
                "SPR_DS_BorrowedBadge_Downed_Hidden.png", "SPR_DS_BorrowedBadge_Downed_Visible.png" };
            for (int i = 0; i < files.Length; i++)
            {
                string path = ArtRoot + files[i];
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 512; importer.spritePivot = new Vector2(.5f,.5f);
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.maxTextureSize = 2048; importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport(); sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var skin = AssetDatabase.LoadAssetAtPath<PlayerSkinDefinition>(DefinitionPath);
            if (skin == null) { skin = ScriptableObject.CreateInstance<PlayerSkinDefinition>(); AssetDatabase.CreateAsset(skin, DefinitionPath); }
            skin.NetworkId = 1; skin.Role = PlayerRole.DeepSeek;
            skin.DefaultAlive = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png");
            skin.DefaultDowned = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/DeepSeek/SPR_DS_DownedSleep_v01.png");
            skin.Preview = sprites[0]; skin.AliveVariants = new[] { sprites[0], sprites[1] };
            skin.DownedVariants = new[] { sprites[2], sprites[3] };
            if (!skin.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(skin);
            var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(ProductPath);
            if (product == null) { product = ScriptableObject.CreateInstance<ShopProductDefinition>(); AssetDatabase.CreateAsset(product, ProductPath); }
            var productData = new SerializedObject(product);
            productData.FindProperty("_productId").stringValue = "ds_borrowed_badge";
            productData.FindProperty("_displayName").stringValue = "借来的工牌";
            productData.FindProperty("_description").stringValue = "DS 专属服装 · 永久持有 · 无属性加成\n仅替换常态/倒地外观，工牌随机显隐，可叠加皇冠与翅膀";
            productData.FindProperty("_price").intValue = 10;
            productData.FindProperty("_repeatable").boolValue = false;
            productData.FindProperty("_icon").objectReferenceValue = skin.Preview;
            productData.FindProperty("_skin").objectReferenceValue = skin;
            productData.ApplyModifiedPropertiesWithoutUndo();
            if (!product.TryValidate(out reason)) throw new InvalidOperationException(reason);

            // 按既有编辑器ID算法追加四张姿态，不改任何旧ID或条目。
            var networkSprites = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            var entries = networkSprites.Entries.ToList();
            using (var hash = SHA256.Create()) foreach (var sprite in sprites)
            {
                if (entries.Any(e => e.Sprite == sprite)) continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId);
                uint id = BitConverter.ToUInt32(hash.ComputeHash(Encoding.UTF8.GetBytes(guid + ":" + localId)), 0);
                if (id == 0 || entries.Any(e => e.Id == id)) throw new InvalidOperationException("皮肤姿态网络ID冲突");
                entries.Add(new NetworkSpriteCatalog.Entry { Id = id, Sprite = sprite });
            }
            networkSprites.Entries = entries.ToArray(); EditorUtility.SetDirty(networkSprites);
            foreach (string path in new[] { HeadwearInstaller.DefinitionPath, BackwearInstaller.DefinitionPath })
            {
                var accessory = AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(path);
                var poses = accessory.Poses.ToList();
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (poses.Any(p => p.Pose == sprites[i])) continue;
                    // 复制对应原姿态的用户已保存参数，旧九张参数不动；新皮肤可独立再调。
                    Sprite original = i < 2 ? skin.DefaultAlive : skin.DefaultDowned;
                    var pose = poses.Single(p => p.Pose == original); pose.Pose = sprites[i]; poses.Add(pose);
                }
                accessory.Poses = poses.ToArray(); EditorUtility.SetDirty(accessory);
            }
            foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var roots = scene.GetRootGameObjects();
                // 主页主题角色图编辑态为空，运行时才填入；不能用现有sprite筛选漏掉它。
                foreach (var theme in roots.SelectMany(r => r.GetComponentsInChildren<DeepSleep.Runtime.UI.Common.UiThemeView>(true)))
                    if (theme.Portraits.Length > 0) theme.PlayerSkins = new[] { product };
                var previews = new List<PlayerSkinImageView>();
                foreach (var image in roots.SelectMany(r => r.GetComponentsInChildren<Image>(true)))
                {
                    if (!skin.Contains(image.sprite)) continue;
                    var view = image.GetComponent<PlayerSkinImageView>();
                    if (view == null) view = image.gameObject.AddComponent<PlayerSkinImageView>();
                    view.Subject = image; view.Catalog = new[] { product }; previews.Add(view);
                }
                if (path.EndsWith("MainMenu.unity"))
                {
                    var menu = roots.SelectMany(r => r.GetComponentsInChildren<WhaleMetaMenuController>(true)).Single();
                    var so = new SerializedObject(menu); var products = so.FindProperty("_products");
                    var all = Enumerable.Range(0, products.arraySize).Select(i => (ShopProductDefinition)products.GetArrayElementAtIndex(i).objectReferenceValue)
                        .Where(p => p.ProductId != "merit_small" && p.ProductId != "merit_medium" && p.ProductId != "merit_large" && p != product).ToList();
                    all.Insert(0, product); products.arraySize = all.Count;
                    for (int i = 0; i < all.Count; i++) products.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                    so.ApplyModifiedPropertiesWithoutUndo(); menu.SkinPreviews = previews.ToArray();
                }
                else
                {
                    var entry = roots.SelectMany(r => r.GetComponentsInChildren<GameplayEntryFlow>(true)).Single();
                    var presenter = entry.GetComponent<PlayerSkinSessionPresenter>();
                    if (presenter == null) presenter = entry.gameObject.AddComponent<PlayerSkinSessionPresenter>();
                    presenter.Session = entry.Headwear.Session;
                    presenter.Node = roots.SelectMany(r => r.GetComponentsInChildren<RestNodePrototypeController2D>(true)).Single();
                    presenter.DeepSeekVisual = presenter.Session.DeepSeek.GetComponent<PlayerDownedVisual2D>();
                    presenter.Catalog = new[] { product }; presenter.Previews = previews.ToArray(); entry.Skins = presenter;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            var net = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            Networking.NetworkBuildRevision.Apply(net); EditorUtility.SetDirty(net);
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            return "DS skin10 / 4 variants / fixed hidden preview / 3 scenes / appended accessory poses and sprite IDs / protocol10 saved";
        }
    }
}
