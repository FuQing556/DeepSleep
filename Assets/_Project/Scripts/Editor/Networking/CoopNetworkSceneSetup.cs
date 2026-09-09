using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DeepSleep.Adapters.Networking;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.UI.Common;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Networking
{
    /// <summary>本轮经授权的一次性场景装配。只在编辑器显式调用，不自动运行。</summary>
    public static class CoopNetworkSceneSetup
    {
        public static string InstallWeapons()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            var session = UnityEngine.Object.FindFirstObjectByType<CoopSessionController>();
            var channel = session.GetComponent<NetworkWeaponChannel>();
            if (channel == null) channel = session.gameObject.AddComponent<NetworkWeaponChannel>();
            channel.Session = session; channel.Catalog = session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek.Sprites;
            channel.Laser = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.HarnessTerminalLaserController>();
            channel.Melee = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleeController>();
            channel.Guard = session.DeepSeek.GetComponent<DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard.DeepSeekRiceGuardController>();
            channel.LaserView = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.Presentation.HarnessTerminalLaserPresenter>();
            channel.MeleeView = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleePresenter2D>();
            var presentationTypes = new HashSet<string> { "HarnessTerminalLaserPresenter", "HarnessMeleePresenter2D",
                "MeleeWaveView2D", "BeamTiledMeshView2D", "DeepSeekRiceGuardOrbitView2D", "DeepSeekRiceGuardCircleView2D",
                "PlayerReviveHealingParticleView2D", "PlayerReviveConvergeRingView2D", "PlayerReviveProtectionView2D" };
            var gate = session.GetComponent<NetworkAuthorityGate>();
            gate.AuthorityOnly = gate.AuthorityOnly.Where(b => !presentationTypes.Contains(b.GetType().Name)).ToArray();
            foreach (var replica in new[] { session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek,
                session.GetComponent<NetworkPlayerSnapshotChannel>().Harness })
            {
                var pose = new SerializedObject(replica.GetComponent<PlayerDownedVisual2D>());
                replica.PoseGhostRenderer = (SpriteRenderer)pose.FindProperty("_ghostRenderer").objectReferenceValue;
                replica.PoseGhostConfig = (PlayerDownedVisualConfig)pose.FindProperty("_config").objectReferenceValue;
                foreach (var shield in UnityEngine.Object.FindObjectsByType<DeepSleep.Runtime.Players.Revive.Presentation.PlayerReviveProtectionView2D>(FindObjectsSortMode.None))
                {
                    var receiver = new SerializedObject(shield).FindProperty("_receiver").objectReferenceValue;
                    if (receiver == replica.LocalHud.DamageReceiver) replica.ProtectionView = shield;
                }
            }
            if (AssetDatabase.LoadMainAssetAtPath("Assets/DefaultNetworkPrefabs.asset") != null)
                AssetDatabase.MoveAsset("Assets/DefaultNetworkPrefabs.asset", "Assets/_Project/Configs/Networking/DefaultNetworkPrefabs.asset");
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene); EditorSceneManager.SaveScene(session.gameObject.scene);
            AssetDatabase.SaveAssets(); return "Weapon presentation channels installed; authority disabled on client";
        }
        public static string InstallWorld()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            var session = UnityEngine.Object.FindFirstObjectByType<CoopSessionController>();
            if (session.GetComponent<NetworkWorldSnapshotChannel>() != null) return "World already installed";
            var channel = session.gameObject.AddComponent<NetworkWorldSnapshotChannel>(); channel.Session = session;
            channel.Catalog = session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek.Sprites;
            var pools = UnityEngine.Object.FindObjectsByType<DeepSleep.Runtime.Combat.Enemies.EnemyActorPool2D>(FindObjectsSortMode.None);
            channel.Windows = pools.Single(p => p.gameObject.name == "EnemyRuntime_404Window");
            channel.Snakes = pools.Single(p => p != channel.Windows);
            channel.Rice = session.DeepSeek.GetComponent<DeepSleep.Runtime.Combat.Projectiles.RiceProjectilePool>();
            channel.EnemyBullets = UnityEngine.Object.FindFirstObjectByType<DeepSleep.Runtime.Combat.Projectiles.EnemyProjectilePool2D>();
            channel.MaximumViews = 512;
            channel.ViewRoot = new GameObject("NetworkEntityViews").transform;
            var template = new GameObject("PF_NetworkEntityView");
            var view = template.AddComponent<NetworkEntityView>(); view.Layers = new SpriteRenderer[8];
            for (int i = 0; i < view.Layers.Length; i++)
            {
                var child = new GameObject("VisualLayer_" + i); child.transform.SetParent(template.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>(); renderer.enabled = false;
                renderer.sharedMaterial = session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek.Visual.sharedMaterial;
                view.Layers[i] = renderer;
            }
            EnsureFolder("Assets/_Project/Prefabs/Networking");
            channel.ViewPrefab = PrefabUtility.SaveAsPrefabAsset(template,
                "Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab").GetComponent<NetworkEntityView>();
            UnityEngine.Object.DestroyImmediate(template);
            ushort id = 100;
            // ID首次装配后保存在场景；不是运行时数组索引。内容版本同时锁定此映射。
            foreach (var pool in UnityEngine.Object.FindObjectsByType<DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D>(FindObjectsSortMode.None))
            {
                var effect = pool.gameObject.AddComponent<NetworkEffectEventChannel>();
                effect.Session = session; effect.Pool = pool; effect.EffectId = id++;
            }
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene); AssetDatabase.SaveAssets();
            return "World replicas + effect event channels installed";
        }

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before installation");
            if (UnityEngine.Object.FindFirstObjectByType<CoopSessionController>() != null)
                return "Already installed; existing scene left unchanged";
            var actors = UnityEngine.Object.FindObjectsByType<PlayerActor>(FindObjectsSortMode.None);
            var assignment = UnityEngine.Object.FindFirstObjectByType<PlayerControlAssignment>();
            var selection = UnityEngine.Object.FindFirstObjectByType<OpeningCharacterSelectionController>();
            var router = UnityEngine.Object.FindFirstObjectByType<CompanionCommandRouter>();
            if (actors.Length != 2 || assignment == null || selection == null || router == null)
                throw new InvalidOperationException("Expected existing two-player gameplay scene");
            var root = new GameObject("NetworkSession"); Undo.RegisterCreatedObjectUndo(root, "Install cooperative networking");
            var manager = root.AddComponent<NetworkManager>(); var transport = root.AddComponent<UnityTransport>();
            var adapter = root.AddComponent<NgoTransportAdapter>(); var session = root.AddComponent<CoopSessionController>();
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            config.Port = 7777; config.ProtocolVersion = 1; config.ClientVersion = "0.1.0";
            config.ContentVersion = "20260909-network-1"; config.SnapshotRate = 20;
            config.InputTimeout = 0.35f; config.ConnectionTimeout = 12;
            config.MaximumQueuedCommands = 32; config.MaximumMessageBytes = 16384; config.RemoteInterpolationSpeed = 20;
            EnsureFolder("Assets/_Project/Configs/Networking");
            AssetDatabase.CreateAsset(config, "Assets/_Project/Configs/Networking/CFG_Network.asset");
            var catalog = CreateCatalog();
            adapter.Manager = manager; adapter.Transport = transport; adapter.Config = config;
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            session.TransportComponent = adapter; session.Config = config; session.Assignment = assignment;
            session.Selection = selection;
            session.LocalInput = (MonoBehaviour)new SerializedObject(assignment)
                .FindProperty("_localCommandSourceComponent").objectReferenceValue;
            session.DeepSeek = actors.Single(a => a.Definition.Role == PlayerRole.DeepSeek);
            session.Harness = actors.Single(a => a.Definition.Role == PlayerRole.Harness);
            session.DeepSeekAi = router.DeepSeek; session.HarnessAi = router.Harness;
            var channel = root.AddComponent<NetworkPlayerSnapshotChannel>(); channel.Session = session;
            channel.DeepSeek = AddReplica(session.DeepSeek, session, catalog);
            channel.Harness = AddReplica(session.Harness, session, catalog);
            var gate = root.AddComponent<NetworkAuthorityGate>(); gate.Session = session;
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            gate.AuthorityOnly = all.Where(IsAuthorityOnly).Cast<Behaviour>().ToArray();
            gate.AuthorityBodies = actors.Select(a => a.GetComponent<Rigidbody2D>()).ToArray();
            CreateUi(session);
            EditorUtility.SetDirty(root); EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene); AssetDatabase.SaveAssets();
            return "Installed session + two replicas + UI; authority-only components: " + gate.AuthorityOnly.Length;
        }

        private static bool IsAuthorityOnly(MonoBehaviour b)
        {
            string ns = b.GetType().Namespace ?? "";
            if (!ns.StartsWith("DeepSleep.Runtime")) return false;
            if (ns.Contains("Networking") || ns.Contains(".UI.") || ns.Contains(".Input.") ||
                ns.Contains(".World.") || ns.Contains(".Orientation") || ns.Contains("Presentation.Effects")) return false;
            if (b is PlayerActor || b is PlayerControlAssignment) return false;
            // 镜像端暂由显式快照视图驱动；其余本地玩法及姿态写入器均不执行。
            return true;
        }

        private static NetworkPlayerReplica AddReplica(PlayerActor actor, CoopSessionController session, NetworkSpriteCatalog catalog)
        {
            var replica = actor.gameObject.AddComponent<NetworkPlayerReplica>(); replica.Session = session;
            replica.Role = actor.Definition.Role; replica.Body = actor.GetComponent<Rigidbody2D>();
            replica.Facing = actor.GetComponent<PlayerFacingController2D>();
            replica.Visual = (SpriteRenderer)new SerializedObject(actor.GetComponent<PlayerDownedVisual2D>())
                .FindProperty("_characterRenderer").objectReferenceValue;
            replica.VisualRoot = replica.Visual.transform.parent; replica.Sprites = catalog;
            replica.LocalHud = UnityEngine.Object.FindObjectsByType<PlayerCombatHudSource>(FindObjectsSortMode.None)
                .Single(h => h.Role == replica.Role);
            var view = replica.LocalHud.GetComponent<PlayerCombatHudView>(); view.SourceComponent = replica;
            EditorUtility.SetDirty(view); return replica;
        }

        private static NetworkSpriteCatalog CreateCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<NetworkSpriteCatalog>();
            var entries = new List<NetworkSpriteCatalog.Entry>();
            using var hash = SHA256.Create();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string assetGuid, out long localId);
                    uint id = BitConverter.ToUInt32(hash.ComputeHash(Encoding.UTF8.GetBytes(assetGuid + ":" + localId)), 0);
                    entries.Add(new NetworkSpriteCatalog.Entry { Id = id, Sprite = sprite });
                }
            }
            catalog.Entries = entries.GroupBy(e => e.Sprite).Select(g => g.First()).ToArray();
            if (!catalog.Initialize()) throw new InvalidOperationException("Sprite catalog validation failed");
            AssetDatabase.CreateAsset(catalog, "Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            return catalog;
        }

        private static void CreateUi(CoopSessionController session)
        {
            var root = Rect("UI_NetworkSession", null); var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var safe = Rect("SafeArea", root); Stretch(safe);
            var fitter = safe.gameObject.AddComponent<SafeAreaRectFitter>();
            var so = new SerializedObject(fitter); so.FindProperty("_target").objectReferenceValue = safe; so.ApplyModifiedPropertiesWithoutUndo();
            var menu = root.gameObject.AddComponent<CoopSessionMenu>(); menu.Session = session;
            menu.TogglePanel = Button("Network", safe, new Vector2(-140, -42), new Vector2(240, 65), new Vector2(1, 1));
            var panel = Rect("Panel", safe); panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(700, 540); panel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.12f, 0.97f);
            menu.Panel = panel.gameObject;
            menu.StatusLabel = Label("Offline / LAN direct connection", panel, new Vector2(0, 205), new Vector2(660, 80));
            menu.HostDs = Button("Host as DS", panel, new Vector2(-165, 105), new Vector2(290, 65));
            menu.HostHs = Button("Host as HS", panel, new Vector2(165, 105), new Vector2(290, 65));
            var field = Rect("Host IPv4", panel); field.sizeDelta = new Vector2(390, 65); field.anchoredPosition = new Vector2(-125, 20);
            field.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.19f, 0.25f);
            var input = field.gameObject.AddComponent<InputField>();
            input.textComponent = Label("127.0.0.1", field, Vector2.zero, new Vector2(365, 60)); input.text = "127.0.0.1"; menu.Address = input;
            menu.JoinButton = Button("Join", panel, new Vector2(215, 20), new Vector2(180, 65));
            menu.ReadyButton = Button("Ready", panel, new Vector2(-165, -65), new Vector2(290, 65));
            menu.AiButton = Button("Let AI Play", panel, new Vector2(165, -65), new Vector2(290, 65));
            menu.ReadyLabel = menu.ReadyButton.GetComponentInChildren<Text>(); menu.AiLabel = menu.AiButton.GetComponentInChildren<Text>();
            menu.LeaveButton = Button("Leave / Return to Menu", panel, new Vector2(0, -155), new Vector2(500, 65));
            Label("LAN only · cloud relay not connected", panel, new Vector2(0, -225), new Vector2(650, 45));
            panel.gameObject.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Text Label(string text, Transform parent, Vector2 position, Vector2 size)
        {
            var r = Rect("Label", parent); r.anchoredPosition = position; r.sizeDelta = size;
            var t = r.gameObject.AddComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = 26; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false; return t;
        }
        private static Button Button(string label, Transform parent, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            var r = Rect(label, parent); r.anchoredPosition = position; r.sizeDelta = size;
            if (anchor.HasValue) r.anchorMin = r.anchorMax = anchor.Value;
            var image = r.gameObject.AddComponent<Image>(); image.color = new Color(0.13f, 0.25f, 0.42f);
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label(label, r, Vector2.zero, size); return button;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
