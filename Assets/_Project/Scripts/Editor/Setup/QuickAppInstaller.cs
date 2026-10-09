using System;
using System.IO;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
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
    /// <summary>一次性制作和登记新内容，不重建已有敌人或覆盖用户调参。</summary>
    public static class QuickAppInstaller
    {
        public const string ConfigRoot = "Assets/_Project/Configs/Combat/Enemies/QuickApp/";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Combat/Enemies/QuickApp/";
        public const string ArtPath = "Assets/_Project/Art/Enemies/QuickApp/SPR_EN_QuickApp_Idle_v01.png";
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save user scene edits first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "PF_Enemy_QuickApp.prefab") != null)
                throw new InvalidOperationException("Already installed; do not overwrite tuned assets.");
            Directory.CreateDirectory(ConfigRoot); Directory.CreateDirectory(PrefabRoot); AssetDatabase.Refresh();
            var sprite = Import(ArtPath, new Vector2(.5f, .5f));
            Import("Assets/_Project/Art/VFX/Enemies/QuickApp/VFX_EN_QuickApp_Death_v01.png", new Vector2(.5f, .5f));
            Import("Assets/_Project/Art/VFX/Enemies/QuickApp/VFX_EN_QuickApp_ContactHit_v01.png", new Vector2(.78f, .5f));
            float scale = 1.9f / VisibleDiameter(ArtPath, sprite.pixelsPerUnit);
            var motion = Create<QuickAppMotionConfig>("Motion");
            motion.SpeedRange = new Vector2(7, 9); motion.AmplitudeRange = new Vector2(5.4f, 7.5f);
            motion.WavelengthRange = new Vector2(21, 28); motion.AxisTiltDegrees = 12;
            motion.VisualTiltDegrees = 12; motion.PlayerLayers = 1 << 6; EditorUtility.SetDirty(motion);
            var health = Create<HealthConfig>("Health"); Set(health, "_maximumHealth", 3f);
            var damage = Create<EnemyContactDamageConfig>("ContactDamage");
            Set(damage, "_damageAmount", 1f); Set(damage, "_targetLayers", 1 << 6);
            Set(damage, "_despawnOnImpact", false); Set(damage, "_repeatIntervalSeconds", 1f);
            Set(damage, "_knockbackDistance", 3.6f); Set(damage, "_knockbackSeconds", .24f);
            var capacity = Create<EnemyPoolConfig>("Pool"); Set(capacity, "_initialCapacity", 4); Set(capacity, "_maximumCapacity", 8);
            var schedule = Create<EnemySpawnScheduleConfig>("Schedule");
            Set(schedule, "_initialDelaySeconds", 1f); Set(schedule, "_minimumIntervalSeconds", 4f); Set(schedule, "_maximumIntervalSeconds", 5f);
            Set(schedule, "_maximumAliveCount", 4); Set(schedule, "_spawnFromBothSides", true);
            Set(schedule, "_horizontalSpawnMargin", .6f); Set(schedule, "_verticalPadding", 1f); Set(schedule, "_randomSeed", 206602);
            var channel = Create<EnemySpawnChannelDefinition>("Channel"); Set(channel, "_displayName", "快应用");
            var playfield = AssetDatabase.LoadAssetAtPath<CombatPlayfieldConfig>("Assets/_Project/Configs/World/CFG_CombatPlayfield_Default.asset");
            var actor = CreateActor(sprite, scale, health, damage, motion, playfield);
            var module = CreateModule(actor, capacity, schedule, channel, motion);
            Register(module, actor, channel);
            var catalog = AssetDatabase.LoadAssetAtPath<NetworkSpriteCatalog>("Assets/_Project/Configs/Networking/CFG_NetworkSprites.asset");
            catalog.Entries = catalog.Entries.Concat(new[] { new NetworkSpriteCatalog.Entry { Id = catalog.Entries.Max(e => e.Id) + 1, Sprite = sprite } }).ToArray();
            EditorUtility.SetDirty(catalog);
            var viewPath = "Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab";
            var view = PrefabUtility.LoadPrefabContents(viewPath);
            try
            { var v = view.GetComponent<NetworkEntityView>(); v.WrappingTrailSprite = sprite; v.WrappingPlayfield = playfield; PrefabUtility.SaveAsPrefabAsset(view, viewPath); }
            finally { PrefabUtility.UnloadPrefabContents(view); }
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(network); EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets(); return "QuickApp registered in World02 wave1 alongside recursion; visible diameter 1.9u.";
        }

        private static EnemyActor2D CreateActor(Sprite sprite, float scale, HealthConfig health,
            EnemyContactDamageConfig damage, QuickAppMotionConfig motion, CombatPlayfieldConfig playfield)
        {
            var go = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Combat/Enemies/404Window/PF_Enemy_404Window.prefab");
            try
            {
                go.name = "PF_Enemy_QuickApp"; Object.DestroyImmediate(go.GetComponent<EnemyTumbleMotor2D>());
                var actor = go.GetComponent<EnemyActor2D>(); Set(go.GetComponent<HealthComponent>(), "_config", health);
                var flash = go.GetComponent<SpriteHitFlash2D>(); var renderer = flash.Sources[0];
                renderer.transform.parent.localScale = Vector3.one; renderer.transform.parent.localRotation = Quaternion.identity;
                renderer.transform.localScale = Vector3.one * scale; renderer.transform.localPosition = Vector3.zero;
                renderer.transform.localRotation = Quaternion.identity; renderer.sprite = sprite; renderer.color = Color.white;
                flash.Overlay.sprite = sprite;
                var shape = go.GetComponent<BoxCollider2D>(); shape.size = new Vector2(1.3f, 1.2f); shape.offset = Vector2.zero;
                var motor = go.AddComponent<QuickAppMotor2D>(); motor.Body = go.GetComponent<Rigidbody2D>(); motor.Shape = shape;
                motor.Playfield = playfield; motor.Config = motion; motor.Contact = go.GetComponent<EnemyContactAttack2D>();
                Set(actor, "_motor", motor); Set(motor.Contact, "_motor", motor); Set(motor.Contact, "_config", damage);
                var settings = new SerializedObject(actor); var steps = settings.FindProperty("_simulationStepComponents");
                steps.arraySize = 1; steps.GetArrayElementAtIndex(0).objectReferenceValue = motor.Contact; settings.ApplyModifiedPropertiesWithoutUndo();
                var perception = go.GetComponent<CombatPerceptionBody2D>(); perception.QuickApp = motor; perception.Shape = shape; perception.TargetValue = 16;
                var visual = go.AddComponent<QuickAppVisual2D>(); visual.Actor = actor; visual.Motor = motor; visual.Renderer = renderer;
                visual.Group = go.GetComponent<UnityEngine.Rendering.SortingGroup>();
                visual.MotionTrailPrefab = AssetDatabase.LoadAssetAtPath<SpriteMotionTrail2D>("Assets/_Project/Prefabs/Combat/Enemies/Internet/PF_Download_MotionTrail.prefab");
                if (!actor.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabRoot + go.name + ".prefab").GetComponent<EnemyActor2D>();
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static GameObject CreateModule(EnemyActor2D actor, EnemyPoolConfig capacity, EnemySpawnScheduleConfig schedule,
            EnemySpawnChannelDefinition channel, QuickAppMotionConfig motion)
        {
            var go = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Combat/Enemies/404Window/PF_EnemyRuntime_404Window.prefab");
            try
            {
                go.name = "PF_EnemyRuntime_QuickApp";
                var pool = go.GetComponent<EnemyActorPool2D>(); Set(pool, "_enemyPrefab", actor); Set(pool, "_config", capacity);
                var director = go.GetComponent<EnemySpawnDirector2D>(); Set(director, "_motionConfig", motion);
                Set(director, "_schedule", schedule); Set(director, "_channel", channel); Set(director, "_autoStart", false);
                foreach (var effect in go.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
                {
                    bool contact = effect.name == "ContactImpactEffectPool";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/VFX/Enemies/QuickApp/VFX_EN_QuickApp_" + (contact ? "ContactHit" : "Death") + "_v01.png");
                    var original = new SerializedObject(effect).FindProperty("_effectPrefab").objectReferenceValue;
                    var view = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(original));
                    try
                    {
                        view.GetComponentInChildren<SpriteRenderer>(true).sprite = sprite;
                        Set(view.GetComponent<OneShotSpriteEffect2D>(), "_effectScaleSettings", contact ?
                            AssetDatabase.LoadAssetAtPath<CombatEffectScaleSettings>("Assets/_Project/Configs/Presentation/CFG_CombatEffectScale_Default.asset") : null);
                        Set(view.GetComponent<OneShotSpriteEffect2D>(), "_effectSource", (int)CombatEffectSource.Enemy);
                        var saved = PrefabUtility.SaveAsPrefabAsset(view, PrefabRoot + "PF_QuickApp_" + effect.name + ".prefab");
                        Set(effect, "_effectPrefab", saved.GetComponent<OneShotSpriteEffect2D>());
                    }
                    finally { PrefabUtility.UnloadPrefabContents(view); }
                    var c = Create<OneShotSpriteEffectConfig>(effect.name);
                    Set(c, "_worldDiameter", contact ? 1.25f : 2.4f); Set(c, "_durationSeconds", contact ? .24f : .36f);
                    Set(c, "_startScaleMultiplier", .75f); Set(c, "_endScaleMultiplier", contact ? 1.1f : 1.2f);
                    Set(c, "_fadeStart01", .1f); Set(c, "_minimumRotationDegrees", 0f); Set(c, "_maximumRotationDegrees", 0f);
                    Set(c, "_color", new Color(1, 1, 1, .9f)); Set(c, "_prewarmCount", 4); Set(c, "_maximumCount", 12);
                    Set(effect, "_config", c);
                }
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabRoot + go.name + ".prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        private static void Register(GameObject module, EnemyActor2D actor, EnemySpawnChannelDefinition channel)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/World02_2066.unity", OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var bindings = all.OfType<LevelSceneBindings>().Single();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(module, scene);
                ushort effectId = checked((ushort)(all.OfType<NetworkEffectEventChannel>().Max(e => (int)e.EffectId) + 1));
                foreach (var effect in instance.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
                { var network = effect.gameObject.AddComponent<NetworkEffectEventChannel>(); network.Session = bindings.Session; network.Pool = effect; network.EffectId = effectId++; }
                var manifest = new SerializedObject(bindings.Level.ContentManifest); var entries = manifest.FindProperty("_enemies");
                var entry = entries.GetArrayElementAtIndex(entries.arraySize++);
                entry.FindPropertyRelative("_entryId").stringValue = "quick-app"; entry.FindPropertyRelative("_channel").objectReferenceValue = channel;
                entry.FindPropertyRelative("_runtimePrefab").objectReferenceValue = module; entry.FindPropertyRelative("_enemyPrefab").objectReferenceValue = actor;
                manifest.ApplyModifiedPropertiesWithoutUndo();
                var data = new SerializedObject(bindings); var list = data.FindProperty("_enemies"); var binding = list.GetArrayElementAtIndex(list.arraySize++);
                binding.FindPropertyRelative("_entryId").stringValue = "quick-app"; binding.FindPropertyRelative("_root").objectReferenceValue = instance.transform;
                binding.FindPropertyRelative("_director").objectReferenceValue = instance.GetComponent<EnemySpawnDirector2D>();
                binding.FindPropertyRelative("_pool").objectReferenceValue = instance.GetComponent<EnemyActorPool2D>();
                binding.FindPropertyRelative("_projectilePools").arraySize = 0; data.ApplyModifiedPropertiesWithoutUndo();
                var run = new SerializedObject(bindings.Level.ChapterRunConfig); var segments = run.FindProperty("_segments");
                for (int s = 0; s < segments.arraySize; s++)
                {
                    var rules = segments.GetArrayElementAtIndex(s).FindPropertyRelative("_spawnRules"); var rule = rules.GetArrayElementAtIndex(rules.arraySize++);
                    rule.FindPropertyRelative("_channel").objectReferenceValue = channel; rule.FindPropertyRelative("_enabled").boolValue = s == 0;
                    rule.FindPropertyRelative("_initialDelaySeconds").floatValue = 1; rule.FindPropertyRelative("_intervalMultiplier").floatValue = 1;
                    rule.FindPropertyRelative("_maximumAliveCount").intValue = 4;
                    if (s == 0) segments.GetArrayElementAtIndex(s).FindPropertyRelative("_objectiveLabel").stringValue = "击败敌人";
                }
                run.ApplyModifiedPropertiesWithoutUndo(); LevelSceneInstaller.Apply(bindings, false);
                if (!bindings.TryValidateConfiguration(out string reason) || !LevelSceneInstaller.TryValidateDerived(bindings, out reason)) throw new InvalidOperationException(reason);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        private static Sprite Import(string path, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path); var i = (TextureImporter)AssetImporter.GetAtPath(path);
            i.textureType = TextureImporterType.Sprite; i.spriteImportMode = SpriteImportMode.Single; i.spritePixelsPerUnit = 512;
            var settings = new TextureImporterSettings(); i.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot; i.SetTextureSettings(settings); i.alphaIsTransparency = true; i.mipmapEnabled = false;
            i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 2048; i.textureCompression = TextureImporterCompression.CompressedHQ;
            i.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        public static float VisibleDiameter(string path, float pixelsPerUnit)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                ImageConversion.LoadImage(texture, File.ReadAllBytes(path)); var pixels = texture.GetPixels32();
                int left = texture.width, right = 0, bottom = texture.height, top = 0;
                for (int i = 0; i < pixels.Length; i++) if (pixels[i].a >= 32)
                { left = Math.Min(left, i % texture.width); right = Math.Max(right, i % texture.width); bottom = Math.Min(bottom, i / texture.width); top = Math.Max(top, i / texture.width); }
                return Mathf.Max(right - left + 1, top - bottom + 1) / pixelsPerUnit;
            }
            finally { Object.DestroyImmediate(texture); }
        }
        private static T Create<T>(string name) where T : ScriptableObject
        { var a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, ConfigRoot + "CFG_QuickApp_" + name + ".asset"); return a; }
        private static void Set(Object owner, string field, object value)
        {
            var s = new SerializedObject(owner); var p = s.FindProperty(field);
            if (p == null) throw new InvalidOperationException(owner.name + "." + field);
            if (value == null || value is Object) p.objectReferenceValue = value as Object;
            else if (value is int n) p.intValue = n; else if (value is float f) p.floatValue = f;
            else if (value is bool b) p.boolValue = b; else if (value is Color c) p.colorValue = c; else if (value is string t) p.stringValue = t;
            s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner);
        }
    }
}
