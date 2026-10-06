using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Presentation.Poses;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.World.Playfield;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>一次性显式制作两种新敌人，随后通过现有关卡登记工具派生接线，不重建原有敌人。</summary>
    public static class InternetEnemyInstaller
    {
        private const string Root = "Assets/_Project/";
        private const string ConfigRoot = Root + "Configs/Combat/Enemies/Internet/";
        private const string PrefabRoot = Root + "Prefabs/Combat/Enemies/Internet/";
        private static readonly string[] Art = {
            "DownloadCharger/SPR_EN_Download_Idle.png", "DownloadCharger/SPR_EN_Download_Charge.png",
            "DownloadCharger/SPR_EN_Download_Dash.png", "SecurityGuard/SPR_EN_SecurityGuard_Idle.png",
            "SecurityGuard/SPR_EN_SecurityGuard_Shield.png" };

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "PF_Enemy_Download.prefab") != null)
                throw new InvalidOperationException("Already installed. Do not overwrite tuned colliders; edit the existing assets.");
            Directory.CreateDirectory(ConfigRoot); Directory.CreateDirectory(PrefabRoot); AssetDatabase.Refresh();
            var sprites = Art.Select(Import).ToArray();
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var slot = tags.FindProperty("layers").GetArrayElementAtIndex(16);
            if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != "PlayerAttackBlocker")
                throw new InvalidOperationException("Layer16 is already occupied.");
            slot.stringValue = "PlayerAttackBlocker"; tags.ApplyModifiedPropertiesWithoutUndo();
            var download = CreateEnemy("Download", true, sprites);
            var guard = CreateEnemy("SecurityGuard", false, sprites);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/World01_EarlyInternet.unity", OpenSceneMode.Additive);
            try
            {
                var bindings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
                AddToLevel(bindings, download, "download", 1, 120);
                AddToLevel(bindings, guard, "security-guard", 2, 122);
                LevelSceneInstaller.Apply(bindings, false);
                var audio = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatAudioPresenter>(true)).Single();
                audio.EnemyPools = bindings.Enemies.Select(e => e.Pool).ToArray(); EditorUtility.SetDirty(audio);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            ConfigurePlayerBlocking();
            var catalog = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>(Root + "Configs/Networking/CFG_NetworkSprites.asset");
            var entries = catalog.Entries.ToList(); uint id = entries.Max(e => e.Id) + 1;
            foreach (var sprite in sprites) if (!entries.Any(e => e.Sprite == sprite))
                entries.Add(new NetworkSpriteCatalog.Entry { Id = id++, Sprite = sprite });
            catalog.Entries = entries.ToArray(); EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return "Two enemies installed in World01; low-density channels, common health/rewards/pools/replication; five sprites.";
        }

        private static Sprite Import(string relative)
        {
            string path = Root + "Art/Enemies/" + relative;
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512; importer.spritePivot = new Vector2(.5f, .5f);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject CreateEnemy(string name, bool charger, Sprite[] sprites)
        {
            var health = Create<HealthConfig>(name + "_Health"); Set(health, "_maximumHealth", charger ? 6f : 8f);
            var poolConfig = Create<EnemyPoolConfig>(name + "_Pool"); Set(poolConfig, "_initialCapacity", 4); Set(poolConfig, "_maximumCapacity", 8);
            var motion = Create<DataCrawlerSnakeMotionConfig>(name + "_Motion");
            Set(motion, "_minimumSpeed", charger ? 2.4f : .65f); Set(motion, "_maximumSpeed", charger ? 2.8f : .85f);
            Set(motion, "_stoppingDistance", 0f);
            var schedule = Create<EnemySpawnScheduleConfig>(name + "_Schedule");
            Set(schedule, "_spawnFromBothSides", charger);
            Set(schedule, "_minimumIntervalSeconds", charger ? 7f : 10f);
            Set(schedule, "_maximumIntervalSeconds", charger ? 10f : 14f);
            Set(schedule, "_maximumAliveCount", 3); Set(schedule, "_randomSeed", charger ? 2003 : 2006);
            var channel = Create<EnemySpawnChannelDefinition>(name + "_Channel"); Set(channel, "_displayName", charger ? "离线下载" : "360 拦截卫士");
            var playfield = AssetDatabase.LoadAssetAtPath<CombatPlayfieldConfig>(Root + "Configs/World/CFG_CombatPlayfield_Default.asset");
            var source = Root + "Prefabs/Combat/Enemies/404Window/PF_Enemy_404Window.prefab";
            var go = PrefabUtility.LoadPrefabContents(source);
            EnemyActor2D actorAsset;
            try
            {
                go.name = "PF_Enemy_" + name; go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                var actor = go.GetComponent<EnemyActor2D>(); var contact = go.GetComponent<EnemyContactAttack2D>();
                Object.DestroyImmediate(go.GetComponent<EnemyTumbleMotor2D>());
                Set(go.GetComponent<HealthComponent>(), "_config", health);
                var body = go.GetComponent<Rigidbody2D>();
                var collider = go.GetComponent<BoxCollider2D>(); collider.offset = Vector2.zero;
                collider.size = charger ? new Vector2(1.4f, .7f) : new Vector2(.92f, 1.28f);
                var flash = go.GetComponent<SpriteHitFlash2D>(); var renderer = flash.Sources[0];
                renderer.transform.parent.localScale = Vector3.one;
                renderer.transform.parent.localRotation = Quaternion.identity;
                renderer.sprite = sprites[charger ? 0 : 3]; renderer.transform.localPosition = Vector3.zero;
                renderer.transform.localRotation = Quaternion.identity;
                // 首次制作按可见轮廓定等比尺寸，后续不运行安装器覆盖人工调参。
                float scale = VisualScale(renderer.sprite, charger ? 1.9f : 1.7f, !charger);
                renderer.transform.localScale = Vector3.one * scale;
                var ghost = new GameObject("PoseGhost", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                ghost.transform.SetParent(go.transform, false); ghost.sharedMaterial = renderer.sharedMaterial;
                ghost.sortingLayerID = renderer.sortingLayerID; ghost.sortingOrder = renderer.sortingOrder - 1; ghost.enabled = false;
                var transition = go.AddComponent<SpritePoseTransition2D>();
                Set(transition, "_subjectRenderer", renderer); Set(transition, "_ghostRenderer", ghost);
                Set(transition, "_config", AssetDatabase.LoadAssetAtPath<SpritePoseTransitionConfig>(Root + "Configs/Presentation/CFG_SpritePoseTransition_Default.asset"));
                EnemyMotor2D motor;
                if (charger)
                {
                    var config = Create<DownloadChargeConfig>(name + "_Charge"); config.PlayerLayers = 1 << 6; EditorUtility.SetDirty(config);
                    var m = go.AddComponent<DownloadChargeMotor2D>(); m.Body = body; m.Playfield = playfield; m.Config = config; m.Contact = contact; motor = m;
                    var visual = go.AddComponent<DownloadChargeVisual2D>(); visual.Motor = m; visual.Actor = actor;
                    visual.VisualRoot = renderer.transform; visual.Renderer = renderer; visual.Transition = transition;
                    visual.Idle = sprites[0]; visual.Charge = sprites[1]; visual.Dash = sprites[2];
                    flash.Sources = new[] { renderer, ghost };
                }
                else
                {
                    var m = go.AddComponent<DataCrawlerSnakeMotor2D>(); motor = m;
                    Set(m, "_body", body); Set(m, "_playfield", playfield); Set(m, "_config", motion);
                    var shield = new GameObject("Shield", typeof(BoxCollider2D), typeof(PlayerAttackBlocker2D));
                    shield.layer = 16; shield.transform.SetParent(go.transform, false);
                    var shieldCollider = shield.GetComponent<BoxCollider2D>(); shieldCollider.isTrigger = true; shieldCollider.size = new Vector2(.36f, .85f);
                    var shieldView = new GameObject("Visual", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    shieldView.transform.SetParent(shield.transform, false); shieldView.sprite = sprites[4];
                    shieldView.sharedMaterial = renderer.sharedMaterial; shieldView.sortingLayerID = renderer.sortingLayerID; shieldView.sortingOrder = renderer.sortingOrder + 1;
                    shieldView.transform.localScale = Vector3.one * VisualScale(sprites[4], .9f, true);
                    shield.GetComponent<PlayerAttackBlocker2D>().Renderer = shieldView;
                    var facing = go.AddComponent<SecurityGuardFacing2D>(); facing.Motor = motor;
                    facing.Renderer = renderer; facing.VisualRoot = renderer.transform; facing.ShieldRoot = shield.transform; facing.Transition = transition;
                    shield.transform.localPosition = Vector3.right * facing.ShieldOffset;
                    SetArray(actor, "_simulationStepComponents", new Object[] { facing });
                    flash.Sources = new[] { renderer, ghost, shieldView };
                }
                Set(actor, "_motor", motor); Set(contact, "_motor", motor);
                go.GetComponent<CombatPerceptionBody2D>().TargetValue = charger ? 2.5f : 1.5f;
                var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabRoot + "PF_Enemy_" + name + ".prefab");
                actorAsset = saved.GetComponent<EnemyActor2D>();
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            var runtime = PrefabUtility.LoadPrefabContents(Root + "Prefabs/Combat/Enemies/404Window/PF_EnemyRuntime_404Window.prefab");
            try
            {
                runtime.name = "PF_EnemyRuntime_" + name;
                var pool = runtime.GetComponent<EnemyActorPool2D>(); Set(pool, "_enemyPrefab", actorAsset); Set(pool, "_config", poolConfig);
                var director = runtime.GetComponent<EnemySpawnDirector2D>(); Set(director, "_motionConfig", motion);
                Set(director, "_schedule", schedule); Set(director, "_channel", channel); Set(director, "_autoStart", false);
                var deathPool = runtime.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true).Single(p => p.name == "DeathEffectPool");
                var deathPath = PrefabRoot + "PF_Death_" + name + ".prefab";
                var template = (OneShotSpriteEffect2D)new SerializedObject(deathPool).FindProperty("_effectPrefab").objectReferenceValue;
                var death = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(template));
                try
                {
                    death.GetComponentInChildren<SpriteRenderer>(true).sprite = sprites[charger ? 0 : 3];
                    var deathAsset = PrefabUtility.SaveAsPrefabAsset(death, deathPath);
                    Set(deathPool, "_effectPrefab", deathAsset.GetComponent<OneShotSpriteEffect2D>());
                }
                finally { PrefabUtility.UnloadPrefabContents(death); }
                var effect = Create<OneShotSpriteEffectConfig>(name + "_Death");
                Set(effect, "_durationSeconds", .25f); Set(effect, "_worldDiameter", charger ? 2.5f : 2.3f);
                Set(effect, "_startScaleMultiplier", 1f); Set(effect, "_endScaleMultiplier", .4f);
                Set(effect, "_fadeStart01", 0f); Set(deathPool, "_config", effect);
                return PrefabUtility.SaveAsPrefabAsset(runtime, PrefabRoot + "PF_EnemyRuntime_" + name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(runtime); }
        }

        private static void AddToLevel(LevelSceneBindings bindings, GameObject prefab, string entryId, int firstWaveIndex, int effectId)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bindings.gameObject.scene);
            var pool = instance.GetComponent<EnemyActorPool2D>(); var director = instance.GetComponent<EnemySpawnDirector2D>();
            var actor = (EnemyActor2D)new SerializedObject(pool).FindProperty("_enemyPrefab").objectReferenceValue;
            var manifest = new SerializedObject(bindings.Level.ContentManifest); var entries = manifest.FindProperty("_enemies");
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++);
            entry.FindPropertyRelative("_entryId").stringValue = entryId;
            entry.FindPropertyRelative("_channel").objectReferenceValue = director.Channel;
            entry.FindPropertyRelative("_runtimePrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("_enemyPrefab").objectReferenceValue = actor;
            manifest.ApplyModifiedPropertiesWithoutUndo();
            var data = new SerializedObject(bindings); var list = data.FindProperty("_enemies"); var binding = list.GetArrayElementAtIndex(list.arraySize++);
            binding.FindPropertyRelative("_entryId").stringValue = entryId;
            binding.FindPropertyRelative("_root").objectReferenceValue = instance.transform;
            binding.FindPropertyRelative("_director").objectReferenceValue = director;
            binding.FindPropertyRelative("_pool").objectReferenceValue = pool;
            binding.FindPropertyRelative("_projectilePools").arraySize = 0; data.ApplyModifiedPropertiesWithoutUndo();
            var run = new SerializedObject(bindings.Level.ChapterRunConfig); var segments = run.FindProperty("_segments");
            for (int i = 0; i < segments.arraySize; i++)
            {
                var rules = segments.GetArrayElementAtIndex(i).FindPropertyRelative("_spawnRules"); var rule = rules.GetArrayElementAtIndex(rules.arraySize++);
                rule.FindPropertyRelative("_channel").objectReferenceValue = director.Channel;
                rule.FindPropertyRelative("_enabled").boolValue = i >= firstWaveIndex;
                rule.FindPropertyRelative("_initialDelaySeconds").floatValue = firstWaveIndex == 1 ? 4f : 7f;
                rule.FindPropertyRelative("_intervalMultiplier").floatValue = i == 3 ? .8f : 1f;
                rule.FindPropertyRelative("_maximumAliveCount").intValue = i == 3 ? 3 : 2;
            }
            run.ApplyModifiedPropertiesWithoutUndo();
            foreach (var effect in instance.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
            {
                var network = effect.gameObject.AddComponent<NetworkEffectEventChannel>();
                network.Session = bindings.Session; network.Pool = effect; network.EffectId = (ushort)effectId++;
            }
        }

        private static void ConfigurePlayerBlocking()
        {
            var laser = AssetDatabase.LoadAssetAtPath<HarnessTerminalLaserConfig>(Root + "Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset");
            laser.AttackBlockerLayers = 1 << 16; EditorUtility.SetDirty(laser);
            var melee = AssetDatabase.LoadAssetAtPath<HarnessMeleeConfig>(Root + "Configs/Combat/Harness/Melee/CFG_HA_Melee_Default.asset");
            melee.AttackBlockerLayers = 1 << 16; EditorUtility.SetDirty(melee);
            var rice = AssetDatabase.LoadMainAssetAtPath(Root + "Configs/Combat/DeepSeek/CFG_DS_RiceWeapon_Default.asset");
            var so = new SerializedObject(rice); so.FindProperty("_projectileCollisionLayers").intValue |= 1 << 16; so.ApplyModifiedPropertiesWithoutUndo();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs/Combat/Projectiles" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path);
                try { var projectile = root.GetComponent<RiceProjectile>(); if (projectile != null) { Set(projectile, "_attackBlockerLayers", 1 << 16); PrefabUtility.SaveAsPrefabAsset(root, path); } }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        private static float VisualScale(Sprite sprite, float size, bool vertical)
        {
            var texture = new Texture2D(2, 2); ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            var pixels = texture.GetPixels32(); int low = int.MaxValue, high = 0, transparent = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0) transparent++;
                if (pixels[i].a < 32) continue;
                int coordinate = vertical ? i / texture.width : i % texture.width;
                low = Math.Min(low, coordinate); high = Math.Max(high, coordinate);
            }
            Object.DestroyImmediate(texture);
            if (transparent == 0 || low == int.MaxValue) throw new InvalidOperationException("Missing real alpha in " + sprite.name);
            return size * 512 / (high - low + 1);
        }
        private static T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, ConfigRoot + "CFG_" + name + ".asset"); return asset;
        }
        private static void Set(Object owner, string field, object value)
        {
            var data = new SerializedObject(owner); var p = data.FindProperty(field);
            if (p == null) throw new InvalidOperationException(owner.name + "." + field);
            if (value is Object obj) p.objectReferenceValue = obj;
            else if (value is float f) p.floatValue = f;
            else if (value is int i) p.intValue = i;
            else if (value is bool b) p.boolValue = b;
            else if (value is string s) p.stringValue = s;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner);
        }
        private static void SetArray(Object owner, string field, Object[] values)
        {
            var data = new SerializedObject(owner); var p = data.FindProperty(field); p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
