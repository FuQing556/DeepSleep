using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DeepSleep.Adapters.Networking;
using DeepSleep.Editor.Setup;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Networking
{
    /// <summary>Legacy 旧场景初始化，不是新关卡生成器。已登记关卡只转调 LevelSceneInstaller，不重建现役资产。</summary>
    public static class CoopNetworkSceneSetup
    {
        public static string InstallWeapons()
        {
            var scene = LegacyScene();
            if (LevelSceneInstaller.TryApplyRegisteredScene(scene, out string report)) return report;
            var session = All<CoopSessionController>(scene).Single();
            var channel = session.GetComponent<NetworkWeaponChannel>();
            if (channel == null) channel = session.gameObject.AddComponent<NetworkWeaponChannel>();
            channel.Session = session; channel.Catalog = session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek.Sprites;
            channel.Laser = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.HarnessTerminalLaserController>();
            channel.Melee = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleeController>();
            channel.Guard = session.DeepSeek.GetComponent<DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard.DeepSeekRiceGuardController>();
            channel.LaserView = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.Presentation.HarnessTerminalLaserPresenter>();
            channel.MeleeView = session.Harness.GetComponent<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleePresenter2D>();
            var gate = session.GetComponent<NetworkAuthorityGate>();
            NetworkAuthorityRules.Apply(gate);
            foreach (var replica in new[] { session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek,
                session.GetComponent<NetworkPlayerSnapshotChannel>().Harness })
            {
                var pose = new SerializedObject(replica.GetComponent<PlayerDownedVisual2D>());
                replica.PoseGhostRenderer = (SpriteRenderer)pose.FindProperty("_ghostRenderer").objectReferenceValue;
                replica.PoseGhostConfig = (PlayerDownedVisualConfig)pose.FindProperty("_config").objectReferenceValue;
                foreach (var shield in All<DeepSleep.Runtime.Players.Revive.Presentation.PlayerReviveProtectionView2D>(scene))
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
            var scene = LegacyScene();
            if (LevelSceneInstaller.TryApplyRegisteredScene(scene, out string report)) return report;
            var session = All<CoopSessionController>(scene).Single();
            if (session.GetComponent<NetworkWorldSnapshotChannel>() != null) return "Legacy world already installed; import the scene through LevelSceneInstaller.";
            RequireNewAsset("Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab");
            var channel = session.gameObject.AddComponent<NetworkWorldSnapshotChannel>(); channel.Session = session;
            channel.Catalog = session.GetComponent<NetworkPlayerSnapshotChannel>().DeepSeek.Sprites;
            // EnemyPools is populated only when explicit LevelSceneInstaller import creates the registration.
            channel.Rice = session.DeepSeek.GetComponent<DeepSleep.Runtime.Combat.Projectiles.RiceProjectilePool>();
            channel.EnemyBullets = All<DeepSleep.Runtime.Combat.Projectiles.EnemyProjectilePool2D>(scene).Single();
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
            foreach (var pool in All<DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D>(scene))
            {
                var effect = pool.gameObject.AddComponent<NetworkEffectEventChannel>();
                effect.Session = session; effect.Pool = pool; effect.EffectId = id++;
            }
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene); AssetDatabase.SaveAssets();
            return "Legacy world bootstrap installed; explicitly import with LevelSceneInstaller before Play to bind enemy pools and level identity.";
        }

        public static string Install()
        {
            var scene = LegacyScene();
            if (LevelSceneInstaller.TryApplyRegisteredScene(scene, out string report)) return report;
            if (All<CoopSessionController>(scene).Length != 0)
                return "Legacy session already installed; import the scene through LevelSceneInstaller. Existing scene left unchanged.";
            RequireNewAsset("Assets/_Project/Configs/Networking/CFG_Network.asset");
            RequireNewAsset("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            var actors = All<PlayerActor>(scene);
            var assignment = All<PlayerControlAssignment>(scene).Single();
            var selection = All<OpeningCharacterSelectionController>(scene).Single();
            var router = All<CompanionCommandRouter>(scene).Single();
            if (actors.Length != 2 || assignment == null || selection == null || router == null)
                throw new InvalidOperationException("Expected existing two-player gameplay scene");
            var root = new GameObject("NetworkSession"); Undo.RegisterCreatedObjectUndo(root, "Install cooperative networking");
            var manager = root.AddComponent<NetworkManager>(); var transport = root.AddComponent<UnityTransport>();
            var adapter = root.AddComponent<NgoTransportAdapter>(); var session = root.AddComponent<CoopSessionController>();
            var config = ScriptableObject.CreateInstance<NetworkTuningConfig>();
            config.Port = 7777; config.ClientVersion = "0.1.0";
            NetworkBuildRevision.Apply(config); config.SnapshotRate = 20;
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
            gate.AuthorityOnly = All<MonoBehaviour>(scene).Where(NetworkAuthorityRules.IsLegacyAuthorityCandidate).Cast<Behaviour>().ToArray();
            gate.AuthorityBodies = actors.Select(a => a.GetComponent<Rigidbody2D>()).ToArray();
            CreateUi(session);
            EditorUtility.SetDirty(root); EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene); AssetDatabase.SaveAssets();
            return "Legacy session + replicas + UI created; explicit level import is still required. Authority-only components: " + gate.AuthorityOnly.Length;
        }

        private static Scene LegacyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before Legacy initialization.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Select a saved legacy gameplay scene explicitly.");
            return scene;
        }

        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void RequireNewAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("Legacy initialization refuses to overwrite " + path + ". Use an existing registered level/template instead.");
        }

        private static NetworkPlayerReplica AddReplica(PlayerActor actor, CoopSessionController session, NetworkSpriteCatalog catalog)
        {
            var replica = actor.gameObject.AddComponent<NetworkPlayerReplica>(); replica.Session = session;
            replica.Role = actor.Definition.Role; replica.Body = actor.GetComponent<Rigidbody2D>();
            replica.Facing = actor.GetComponent<PlayerFacingController2D>();
            replica.Visual = (SpriteRenderer)new SerializedObject(actor.GetComponent<PlayerDownedVisual2D>())
                .FindProperty("_characterRenderer").objectReferenceValue;
            replica.VisualRoot = replica.Visual.transform.parent; replica.Sprites = catalog;
            replica.LocalHud = All<PlayerCombatHudSource>(actor.gameObject.scene)
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
