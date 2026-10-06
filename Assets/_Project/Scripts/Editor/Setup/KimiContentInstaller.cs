using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.World.Playfield;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户确认后的显式首次装配；不修改正式关卡，不重复覆盖调参或已有碰撞体。</summary>
    public static class KimiContentInstaller
    {
        public const string Root = "Assets/_Project/Prefabs/Combat/Encounters/Kimi";
        public const string ConfigRoot = "Assets/_Project/Configs/Combat/Encounters/Kimi";
        public const string RigPath = Root + "/PF_KI_MoonBladeModule.prefab";
        public const string HudPath = Root + "/PF_UI_KI_BossHud.prefab";
        private static void ConfigureGuardFormation(KimiUltimatePattern2D ultimate)
        {
            Set(ultimate.Reinforcements, "_spawnAnchor", ultimate.Boss.transform);
            var so = new SerializedObject(ultimate.Reinforcements);
            var offsets = so.FindProperty("_spawnOffsets");
            // 前方三点优先形成保护，其余点包围本体；不改普通关卡360的刷怪器。
            var positions = new[] { new Vector2(-3,.2f), new Vector2(-2.3f,1.7f), new Vector2(-2.3f,-1.5f),
                new Vector2(-.7f,2.1f), new Vector2(-.7f,-1.8f), new Vector2(1,1) };
            offsets.arraySize = positions.Length;
            for (int i=0;i<positions.Length;i++) offsets.GetArrayElementAtIndex(i).vector2Value=positions[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static string ApplyPressureRevision()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出Play。");
            string path = Art + "VFX/Kimi/VFX_KI_TargetReticle_v01.png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=512; importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.maxTextureSize=2048; importer.SaveAndReimport();
            var laserConfig=AssetDatabase.LoadAssetAtPath<KimiLaserConfig>(ConfigRoot+"/CFG_KI_Laser.asset");
            laserConfig.ShotCount=5; laserConfig.TargetMarkerDiameter=2.4f; laserConfig.TargetMarkerSpinDegrees=35;
            EditorUtility.SetDirty(laserConfig); AssetDatabase.SaveAssetIfDirty(laserConfig);
            var prism=AssetDatabase.LoadAssetAtPath<KimiPrismConfig>(ConfigRoot+"/CFG_KI_Prism.asset");
            prism.PhaseOneMirrorHealth=600; prism.PhaseTwoMirrorHealth=1200;
            EditorUtility.SetDirty(prism); AssetDatabase.SaveAssetIfDirty(prism);
            var ultimateConfig=AssetDatabase.LoadAssetAtPath<KimiUltimateConfig>(ConfigRoot+"/CFG_KI_Ultimate.asset");
            ultimateConfig.PhaseOneHits=500; ultimateConfig.PhaseTwoHits=1000;
            EditorUtility.SetDirty(ultimateConfig); AssetDatabase.SaveAssetIfDirty(ultimateConfig);
            var root=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var laser=root.GetComponentInChildren<KimiLaserPattern2D>(true);
                if(laser.TargetMarker==null) laser.TargetMarker=Renderer(laser.transform,"TargetMarker",SpriteAt("VFX_KI_TargetReticle_v01"),65);
                laser.TargetMarker.enabled=false;
                ConfigureGuardFormation(root.GetComponentInChildren<KimiUltimatePattern2D>(true));
                PrefabUtility.SaveAsPrefabAsset(root,RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var network=AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network);
            EditorUtility.SetDirty(network); AssetDatabase.SaveAssetIfDirty(network);
            return "Saved Kimi: mirrors600/1200; curtain500/1000; laser5; reticle; local guard formation; protocol7.";
        }
        /// <summary>只补两种光刃的稳定网络资源ID与兼容版本，不启用正式章节、不改旧ID。</summary>
        public static void InstallNetworkAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play first.");
            var catalog = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkSpriteCatalog>(
                "Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            var config = AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Networking.NetworkTuningConfig>(
                "Assets/_Project/Configs/Networking/CFG_Network.asset");
            if (catalog == null || config == null) throw new InvalidOperationException("Missing explicit network assets.");
            var entries = catalog.Entries.ToList(); uint next = entries.Max(e => e.Id);
            foreach (string path in new[] { Root + "/PF_KI_MoonBlade.prefab", Root + "/PF_KI_TidalBlade.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing " + path);
                foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.sprite != null && !entries.Any(e => e.Sprite == renderer.sprite))
                        entries.Add(new DeepSleep.Runtime.Networking.NetworkSpriteCatalog.Entry { Id = ++next, Sprite = renderer.sprite });
            }
            catalog.Entries = entries.ToArray();
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(config);
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        }
        private const string Art = "Assets/_Project/Art/";
        private const string EnemyBullet = "Assets/_Project/Prefabs/Combat/Projectiles/Enemies/DataCrawlerSnake/PF_Projectile_EN_DataCrawlerSnake.prefab";
        private const string EnemyHit = "Assets/_Project/Prefabs/Combat/VFX/Enemies/DataCrawlerSnake/PF_VFX_EN_DataCrawlerSnake_ProjectileHit.prefab";

        [MenuItem("DeepSleep/开发/Kimi/首次装配本体与月光刃模块")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出 Play。");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) != null)
                throw new InvalidOperationException("已装配，拒绝覆盖已有调参。请在配置与Prefab中定点修改。");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(ConfigRoot);
            AssetDatabase.Refresh();
            ImportArt();
            var bossConfig = ScriptableObject.CreateInstance<KimiBossConfig>();
            bossConfig.MaximumHealth = 10000;
            bossConfig.PhaseTwoHealthFraction = .5f;
            bossConfig.Poses = Enum.GetNames(typeof(KimiPose)).Select(p => SpriteAt("SPR_KI_" + p + "_v01")).ToArray();
            SaveNew(bossConfig, ConfigRoot + "/CFG_KI_Boss.asset");
            var moonConfig = ScriptableObject.CreateInstance<KimiMoonBladeConfig>();
            moonConfig.LaneCount = 10; moonConfig.VolleyCount = 10;
            moonConfig.PhaseOneBlades = 2; moonConfig.PhaseTwoBlades = 4;
            moonConfig.WarningSeconds = .7f; moonConfig.VolleyGapSeconds = .2f;
            moonConfig.EdgePadding = 2.5f; moonConfig.WarningWidth = .56f;
            moonConfig.WarningColor = new Color(.58f, .72f, 1f, .28f);
            SaveNew(moonConfig, ConfigRoot + "/CFG_KI_MoonBlade.asset");
            var projectileConfig = ScriptableObject.CreateInstance<EnemyProjectileConfig>();
            Set(projectileConfig, "_speed", 24f); Set(projectileConfig, "_lifetimeSeconds", 1.35f);
            Set(projectileConfig, "_damage", 1f); Set(projectileConfig, "_collisionLayers", 1 << LayerMask.NameToLayer("Player"));
            Set(projectileConfig, "_initialPoolSize", 4); Set(projectileConfig, "_maximumPoolSize", 4);
            SaveNew(projectileConfig, ConfigRoot + "/CFG_KI_MoonProjectile.asset");
            var hitConfig = ScriptableObject.CreateInstance<OneShotSpriteEffectConfig>();
            Set(hitConfig, "_worldDiameter", 1.1f); Set(hitConfig, "_durationSeconds", .28f);
            SaveNew(hitConfig, ConfigRoot + "/CFG_KI_Hit.asset");

            GameObject hit = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyHit));
            hit.name = "PF_VFX_KI_Hit";
            hit.GetComponentInChildren<SpriteRenderer>(true).sprite = SpriteAt("VFX_KI_Hit_v01");
            var hitPrefab = SavePrefab(hit, Root + "/PF_VFX_KI_Hit.prefab");

            GameObject bullet = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBullet));
            bullet.name = "PF_KI_MoonBlade";
            var projectile = bullet.GetComponent<EnemyProjectile2D>();
            Set(projectile, "_canBeCleared", false);
            // 换成狭长胶囊只在新建 Kimi 副本中进行，不修改蛇或任何用户已有体积。
            foreach (Collider2D old in bullet.GetComponents<Collider2D>()) UnityEngine.Object.DestroyImmediate(old);
            var shape = bullet.AddComponent<CapsuleCollider2D>();
            shape.isTrigger = true; shape.direction = CapsuleDirection2D.Horizontal;
            shape.size = new Vector2(1.9f, .56f); shape.offset = new Vector2(.45f, 0);
            Set(projectile, "_bodyCollider", shape);
            var body = bullet.GetComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var bulletRenderer = bullet.GetComponentInChildren<SpriteRenderer>(true);
            bulletRenderer.sprite = SpriteAt("VFX_KI_MoonBlade_v01");
            bulletRenderer.flipX = true;
            // 核对可见轮廓高382px：1.44倍 => 1.074u，约一条通道；不是拉长位图。
            if (bulletRenderer.transform == bullet.transform)
            {
                GameObject visual = Child(bullet.transform, "Visual");
                SpriteRenderer moved = visual.AddComponent<SpriteRenderer>();
                EditorUtility.CopySerialized(bulletRenderer, moved);
                UnityEngine.Object.DestroyImmediate(bulletRenderer); bulletRenderer = moved;
            }
            bulletRenderer.transform.localScale = Vector3.one * 1.44f;
            bulletRenderer.sortingLayerName = "Gameplay"; bulletRenderer.sortingOrder = 45;
            var perception = bullet.GetComponent<CombatPerceptionBody2D>();
            if (perception == null) perception = bullet.AddComponent<CombatPerceptionBody2D>();
            perception.Shape = shape; perception.Body = body; perception.Projectile = projectile;
            var bulletPrefab = SavePrefab(bullet, Root + "/PF_KI_MoonBlade.prefab");

            GameObject root = new GameObject("PF_KI_MoonBladeModule");
            root.SetActive(false);
            var boss = CreateBoss(root.transform, bossConfig);
            var effects = Child(root.transform, "SharedHitEffects").AddComponent<OneShotSpriteEffectPool2D>();
            Set(effects, "_effectPrefab", hitPrefab.GetComponent<OneShotSpriteEffect2D>());
            Set(effects, "_poolRoot", effects.transform); Set(effects, "_config", hitConfig);
            var pool = Child(root.transform, "MoonBladePool").AddComponent<EnemyProjectilePool2D>();
            Set(pool, "_projectilePrefab", bulletPrefab.GetComponent<EnemyProjectile2D>());
            Set(pool, "_poolRoot", pool.transform); Set(pool, "_config", projectileConfig);
            Set(pool, "_impactEffectPool", effects);
            var pattern = root.AddComponent<KimiMoonBladePattern2D>();
            pattern.Config = moonConfig; pattern.ProjectileConfig = projectileConfig; pattern.Boss = boss;
            pattern.Projectiles = pool;
            pattern.Playfield = AssetDatabase.LoadAssetAtPath<CombatPlayfieldConfig>("Assets/_Project/Configs/World/CFG_CombatPlayfield_Default.asset");
            var material = new Material(Shader.Find("Sprites/Default"));
            SaveNew(material, ConfigRoot + "/MAT_KI_Warning.mat");
            pattern.Warnings = new LineRenderer[moonConfig.PhaseTwoBlades];
            for (int i = 0; i < pattern.Warnings.Length; i++)
            {
                var line = Child(root.transform, "MoonWarning" + i).AddComponent<LineRenderer>();
                line.sharedMaterial = material; line.sortingLayerName = "Gameplay"; line.sortingOrder = 3;
                line.enabled = false; line.positionCount = 2;
                pattern.Warnings[i] = line;
            }
            root.SetActive(true);
            SavePrefab(root, RigPath);
            CreateHud();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kimi] 26张确认素材已导入，本体/月光刃模块和uGUI已装配；正式第四波未启用。");
        }

        private static KimiBoss2D CreateBoss(Transform parent, KimiBossConfig config)
        {
            GameObject root = Child(parent, "Kimi"); root.layer = LayerMask.NameToLayer("Enemy");
            root.transform.localPosition = new Vector3(6.5f, -.8f, 0);
            var boss = root.AddComponent<KimiBoss2D>(); boss.Config = config;
            var shape = root.AddComponent<CapsuleCollider2D>(); shape.isTrigger = true;
            shape.size = new Vector2(.65f, 1.55f); shape.offset = new Vector2(0, 1.15f);
            boss.HitCollider = shape; shape.enabled = false;
            var hitbox = root.AddComponent<DamageHitbox2D>(); Set(hitbox, "_receiverComponent", boss);
            var perception = root.AddComponent<CombatPerceptionBody2D>();
            perception.Shape = shape; perception.Hitbox = hitbox; perception.TargetValue = 2;
            boss.Perception = perception;
            var group = root.AddComponent<SortingGroup>(); group.sortingLayerName = "Gameplay"; group.sortingOrder = 20;
            boss.Cloud = Renderer(root.transform, "Cloud", SpriteAt("SPR_KI_CloudPlatform_v01"), -2);
            boss.Cloud.transform.localScale = Vector3.one * .62f;
            boss.Cloud.transform.localPosition = new Vector3(0, -.17f, 0);
            boss.Body = Renderer(root.transform, "Body", config.Poses[0], 0);
            SpriteRenderer ghost = Renderer(root.transform, "PoseGhost", config.Poses[0], -1);
            ghost.enabled = false;
            var transition = root.AddComponent<SpritePoseTransition2D>();
            Set(transition, "_subjectRenderer", boss.Body); Set(transition, "_ghostRenderer", ghost);
            Set(transition, "_config", AssetDatabase.LoadAssetAtPath<SpritePoseTransitionConfig>("Assets/_Project/Configs/Presentation/CFG_SpritePoseTransition_Default.asset"));
            boss.PoseTransition = transition;
            var overlay = Renderer(root.transform, "HitFlash", config.Poses[0], 2);
            overlay.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Shaders/MAT_PlayerHitOverlay.mat");
            overlay.enabled = false;
            var flash = root.AddComponent<SpriteHitFlash2D>();
            flash.DamageSource = boss; flash.Sources = new[] { boss.Body }; flash.Overlay = overlay;
            return boss;
        }

        [MenuItem("DeepSleep/开发/Kimi/装配棱光模块")]
        public static void InstallPrism()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("先退出Play。");
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if(existing==null || existing.GetComponentInChildren<KimiPrismPattern2D>(true)!=null)
                throw new InvalidOperationException("必须已有本体且尚未装配棱光；不覆盖已有调参。");
            string edgePath=Art+"VFX/Kimi/SPR_KI_MirrorEdge_v01.png";
            var edgeImporter=(TextureImporter)AssetImporter.GetAtPath(edgePath);
            edgeImporter.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable CS0618
            edgeImporter.spritesheet=new[]{new SpriteMetaData{name="SPR_KI_MirrorEdge_v01",rect=new Rect(48,294,2076,148),alignment=0,pivot=new Vector2(.5f,.5f)}};
#pragma warning restore CS0618
            edgeImporter.SaveAndReimport();
            var cornerImporter=(TextureImporter)AssetImporter.GetAtPath(Art+"VFX/Kimi/SPR_KI_MirrorCorner_v01.png");
            var settings=new TextureImporterSettings();cornerImporter.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=new Vector2(214f/1295f,1-213f/1214f);
            cornerImporter.SetTextureSettings(settings);cornerImporter.SaveAndReimport();
            var config=ScriptableObject.CreateInstance<KimiPrismConfig>();
            config.Duration=12;config.FirstShotDelay=.8f;config.ShotInterval=1;
            config.PhaseOneSpeed=5;config.PhaseTwoSpeed=7.5f;
            config.PhaseOneMirrorHealth=600;config.PhaseTwoMirrorHealth=1200;
            config.ContactDamage=1;config.ContactInterval=.8f;config.OrbRadius=.18f;
            config.ArenaInset=.55f;config.ExitPadding=2;config.PlayerLayers=1<<LayerMask.NameToLayer("Player");
            SaveNew(config,ConfigRoot+"/CFG_KI_Prism.asset");
            var breakConfig=ScriptableObject.CreateInstance<OneShotSpriteEffectConfig>();
            Set(breakConfig,"_worldDiameter",2f);Set(breakConfig,"_durationSeconds",.45f);
            Set(breakConfig,"_prewarmCount",16);Set(breakConfig,"_maximumCount",24);
            SaveNew(breakConfig,ConfigRoot+"/CFG_KI_MirrorBreak.asset");
            var burst=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/PF_VFX_KI_Hit.prefab"));
            burst.name="PF_VFX_KI_MirrorBreak";burst.GetComponentInChildren<SpriteRenderer>(true).sprite=SpriteAt("VFX_KI_MirrorBreak_v01");
            var burstPrefab=SavePrefab(burst,Root+"/PF_VFX_KI_MirrorBreak.prefab");
            GameObject root=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var moon=root.GetComponent<KimiMoonBladePattern2D>();
                var prism=Child(root.transform,"Prism").AddComponent<KimiPrismPattern2D>();
                prism.Config=config;prism.Playfield=moon.Playfield;prism.Boss=moon.Boss;
                prism.Muzzle=Child(prism.Boss.transform,"PrismMuzzle").transform;prism.Muzzle.localPosition=new Vector3(-.55f,1.15f,0);
                prism.HitEffects=root.GetComponentInChildren<OneShotSpriteEffectPool2D>(true);
                prism.BreakEffects=Child(prism.transform,"MirrorBreakEffects").AddComponent<OneShotSpriteEffectPool2D>();
                Set(prism.BreakEffects,"_effectPrefab",burstPrefab.GetComponent<OneShotSpriteEffect2D>());
                Set(prism.BreakEffects,"_poolRoot",prism.BreakEffects.transform);Set(prism.BreakEffects,"_config",breakConfig);
                prism.Sides=new KimiMirrorSide2D[4];prism.Corners=new SpriteRenderer[4];
                for(int i=0;i<4;i++)
                {
                    var go=Child(prism.transform,new[]{"LeftMirror","RightMirror","BottomMirror","TopMirror"}[i]);
                    go.layer=LayerMask.NameToLayer("Enemy");
                    var side=go.AddComponent<KimiMirrorSide2D>();
                    side.Shape=go.AddComponent<BoxCollider2D>();side.Shape.isTrigger=true;side.Shape.enabled=false;
                    side.Visual=Renderer(go.transform,"MirrorTiles",SpriteAt("SPR_KI_MirrorEdge_v01"),30);
                    side.Visual.drawMode=SpriteDrawMode.Tiled;side.Visual.enabled=false;side.Thickness=.27f;side.FlashSeconds=.12f;
                    var hit=go.AddComponent<DamageHitbox2D>();Set(hit,"_receiverComponent",side);
                    side.Perception=go.AddComponent<CombatPerceptionBody2D>();side.Perception.Shape=side.Shape;
                    side.Perception.Hitbox=hit;side.Perception.TargetValue=1;
                    prism.Sides[i]=side;
                    prism.Corners[i]=Renderer(prism.transform,"PrismCorner"+i,SpriteAt("SPR_KI_MirrorCorner_v01"),31);
                    prism.Corners[i].transform.localScale=Vector3.one*.6f;prism.Corners[i].enabled=false;
                }
                prism.Orbs=new KimiPrismOrb2D[16];
                for(int i=0;i<prism.Orbs.Length;i++)
                {
                    var go=Child(prism.transform,"PrismOrb_"+i);go.layer=LayerMask.NameToLayer("EnemyProjectile");
                    var orb=go.AddComponent<KimiPrismOrb2D>();orb.Shape=go.AddComponent<CircleCollider2D>();
                    orb.Shape.isTrigger=true;orb.Shape.radius=config.OrbRadius;orb.Shape.enabled=false;
                    orb.Visual=Renderer(go.transform,"Visual",SpriteAt("VFX_KI_PrismOrb_v01"),45);
                    orb.Visual.transform.localScale=Vector3.one*.32f;orb.Visual.enabled=false;
                    prism.Orbs[i]=orb;
                }
                PrefabUtility.SaveAsPrefabAsset(root,RigPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            Debug.Log("[Kimi] 棱光四边和16球预热模块已装配；未启用正式第四波。");
        }

        [MenuItem("DeepSleep/开发/Kimi/装配完整技能编排")]
        public static void InstallEncounter()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("先退出Play。");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if(prefab==null || prefab.GetComponent<KimiEncounter2D>()!=null)throw new InvalidOperationException("缺少模块或已装配，拒绝覆盖。");
            var config=ScriptableObject.CreateInstance<KimiEncounterConfig>();
            config.PreludeSeconds=20;config.EntrySeconds=2;config.PhaseChangeSeconds=1.5f;
            config.CastGapSeconds=2;config.CycleGapSeconds=5;config.CastsPerCycle=5;
            config.BossPosition=new Vector2(6.5f,-.8f);config.RevealedTitle="月之暗面";config.ObjectiveText="通过 Kimi 的试炼";config.DisplaySeconds=999;
            SaveNew(config,ConfigRoot+"/CFG_KI_Encounter.asset");
            var root=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var encounter=root.AddComponent<KimiEncounter2D>();encounter.Config=config;
                encounter.Boss=root.GetComponentInChildren<KimiBoss2D>(true);
                encounter.Moon=root.GetComponentInChildren<KimiMoonBladePattern2D>(true);
                encounter.Prism=root.GetComponentInChildren<KimiPrismPattern2D>(true);
                encounter.Laser=root.GetComponentInChildren<KimiLaserPattern2D>(true);
                encounter.Ultimate=root.GetComponentInChildren<KimiUltimatePattern2D>(true);
                PrefabUtility.SaveAsPrefabAsset(root,RigPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("[Kimi] 技能编排已装配；正式章节计时/网络仍须成套接入。");
        }

        [MenuItem("DeepSleep/开发/Kimi/装配吹笛大招模块")]
        public static void InstallUltimate()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("先退出Play。");
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if(existing==null || existing.GetComponentInChildren<KimiUltimatePattern2D>(true)!=null)
                throw new InvalidOperationException("缺少本体或大招已装配，拒绝覆盖。");
            var config=ScriptableObject.CreateInstance<KimiUltimateConfig>();
            config.PhaseOneHits=500;config.PhaseTwoHits=1000;config.PhaseOneChargeSeconds=10;config.PhaseTwoChargeSeconds=14;
            config.StaggerSeconds=3;config.ReleaseDelay=.8f;config.VolleyInterval=1.1f;
            config.VolleyCount=5;config.PhaseTwoBlades=3;config.SpreadDegrees=22;
            config.ReinforcementLimit=10;config.ReinforcementHealthMultiplier=3;
            config.CurtainOffset=new Vector2(-2,1.15f);config.MuzzleOffset=new Vector2(-1.2f,1.15f);
            SaveNew(config,ConfigRoot+"/CFG_KI_Ultimate.asset");
            var bladeConfig=ScriptableObject.CreateInstance<EnemyProjectileConfig>();
            Set(bladeConfig,"_speed",6f);Set(bladeConfig,"_lifetimeSeconds",5f);Set(bladeConfig,"_damage",2f);
            Set(bladeConfig,"_initialPoolSize",15);Set(bladeConfig,"_maximumPoolSize",15);
            Set(bladeConfig,"_collisionLayers",1<<LayerMask.NameToLayer("Player"));
            SaveNew(bladeConfig,ConfigRoot+"/CFG_KI_TidalProjectile.asset");
            var bullet=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/PF_KI_MoonBlade.prefab"));
            bullet.name="PF_KI_TidalBlade";
            var visual=bullet.GetComponentInChildren<SpriteRenderer>(true);visual.sprite=SpriteAt("VFX_KI_TidalBlade_v01");
            visual.flipX=visual.flipY=false;
            visual.transform.localScale=Vector3.one*1.45f;
            visual.transform.localRotation=Quaternion.Euler(0,0,180); // 源图朝左，通用弹体局部+X前进。
            var shape=bullet.GetComponent<CapsuleCollider2D>();shape.direction=CapsuleDirection2D.Vertical;
            shape.size=new Vector2(1.1f,3.6f);shape.offset=new Vector2(.65f,0);
            var bladePrefab=SavePrefab(bullet,Root+"/PF_KI_TidalBlade.prefab");
            var guardSchedule=ScriptableObject.CreateInstance<DeepSleep.Runtime.Combat.Enemies.EnemySpawnScheduleConfig>();
            Set(guardSchedule,"_initialDelaySeconds",0f);Set(guardSchedule,"_minimumIntervalSeconds",.65f);
            Set(guardSchedule,"_maximumIntervalSeconds",.85f);Set(guardSchedule,"_maximumAliveCount",10);
            Set(guardSchedule,"_horizontalSpawnMargin",1f);Set(guardSchedule,"_verticalPadding",1.8f);
            Set(guardSchedule,"_spawnFromBothSides",false);
            SaveNew(guardSchedule,ConfigRoot+"/CFG_KI_GuardSchedule.asset");
            var guardPoolConfig=ScriptableObject.CreateInstance<DeepSleep.Runtime.Combat.Enemies.EnemyPoolConfig>();
            Set(guardPoolConfig,"_initialCapacity",10);Set(guardPoolConfig,"_maximumCapacity",10);
            SaveNew(guardPoolConfig,ConfigRoot+"/CFG_KI_GuardPool.asset");
            var root=PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var ultimate=Child(root.transform,"FluteUltimate").AddComponent<KimiUltimatePattern2D>();
                ultimate.Config=config;ultimate.Boss=root.GetComponentInChildren<KimiBoss2D>(true);
                var prism=root.GetComponentInChildren<KimiPrismPattern2D>(true);ultimate.BreakEffects=prism.BreakEffects;
                var curtainRoot=Child(ultimate.transform,"HitCurtain");curtainRoot.layer=LayerMask.NameToLayer("Enemy");
                var curtain=curtainRoot.AddComponent<KimiHitCurtain2D>();ultimate.Curtain=curtain;
                curtain.Shape=curtainRoot.AddComponent<BoxCollider2D>();curtain.Shape.size=new Vector2(1.5f,6);
                curtain.Shape.isTrigger=true;curtain.Shape.enabled=false;
                curtain.Visual=Renderer(curtainRoot.transform,"Visual",SpriteAt("VFX_KI_UltimateCurtain_v01"),30);
                curtain.Visual.transform.localScale=Vector3.one*2;curtain.Visual.enabled=false;
                var hitbox=curtainRoot.AddComponent<DamageHitbox2D>();Set(hitbox,"_receiverComponent",curtain);
                curtain.Perception=curtainRoot.AddComponent<CombatPerceptionBody2D>();
                curtain.Perception.Shape=curtain.Shape;curtain.Perception.Hitbox=hitbox;curtain.Perception.TargetValue=3;
                var overlay=Renderer(curtainRoot.transform,"HitFlash",curtain.Visual.sprite,31);
                overlay.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Shaders/MAT_PlayerHitOverlay.mat");overlay.enabled=false;
                var flash=curtainRoot.AddComponent<SpriteHitFlash2D>();flash.DamageSource=curtain;flash.Sources=new[]{curtain.Visual};flash.Overlay=overlay;
                var pool=Child(ultimate.transform,"TidalBladePool").AddComponent<EnemyProjectilePool2D>();
                Set(pool,"_projectilePrefab",bladePrefab.GetComponent<EnemyProjectile2D>());Set(pool,"_poolRoot",pool.transform);
                Set(pool,"_config",bladeConfig);Set(pool,"_impactEffectPool",prism.HitEffects);ultimate.Blades=pool;
                var guards=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Combat/Enemies/Internet/PF_EnemyRuntime_SecurityGuard.prefab"),ultimate.transform);
                ultimate.Reinforcements=guards.GetComponent<DeepSleep.Runtime.Combat.Enemies.EnemySpawnDirector2D>();
                ultimate.ReinforcementPool=guards.GetComponent<DeepSleep.Runtime.Combat.Enemies.EnemyActorPool2D>();
                Set(ultimate.Reinforcements,"_schedule",guardSchedule);Set(ultimate.Reinforcements,"_autoStart",false);
                ConfigureGuardFormation(ultimate);
                Set(ultimate.ReinforcementPool,"_config",guardPoolConfig);
                PrefabUtility.SaveAsPrefabAsset(root,RigPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("[Kimi] 大招光幕/巨刃/专属360增援装配完成，正式关卡仍未启用。");
        }

        [MenuItem("DeepSleep/开发/Kimi/装配锁向激光模块")]
        public static void InstallLaser()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出Play。");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null || prefab.GetComponentInChildren<KimiLaserPattern2D>(true) != null)
                throw new InvalidOperationException("缺少本体模块或激光已装配；拒绝覆盖调参。");
            string texturePath = Art + "VFX/Kimi/TEX_KI_LaserBody_v01.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            // 原图两端不保证同像素；镜像重复在接缝共用同一边，不改PNG也不把整图拉长。
            importer.wrapModeU = TextureWrapMode.Mirror; importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var config = ScriptableObject.CreateInstance<KimiLaserConfig>();
            config.ChargeSeconds = 1.2f; config.FireSeconds = .45f; config.RecoverySeconds = .35f;
            config.ShotCount = 5; config.TargetMarkerDiameter = 2.4f; config.TargetMarkerSpinDegrees = 35;
            config.Length = 40; config.DamageWidth = 1.2f; config.VisualWidth = 2.8f;
            config.TextureRepeatLength = config.VisualWidth * texture.width / texture.height;
            config.TextureScrollSpeed = .6f; config.Damage = 1;
            config.FocusStartScale = .3f; config.FocusEndScale = .7f; config.FocusSpinDegrees = 40;
            config.WarningColor = new Color(.72f,.65f,1,.22f); config.BeamColor = Color.white;
            config.PlayerLayers = 1 << LayerMask.NameToLayer("Player");
            SaveNew(config, ConfigRoot + "/CFG_KI_Laser.asset");
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Combat/Harness/MAT_HA_LaserBeam_v01.mat");
            var material = new Material(template); material.SetTexture("_BaseMap", texture);
            SaveNew(material, ConfigRoot + "/MAT_KI_Laser.mat");
            var warningMaterial = new Material(template);
            SaveNew(warningMaterial, ConfigRoot + "/MAT_KI_LaserWarning.mat");
            // Unity临时whiteTexture不持久化；纯几何预警的常量采样存为材质子资产。
            var white = new Texture2D(1,1,TextureFormat.RGBA32,false) { name = "WarningConstantWhite" };
            white.SetPixel(0,0,Color.white); white.Apply();
            AssetDatabase.AddObjectToAsset(white,warningMaterial);
            warningMaterial.SetTexture("_BaseMap",white);EditorUtility.SetDirty(warningMaterial);
            var root = PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var laser = Child(root.transform, "LockedLaser").AddComponent<KimiLaserPattern2D>();
                laser.Config = config; laser.Boss = root.GetComponentInChildren<KimiBoss2D>(true);
                laser.Muzzle = Child(laser.Boss.transform, "LaserOrigin").transform;
                laser.Muzzle.localPosition = new Vector3(-1.05f,1.5f,0);
                laser.HitEffects = root.GetComponentInChildren<KimiPrismPattern2D>(true).HitEffects;
                laser.Warning = LaserMesh(laser.transform,"Warning",warningMaterial,laser.Boss.Body,23);
                laser.Beam = LaserMesh(laser.transform,"Beam",material,laser.Boss.Body,24);
                laser.Focus = Renderer(laser.transform,"Focus",SpriteAt("VFX_KI_LaserFocus_v01"),46);
                laser.Focus.enabled = false;
                laser.TargetMarker = Renderer(laser.transform,"TargetMarker",SpriteAt("VFX_KI_TargetReticle_v01"),65);
                laser.TargetMarker.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root,RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Kimi] 锁向激光装配完成，正式关卡未启用。");
        }

        private static DeepSleep.Runtime.Combat.Beams.Presentation.BeamTiledMeshView2D LaserMesh(
            Transform parent,string name,Material material,Renderer sorting,int order)
        {
            var go = Child(parent,name);var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();renderer.sharedMaterial = material;renderer.enabled = false;
            var view = go.AddComponent<DeepSleep.Runtime.Combat.Beams.Presentation.BeamTiledMeshView2D>();
            Set(view,"_meshFilter",filter);Set(view,"_meshRenderer",renderer);
            Set(view,"_sortingReferenceRenderer",sorting);Set(view,"_orderOffset",order);return view;
        }

        private static void ImportArt()
        {
            string[] folders = {"Characters/Kimi", "VFX/Kimi", "UI/Kimi", "Backgrounds/Kimi"};
            foreach (string folder in folders)
            foreach (string path in Directory.GetFiles(Art + folder, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 512;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096; importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                // 姿态脚底统一到同一云面；本体不把物理根跟着素材摆动。
                bool character = folder == "Characters/Kimi" && !path.Contains("Cloud");
                settings.spritePivot = character ? new Vector2(.5f, .028f) : new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                string name = Path.GetFileNameWithoutExtension(path);
                Rect crop = default;
                if (name == "UI_KI_HealthFrame_v01") crop = new Rect(25, 277, 2123, 231);
                if (name == "UI_KI_HealthBack_v01") crop = new Rect(25, 450, 1536, 91);
                if (name == "UI_KI_HealthFill_v01") crop = new Rect(39, 455, 1508, 87);
                if (name == "UI_KI_ShieldCounterFrame_v01") crop = new Rect(100, 147, 1967, 453);
                if (crop.width > 0)
                {
                    importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618
                    importer.spritesheet = new[] { new SpriteMetaData { name = name, rect = crop, alignment = 0, pivot = new Vector2(.5f, .5f) } };
#pragma warning restore CS0618
                }
                importer.SaveAndReimport();
            }
        }

        private static void CreateHud()
        {
            var root = new GameObject("PF_UI_KI_BossHud", typeof(RectTransform)); root.SetActive(false);
            var group = root.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
            var safe = root.AddComponent<SafeAreaRectFitter>(); Set(safe, "_target", (RectTransform)root.transform); Set(safe, "_symmetricInsets", true);
            var view = root.AddComponent<KimiBossHudView>(); view.Visibility = group; view.DisplayName = "Kimi · 月之暗面";
            RectTransform panel = Ui(root.transform, "BossBar", new Vector2(720, 120), new Vector2(0, -175));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1); panel.pivot = new Vector2(.5f, 1);
            // 坐标与已验收离线预览一致：内槽裁切层在框下，禁止填充直接铺在框外。
            RectTransform slot = Ui(panel, "InnerSlotMask", new Vector2(583, 20), new Vector2(.5f, -54));
            slot.gameObject.AddComponent<RectMask2D>();
            var back = UiImage(slot, "Back", SpriteAt("UI_KI_HealthBack_v01"), new Vector2(583, 34.5f), Vector2.zero);
            var fill = UiImage(slot, "Fill", SpriteAt("UI_KI_HealthFill_v01"), new Vector2(583, 33.6f), Vector2.zero);
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(.5f,.5f);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(.5f,.5f);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            view.HealthFill = fill;
            UiImage(panel, "Frame", SpriteAt("UI_KI_HealthFrame_v01"), new Vector2(720, 78.34f), new Vector2(0, -39.17f));
            view.HealthText = Label(panel, "BossNameAndHealth", new Vector2(720, 32), new Vector2(0, -97));
            RectTransform shield = Ui(panel, "ShieldCounter", new Vector2(270,62.18f), new Vector2(0,-155));
            var shieldImage = shield.gameObject.AddComponent<Image>(); shieldImage.sprite = SpriteAt("UI_KI_ShieldCounterFrame_v01"); shieldImage.raycastTarget = false;
            view.ShieldPanel = shield.gameObject;
            view.ShieldText = Label(shield, "RemainingHits", new Vector2(230,35), Vector2.zero);
            view.ShieldText.rectTransform.anchorMin = view.ShieldText.rectTransform.anchorMax = new Vector2(.5f,.5f);
            root.SetActive(true); SavePrefab(root, HudPath);
        }

        private static RectTransform Ui(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static Image UiImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 position)
        {
            var image = Ui(parent, name, size, position).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        private static Text Label(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var text = Ui(parent, name, size, position).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.9f,.92f,1); text.raycastTarget = false;
            return text;
        }

        private static Sprite SpriteAt(string name)
        {
            string folder = name.StartsWith("UI_") ? "UI/Kimi" : name.StartsWith("BG_") ? "Backgrounds/Kimi" :
                name.StartsWith("SPR_KI_") && !name.Contains("Mirror") ? "Characters/Kimi" : "VFX/Kimi";
            return AssetDatabase.LoadAllAssetsAtPath(Art + folder + "/" + name + ".png").OfType<Sprite>().Single();
        }
        private static GameObject Child(Transform parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order)
        {
            var renderer = Child(parent, name).AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingLayerName = "Gameplay"; renderer.sortingOrder = order;
            return renderer;
        }
        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject result = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root); return result;
        }
        private static void SaveNew(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("拒绝覆盖 " + path);
            AssetDatabase.CreateAsset(asset, path);
        }
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string name, float value)
        { var so = new SerializedObject(target); so.FindProperty(name).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string name, int value)
        { var so = new SerializedObject(target); so.FindProperty(name).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(UnityEngine.Object target, string name, bool value)
        { var so = new SerializedObject(target); so.FindProperty(name).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
