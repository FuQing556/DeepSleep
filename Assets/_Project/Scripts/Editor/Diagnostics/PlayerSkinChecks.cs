using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Presentation.Accessories;
using DeepSleep.Runtime.Presentation.Skins;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>隔离对象/临时存档验证，不写用户档案，不启动AI或战斗。</summary>
    public static class PlayerSkinChecks
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        public static string Run()
        {
            int assertions = 0;
            void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception("Skin check: " + message); }
            var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(PlayerSkinInstaller.ProductPath);
            var skin = product.Skin;
            Check(product.TryValidate(out _) && product.Price == 10 && skin.Role == PlayerRole.DeepSeek, "DS product10 valid");
            Check(skin.AliveVariants.Length == 2 && skin.DownedVariants.Length == 2 && skin.Preview == skin.AliveVariants[0], "Two equally weighted variants and hidden preview");
            var root = new GameObject("SkinChecks"); root.SetActive(false);
            string directory = Path.GetFullPath(Path.Combine("Temp", "PlayerSkinChecks", Guid.NewGuid().ToString("N")));
            try
            {
                var profile = root.AddComponent<LocalPlayerProfileStore>(); Set(profile, "TestSaveDirectory", directory);
                Type dataType = typeof(LocalPlayerProfileStore).Assembly.GetType("DeepSleep.Runtime.Progression.Meta.LocalPlayerProfileData");
                var data = JsonUtility.FromJson("{\"version\":4,\"whaleVoucherBalance\":30,\"ownedProducts\":[{\"productId\":\"little_crown\",\"count\":1},{\"productId\":\"little_wings\",\"count\":1}],\"deepSeekHeadwear\":\"little_crown\",\"deepSeekBackwear\":\"little_wings\"}", dataType);
                typeof(LocalPlayerProfileStore).GetMethod("TryNormalize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { data });
                Set(profile, "_data", data);
                Check(profile.GetSkin(PlayerRole.DeepSeek) == "", "v4 migration preserves default costume");
                Check(!profile.TrySetSkin(PlayerRole.DeepSeek, product, out _), "Cannot wear unowned skin");
                Check(profile.TryPurchase(product, out _) && profile.WhaleVoucherBalance == 20, "Purchase deducts10");
                Check(!profile.TryPurchase(product, out _) && profile.WhaleVoucherBalance == 20, "Permanent nonrepeatable purchase");
                Check(!profile.TrySetSkin(PlayerRole.Harness, product, out _), "HS cannot wear DS costume");
                Check(profile.TrySetSkin(PlayerRole.DeepSeek, product, out _), "Wear skin");
                Check(profile.GetHeadwear(PlayerRole.DeepSeek) == "little_crown" && profile.GetAccessory(PlayerRole.DeepSeek, AccessorySlot.Back) == "little_wings", "Stack without modifying accessories");
                Call(profile, "Awake");
                Check(profile.GetSkin(PlayerRole.DeepSeek) == product.ProductId && profile.WhaleVoucherBalance == 20, "Persistence reload");

                var subject = new GameObject("Subject", typeof(SpriteRenderer)); subject.transform.SetParent(root.transform);
                var renderer = subject.GetComponent<SpriteRenderer>(); renderer.sprite = skin.DefaultAlive;
                var ghost = new GameObject("Ghost", typeof(SpriteRenderer)); ghost.transform.SetParent(root.transform);
                var visual = root.AddComponent<PlayerDownedVisual2D>();
                Set(visual, "_characterRenderer", renderer); Set(visual, "_ghostRenderer", ghost.GetComponent<SpriteRenderer>());
                Set(visual, "_downedSprite", skin.DefaultDowned);
                Set(visual, "_config", AssetDatabase.LoadAssetAtPath<PlayerDownedVisualConfig>("Assets/_Project/Configs/Presentation/CFG_PlayerDownedVisual_Default.asset"));
                Call(visual, "Awake"); visual.SetSkin(skin);
                var theme = root.AddComponent<DeepSleep.Runtime.UI.Common.UiThemeView>();
                theme.PlayerSkins = new[] { product };
                Check(theme.ResolvePortrait(PlayerRole.DeepSeek, skin.DefaultAlive, profile) == skin.Preview, "Theme portrait uses equipped skin even when edit-time image is empty");
                Check(theme.ResolvePortrait(PlayerRole.Harness, skin.DefaultAlive, profile) == skin.DefaultAlive, "DS costume does not replace HS theme portrait");
                Check(profile.TrySetSkin(PlayerRole.DeepSeek, null, out _) && theme.ResolvePortrait(PlayerRole.DeepSeek, skin.DefaultAlive, profile) == skin.DefaultAlive, "Theme portrait restores original on unequip");
                profile.TrySetSkin(PlayerRole.DeepSeek, product, out _);
                var randomState = UnityEngine.Random.state;
                var seen = new System.Collections.Generic.HashSet<Sprite>();
                for (int i = 0; i < 512; i++) { visual.RerollSkinPose(); seen.Add(renderer.sprite); }
                Check(seen.SetEquals(skin.AliveVariants), "Both alive variants selected");
                Check(UnityEngine.Random.state.Equals(randomState), "Does not consume combat RNG");
                uint before = visual.SkinRollCount; visual.ShowDownedPose();
                Check(visual.SkinRollCount == before + 1 && skin.DownedVariants.Contains(renderer.sprite), "Downed transition rolls once");
                seen.Clear();
                for (int i = 0; i < 512; i++) { visual.RerollSkinPose(); seen.Add(renderer.sprite); }
                Check(seen.SetEquals(skin.DownedVariants), "Both downed variants selected");
                before = visual.SkinRollCount; visual.ShowAlivePose();
                Check(visual.SkinRollCount == before + 1 && skin.AliveVariants.Contains(renderer.sprite), "Revive rolls once");
                var stable = renderer.sprite;
                for (int i = 0; i < 10; i++) Call(visual, "LateUpdate");
                Check(renderer.sprite == stable, "No per-frame reroll");
                visual.SetSkin(null); Check(renderer.sprite == skin.DefaultAlive, "Restore default costume"); visual.SetSkin(skin);
                // 场景Awake顺序不固定：入口可能先绑定服装，随后角色Awake才初始化。
                visual.SetSkin(null); Set(visual, "_isInitialized", false);
                before = visual.SkinRollCount; visual.SetSkin(skin); Call(visual, "Awake");
                Check(visual.SkinRollCount == before + 1 && skin.AliveVariants.Contains(renderer.sprite), "Binding before player Awake still rolls first visible pose");
                visual.SetSkin(null); Check(renderer.sprite == skin.DefaultAlive, "Early skin binding preserves original costume"); visual.SetSkin(skin);

                var imageObject = new GameObject("Preview", typeof(RectTransform), typeof(Image)); imageObject.transform.SetParent(root.transform);
                var imageView = imageObject.AddComponent<PlayerSkinImageView>(); imageView.Subject = imageObject.GetComponent<Image>();
                imageView.Subject.sprite = skin.DefaultAlive; imageView.Catalog = new[] { product }; imageView.Bind(profile); Call(imageView, "LateUpdate");
                Check(imageView.Subject.sprite == skin.Preview, "Preview always hidden-badge version");
                var palette = ScriptableObject.CreateInstance<DeepSleep.Runtime.UI.Common.UiThemePalette>();
                try
                {
                    palette.Portrait = skin.DefaultAlive; theme.DeepSeek = theme.Harness = palette;
                    theme.Portraits = new[] { new DeepSleep.Runtime.UI.Common.UiThemeView.PortraitBinding { Target = imageView.Subject } };
                    Set(theme, "_profile", profile); imageView.Subject.sprite = null;
                    theme.Apply(PlayerRole.DeepSeek);
                    Check(imageView.Subject.sprite == skin.Preview, "Real theme Apply fills blank homepage portrait with skin");
                    profile.TrySetSkin(PlayerRole.DeepSeek, null, out _); theme.Apply(PlayerRole.DeepSeek);
                    Check(imageView.Subject.sprite == skin.DefaultAlive, "Real theme Apply restores homepage original");
                    profile.TrySetSkin(PlayerRole.DeepSeek, product, out _); theme.Apply(PlayerRole.DeepSeek);
                }
                finally { Object.DestroyImmediate(palette); }

                var session = root.AddComponent<CoopSessionController>(); var transport = new Capture(); Set(session, "_transport", transport);
                session.Config = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
                Set(session, "<Phase>k__BackingField", SessionPhase.Lobby); Set(session, "_hasPeer", true);
                var presenter = root.AddComponent<PlayerSkinSessionPresenter>(); presenter.Session = session;
                presenter.DeepSeekVisual = visual; presenter.Catalog = new[] { product }; presenter.Previews = new[] { imageView };
                presenter.Bind(profile);
                Check(transport.Last?.Length == 3 && NetworkMessageCatalog.TryValidatePacket(transport.Last, NetworkMessageCatalog.Direction.AuthorityToPeer, out _), "Registered compact costume packet");
                before = visual.SkinRollCount;
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing); Call(presenter, "Refresh");
                Check(visual.SkinRollCount == before + 1 && skin.AliveVariants.Contains(renderer.sprite), "First entry from lobby rolls alive rather than fixing preview");
                before = visual.SkinRollCount; Call(presenter, "Refresh"); Check(visual.SkinRollCount == before, "Control/profile refresh does not reroll");
                Call(presenter, "NodeChanged", RestNodeState.Revealing); Check(visual.SkinRollCount == before + 1, "Node entry rolls once");
                Call(presenter, "NodeChanged", RestNodeState.Open); Check(visual.SkinRollCount == before + 1, "Opening same node does not reroll");
                Call(presenter, "NodeChanged", RestNodeState.Departing); Check(visual.SkinRollCount == before + 2, "Departure rolls once");
                Call(presenter, "NodeChanged", RestNodeState.Combat); Call(presenter, "NodeChanged", RestNodeState.Open);
                Check(visual.SkinRollCount == before + 3, "Direct preparation entry rolls");
                Read(presenter, "ReadPeer", NetworkMessageCatalog.Peer.Skin, 0); Check(presenter.CurrentSkinId == skin.NetworkId, "Guest cannot replace host DS skin");
                Set(session, "<HostRole>k__BackingField", PlayerRole.Harness);
                Read(presenter, "ReadPeer", NetworkMessageCatalog.Peer.Skin, skin.NetworkId); Check(presenter.CurrentSkinId == skin.NetworkId, "Guest DS chooses own skin");
                Read(presenter, "ReadPeer", NetworkMessageCatalog.Peer.Skin, ushort.MaxValue); Check(presenter.CurrentSkinId == skin.NetworkId, "Unknown skin ignored");
                transport.IsServer = false; before = visual.SkinRollCount;
                Read(presenter, "ReadAuthority", NetworkMessageCatalog.Authority.Skin, 0);
                Call(presenter, "NodeChanged", RestNodeState.Departing);
                Check(presenter.CurrentSkinId == 0 && visual.SkinRollCount == before, "Guest receives choice without drawing random/body pose");
                var sprites = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
                Check(sprites.Initialize(), "Network sprite catalog valid");
                foreach (var pose in skin.AliveVariants.Concat(skin.DownedVariants))
                {
                    uint id = sprites.GetId(pose);
                    Check(id != 0 && sprites.TryResolve(id, out var resolved) && resolved == pose, "Existing snapshot resolves skin sprite");
                    foreach (string path in new[] { HeadwearInstaller.DefinitionPath, BackwearInstaller.DefinitionPath })
                        Check(AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(path).TryGetPose(pose, out _), "Accessory supports skin pose");
                }
                return "SKIN PASS " + assertions + " checks: isolated v4 migration/purchase/save/equip, random event edges, fixed preview, host-role ownership and guest no-reroll, four sprite IDs/accessories. Temp save " + directory;
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        static void Read(object target, string method, byte kind, ushort id)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); writer.Write(id); writer.Flush(); stream.Position = 0;
            using var reader = new BinaryReader(stream); Call(target, method, kind, reader);
        }
        sealed class Capture : ITransportAdapter
        {
            public byte[] Last; public bool IsServer { get; set; } = true; public bool IsConnected => true; public string DisconnectReason => "";
            public event Action<ulong> Connected { add {} remove {} } public event Action<ulong> Disconnected { add {} remove {} }
            public event Action<ulong, byte[]> Received { add {} remove {} }
            public bool StartHost() => true; public bool StartClient(string address) => true; public void Stop() {}
            public void Send(ulong peer, byte[] payload, bool reliable) => Last = payload;
        }
    }
}
