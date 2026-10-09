using System;
using System.IO;
using System.Linq;
using DeepSleep.Editor.Networking;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.World.Playfield;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式安装第二世界递归首波；只创建新敌资源，不覆写原有敌人/用户碰撞调参。</summary>
    public static class RecursiveJellyInstaller
    {
        public const string ConfigRoot = "Assets/_Project/Configs/Combat/Enemies/RecursiveJelly/";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Combat/Enemies/RecursiveJelly/";
        public const string ArtPath = "Assets/_Project/Art/Enemies/RecursiveJelly/SPR_EN_RecursiveJelly_Idle_v01.png";
        public const string ScenePath = "Assets/Scenes/World02_2066.unity";
        private static readonly string[] Stages = { "Large", "Medium", "Small" };

        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before installing.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Scene has unsaved user edits. Save it first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "PF_Enemy_Recursive_Large.prefab") != null)
                throw new InvalidOperationException("Recursive content already exists; do not rerun over tuned assets.");
            if (!File.Exists(ArtPath)) throw new InvalidOperationException("Confirmed jelly artwork is missing.");
            Directory.CreateDirectory(ConfigRoot); Directory.CreateDirectory(PrefabRoot); AssetDatabase.Refresh();
            Sprite sprite = Import();
            var texture = new Texture2D(2, 2);
            ImageConversion.LoadImage(texture, File.ReadAllBytes(ArtPath));
            var pixels = texture.GetPixels32(); int left = texture.width, right = 0, transparent = 0;
            for (int i = 0; i < pixels.Length; i++)
            { if (pixels[i].a == 0) transparent++; if (pixels[i].a >= 32) { left = Math.Min(left, i % texture.width); right = Math.Max(right, i % texture.width); } }
            Object.DestroyImmediate(texture);
            if (transparent == 0 || right <= left) throw new InvalidOperationException("Sprite must have real transparent padding.");
            float[] widths = { 2.4f, 1.45f, .85f };
            float[] healths = { 20, 15, 10 };
            int[] capacities = { 3, 12, 24 };
            var configs = new RecursiveJellyConfig[3];
            var pools = new EnemyPoolConfig[3];
            var channels = new EnemySpawnChannelDefinition[3];
            var modules = new GameObject[3];
            var playfield = AssetDatabase.LoadAssetAtPath<CombatPlayfieldConfig>("Assets/_Project/Configs/World/CFG_CombatPlayfield_Default.asset");
            for (int i = 0; i < 3; i++)
            {
                string stage = Stages[i];
                var health = Create<HealthConfig>(stage + "_Health"); Set(health, "_maximumHealth", healths[i]);
                pools[i] = Create<EnemyPoolConfig>(stage + "_Pool");
                Set(pools[i], "_initialCapacity", capacities[i]); Set(pools[i], "_maximumCapacity", capacities[i]);
                configs[i] = Create<RecursiveJellyConfig>(stage + "_Motion");
                var c = configs[i]; c.Speed = .85f; c.RetargetSeconds = .2f; c.DespawnMargin = 1.5f;
                c.StrideDistance = new[] { .42f, .3f, .22f }[i]; c.StrideSeconds = .9f;
                c.ReachPhase01 = .28f; c.PullPhase01 = .42f;
                c.SplitDirections = i == 0 ? new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) }
                    : i == 1 ? new[] { Vector2.left, Vector2.right } : Array.Empty<Vector2>();
                c.SplitOffset = i == 0 ? .42f : .32f; c.SeparationSpeed = i == 0 ? 3 : 2;
                c.SeparationSeconds = .5f; c.BirthProtectionSeconds = .24f;
                c.MoveStretch = .18f; c.HitSquash = .16f;
                c.HitRecoverSeconds = .35f - .04f * i; c.BirthSquash = .2f; c.Opacity = .86f;
                EditorUtility.SetDirty(c);
                channels[i] = Create<EnemySpawnChannelDefinition>(stage + "_Channel"); Set(channels[i], "_displayName", "递归 · " + stage);
                var schedule = Create<EnemySpawnScheduleConfig>(stage + "_Schedule");
                Set(schedule, "_minimumIntervalSeconds", i == 0 ? 4f : 8f); Set(schedule, "_maximumIntervalSeconds", i == 0 ? 5f : 10f);
                Set(schedule, "_maximumAliveCount", i == 0 ? 3 : 1); Set(schedule, "_randomSeed", 206600 + i);
                Set(schedule, "_horizontalSpawnMargin", .6f); Set(schedule, "_verticalPadding", 1.3f);
                var actor = CreateActor(stage, sprite, widths[i] * sprite.pixelsPerUnit / (right - left + 1), widths[i], health, c, playfield);
                modules[i] = CreateModule(stage, actor, pools[i], schedule, c, channels[i], sprite, widths[i]);
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var bindings = all.OfType<LevelSceneBindings>().Single();
                ushort effectId = checked((ushort)(all.OfType<NetworkEffectEventChannel>().Max(e => (int)e.EffectId) + 1));
                var instances = new GameObject[3];
                var runtimePools = new EnemyActorPool2D[3];
                var binders = new RecursiveJellyRuntimeBinder2D[3];
                for (int i = 0; i < 3; i++)
                {
                    instances[i] = (GameObject)PrefabUtility.InstantiatePrefab(modules[i], scene);
                    runtimePools[i] = instances[i].GetComponent<EnemyActorPool2D>();
                    binders[i] = instances[i].GetComponent<RecursiveJellyRuntimeBinder2D>();
                    binders[i].Session = bindings.Session;
                    foreach (var effect in instances[i].GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
                    {
                        var network = effect.gameObject.AddComponent<NetworkEffectEventChannel>();
                        network.Session = bindings.Session; network.Pool = effect; network.EffectId = effectId++;
                    }
                    Register(bindings, modules[i], instances[i], Stages[i], channels[i], i);
                }
                binders[0].Children = runtimePools[1]; binders[1].Children = runtimePools[2];
                binders[0].Large = runtimePools[0]; binders[0].Medium = runtimePools[1]; binders[0].Small = runtimePools[2];
                binders[0].MediumCapacity = pools[1]; binders[0].SmallCapacity = pools[2];
                binders[0].MediumPerLarge = configs[0].SplitDirections.Length; binders[0].SmallPerMedium = configs[1].SplitDirections.Length;
                Set(instances[0].GetComponent<EnemySpawnDirector2D>(), "_spawnAdmission", binders[0]);
                foreach (var binder in binders) { EditorUtility.SetDirty(binder); PrefabUtility.RecordPrefabInstancePropertyModifications(binder); }
                var run = new SerializedObject(bindings.Level.ChapterRunConfig);
                var first = run.FindProperty("_segments").GetArrayElementAtIndex(0);
                first.FindPropertyRelative("_displayName").stringValue = "递归";
                first.FindPropertyRelative("_requiredDefeats").intValue = 13;
                first.FindPropertyRelative("_objectiveMode").enumValueIndex = (int)ChapterObjectiveMode.AllEnemiesAndEncounters;
                first.FindPropertyRelative("_objectiveChannel").objectReferenceValue = null;
                first.FindPropertyRelative("_objectiveLabel").stringValue = "击败递归个体";
                var rules = first.FindPropertyRelative("_spawnRules");
                for (int r = 0; r < rules.arraySize; r++)
                    rules.GetArrayElementAtIndex(r).FindPropertyRelative("_enabled").boolValue =
                        rules.GetArrayElementAtIndex(r).FindPropertyRelative("_channel").objectReferenceValue == channels[0];
                run.ApplyModifiedPropertiesWithoutUndo();
                var doubao = new SerializedObject(all.OfType<DoubaoChapterEncounterDriver2D>().Single());
                var numbers = doubao.FindProperty("_segmentNumbers"); numbers.arraySize = 3;
                for (int n = 0; n < 3; n++) numbers.GetArrayElementAtIndex(n).intValue = n + 2;
                doubao.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(doubao.targetObject);
                LevelSceneInstaller.Apply(bindings, false);
                if (!bindings.TryValidateConfiguration(out string reason) || !LevelSceneInstaller.TryValidateDerived(bindings, out reason))
                    throw new InvalidOperationException(reason);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            var catalog = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            if (!catalog.Entries.Any(e => e.Sprite == sprite))
            {
                uint id = checked(catalog.Entries.Max(e => e.Id) + 1);
                catalog.Entries = catalog.Entries.Concat(new[] { new NetworkSpriteCatalog.Entry { Id = id, Sprite = sprite } }).ToArray();
                EditorUtility.SetDirty(catalog);
            }
            var networkConfig = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            NetworkBuildRevision.Apply(networkConfig); EditorUtility.SetDirty(networkConfig);
            AssetDatabase.SaveAssets();
            return "World02 wave1: only recursion, 45s retained, 13 individual defeats; 1→4→8; other waves/world01 untouched.";
        }

        private static EnemyActor2D CreateActor(string stage, Sprite sprite, float scale, float width,
            HealthConfig health, RecursiveJellyConfig config, CombatPlayfieldConfig playfield)
        {
            var go = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Combat/Enemies/404Window/PF_Enemy_404Window.prefab");
            try
            {
                go.name = "PF_Enemy_Recursive_" + stage; go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                Object.DestroyImmediate(go.GetComponent<EnemyTumbleMotor2D>());
                var actor = go.GetComponent<EnemyActor2D>(); var hp = go.GetComponent<HealthComponent>(); Set(hp, "_config", health);
                var flash = go.GetComponent<SpriteHitFlash2D>(); var renderer = flash.Sources[0];
                renderer.transform.parent.localScale = Vector3.one; renderer.transform.parent.localRotation = Quaternion.identity;
                renderer.transform.localScale = Vector3.one * scale; renderer.transform.localPosition = Vector3.zero;
                renderer.transform.localRotation = Quaternion.identity; renderer.sprite = sprite; renderer.color = new Color(1, 1, 1, config.Opacity);
                flash.Overlay.sprite = sprite;
                Set(flash, "_tint", new Color(1f, .72f, .65f)); Set(flash, "_peakAlpha", .65f);
                var shape = go.GetComponent<BoxCollider2D>(); shape.size = new Vector2(width * .82f, width * .5f);
                shape.offset = new Vector2(0, -width * .075f);
                var jelly = go.AddComponent<RecursiveJelly2D>();
                jelly.Actor = actor; jelly.Health = hp; jelly.BaseHealth = health; jelly.Body = go.GetComponent<Rigidbody2D>();
                jelly.Shape = shape; jelly.Hitbox = go.GetComponent<DamageHitbox2D>(); jelly.Renderer = renderer;
                jelly.Playfield = playfield; jelly.Config = config;
                Set(actor, "_motor", jelly); Set(go.GetComponent<EnemyContactAttack2D>(), "_motor", jelly);
                var contact = go.GetComponent<EnemyContactAttack2D>();
                Set(contact, "_config", AssetDatabase.LoadAssetAtPath<EnemyContactDamageConfig>(ConfigRoot + "CFG_Recursive_ContactDamage.asset"));
                var actorSettings = new SerializedObject(actor);
                var steps = actorSettings.FindProperty("_simulationStepComponents");
                steps.arraySize = 1; steps.GetArrayElementAtIndex(0).objectReferenceValue = contact;
                actorSettings.ApplyModifiedPropertiesWithoutUndo();
                var sense = go.GetComponent<CombatPerceptionBody2D>(); sense.TargetValue = 1; sense.Shape = shape;
                if (!actor.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabRoot + go.name + ".prefab").GetComponent<EnemyActor2D>();
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static GameObject CreateModule(string stage, EnemyActor2D actor, EnemyPoolConfig poolConfig,
            EnemySpawnScheduleConfig schedule, RecursiveJellyConfig motion, EnemySpawnChannelDefinition channel, Sprite sprite, float width)
        {
            var go = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Combat/Enemies/404Window/PF_EnemyRuntime_404Window.prefab");
            try
            {
                go.name = "PF_EnemyRuntime_Recursive_" + stage;
                var pool = go.GetComponent<EnemyActorPool2D>(); Set(pool, "_enemyPrefab", actor); Set(pool, "_config", poolConfig);
                var binder = go.AddComponent<RecursiveJellyRuntimeBinder2D>(); Set(pool, "_runtimeBinder", binder);
                var director = go.GetComponent<EnemySpawnDirector2D>();
                Set(director, "_motionConfig", motion); Set(director, "_schedule", schedule); Set(director, "_channel", channel); Set(director, "_autoStart", false);
                foreach (var effect in go.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
                {
                    bool contact = effect.name == "ContactImpactEffectPool";
                    string effectPath = "Assets/_Project/Art/VFX/Enemies/RecursiveJelly/VFX_EN_RecursiveJelly_" +
                        (contact ? "ContactHit" : "Death") + "_v01.png";
                    var effectSprite = AssetDatabase.LoadAssetAtPath<Sprite>(effectPath);
                    if (effectSprite == null) throw new InvalidOperationException("Import approved effect first: " + effectPath);
                    var original = new SerializedObject(effect).FindProperty("_effectPrefab").objectReferenceValue;
                    var view = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(original));
                    try
                    {
                        view.GetComponentInChildren<SpriteRenderer>(true).sprite = effectSprite;
                        Set(view.GetComponent<OneShotSpriteEffect2D>(), "_effectScaleSettings", contact
                            ? AssetDatabase.LoadAssetAtPath<CombatEffectScaleSettings>("Assets/_Project/Configs/Presentation/CFG_CombatEffectScale_Default.asset") : null);
                        Set(view.GetComponent<OneShotSpriteEffect2D>(), "_effectSource", (int)CombatEffectSource.Enemy);
                        var saved = PrefabUtility.SaveAsPrefabAsset(view, PrefabRoot + "PF_Recursive_" + stage + "_" + effect.name + ".prefab");
                        Set(effect, "_effectPrefab", saved.GetComponent<OneShotSpriteEffect2D>());
                    }
                    finally { PrefabUtility.UnloadPrefabContents(view); }
                    var c = Create<OneShotSpriteEffectConfig>(stage + "_" + effect.name);
                    Set(c, "_worldDiameter", width * (contact ? .7f : 1.25f)); Set(c, "_durationSeconds", contact ? .24f : .36f);
                    Set(c, "_startScaleMultiplier", contact ? .7f : .75f); Set(c, "_endScaleMultiplier", contact ? 1.1f : 1.2f); Set(c, "_fadeStart01", contact ? .05f : .15f);
                    Set(c, "_minimumRotationDegrees", 0f); Set(c, "_maximumRotationDegrees", 0f);
                    Set(c, "_color", new Color(1, 1, 1, contact ? .9f : .85f)); Set(c, "_prewarmCount", 8); Set(c, "_maximumCount", 24);
                    Set(effect, "_config", c);
                }
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabRoot + go.name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static void Register(LevelSceneBindings bindings, GameObject prefab, GameObject instance, string stage, EnemySpawnChannelDefinition channel, int stageIndex)
        {
            string id = "recursive-" + stage.ToLowerInvariant();
            var manifest = new SerializedObject(bindings.Level.ContentManifest); var entries = manifest.FindProperty("_enemies");
            var entry = entries.GetArrayElementAtIndex(entries.arraySize++);
            entry.FindPropertyRelative("_entryId").stringValue = id; entry.FindPropertyRelative("_channel").objectReferenceValue = channel;
            entry.FindPropertyRelative("_runtimePrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("_enemyPrefab").objectReferenceValue = new SerializedObject(instance.GetComponent<EnemyActorPool2D>()).FindProperty("_enemyPrefab").objectReferenceValue;
            manifest.ApplyModifiedPropertiesWithoutUndo();
            var data = new SerializedObject(bindings); var list = data.FindProperty("_enemies"); var binding = list.GetArrayElementAtIndex(list.arraySize++);
            binding.FindPropertyRelative("_entryId").stringValue = id; binding.FindPropertyRelative("_root").objectReferenceValue = instance.transform;
            binding.FindPropertyRelative("_director").objectReferenceValue = instance.GetComponent<EnemySpawnDirector2D>();
            binding.FindPropertyRelative("_pool").objectReferenceValue = instance.GetComponent<EnemyActorPool2D>();
            binding.FindPropertyRelative("_projectilePools").arraySize = 0; data.ApplyModifiedPropertiesWithoutUndo();
            var run = new SerializedObject(bindings.Level.ChapterRunConfig); var segments = run.FindProperty("_segments");
            for (int s = 0; s < segments.arraySize; s++)
            {
                var rules = segments.GetArrayElementAtIndex(s).FindPropertyRelative("_spawnRules"); var rule = rules.GetArrayElementAtIndex(rules.arraySize++);
                rule.FindPropertyRelative("_channel").objectReferenceValue = channel;
                rule.FindPropertyRelative("_enabled").boolValue = stageIndex == 0 && s == 0;
                rule.FindPropertyRelative("_initialDelaySeconds").floatValue = 1;
                rule.FindPropertyRelative("_intervalMultiplier").floatValue = 1;
                rule.FindPropertyRelative("_maximumAliveCount").intValue = stageIndex == 0 ? 3 : 1;
            }
            run.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite Import()
        {
            AssetDatabase.ImportAsset(ArtPath); var i = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            i.textureType = TextureImporterType.Sprite; i.spriteImportMode = SpriteImportMode.Single; i.spritePixelsPerUnit = 512;
            i.spritePivot = new Vector2(.5f, .5f); i.alphaIsTransparency = true; i.mipmapEnabled = false;
            i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 2048; i.textureCompression = TextureImporterCompression.CompressedHQ;
            i.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        }
        private static T Create<T>(string name) where T : ScriptableObject
        { var a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, ConfigRoot + "CFG_Recursive_" + name + ".asset"); return a; }
        private static void Set(Object owner, string field, object value)
        {
            var s = new SerializedObject(owner); var p = s.FindProperty(field);
            if (p == null) throw new InvalidOperationException(owner.name + "." + field);
            if (value == null || value is Object) p.objectReferenceValue = value as Object;
            else if (value is int n) p.intValue = n; else if (value is float f) p.floatValue = f;
            else if (value is bool b) p.boolValue = b; else if (value is string t) p.stringValue = t;
            else if (value is Color c) p.colorValue = c;
            s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner);
            if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }
}
