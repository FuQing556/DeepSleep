using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Setup
{
    public static class KimiChapterInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play and resolve unsaved scene first.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/World01_EarlyInternet.unity");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            if (all.OfType<KimiChapterEncounterDriver2D>().Any()) throw new InvalidOperationException("Kimi already installed; no overwrite.");
            KimiPresentationInstaller.ConfigurePrefab();
            var chapter = all.OfType<ChapterRunController>().Single(); var bindings = chapter.LevelBindings;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KimiContentInstaller.RigPath), scene);
            var encounter = root.GetComponent<KimiEncounter2D>();
            var driver = root.AddComponent<KimiChapterEncounterDriver2D>();
            driver.Chapter = chapter; driver.Encounter = encounter; driver.Session = bindings.Session; driver.SegmentNumber = 4;
            driver.Targets = new[] { bindings.Session.DeepSeek.GetComponent<DamageHitbox2D>(), bindings.Session.Harness.GetComponent<DamageHitbox2D>() };
            driver.Perception = bindings.PerceptionRegistry; driver.Obstacles = all.OfType<CompanionObstacleRegistry2D>().Single();
            driver.Backdrop = all.OfType<FinitePanoramaLayer2D>().Single().GetComponent<SpriteRenderer>();
            driver.NightBackdrop = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Backgrounds/Kimi/BG_W01_MoonRiver_Panorama_v01.png");
            KimiPresentationInstaller.ConfigureChapter(driver);
            var canvas = all.OfType<ChapterRunHudView>().Single().GetComponentInParent<Canvas>();
            var hudRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KimiContentInstaller.HudPath), scene);
            hudRoot.transform.SetParent(canvas.transform, false); driver.Hud = hudRoot.GetComponent<KimiBossHudView>();
            driver.Hud.Boss = encounter.Boss;
            Append(chapter, "_additionalObjectiveComponents", driver);
            Append(chapter.CombatWorld, "ParticipantComponents", driver);
            Append(bindings.SimulationLoop, "worldStepComponents", driver);
            var channel = root.AddComponent<KimiEncounterNetworkChannel>();
            channel.Session = bindings.Session; channel.ChapterDriver = driver; channel.Encounter = encounter;
            channel.BossFlash = encounter.Boss.GetComponent<SpriteHitFlash2D>();
            channel.CurtainFlash = encounter.Ultimate.Curtain.GetComponent<SpriteHitFlash2D>();
            // 复用既有可靠特效事件，生成一次性命中/碎镜/增援特效，不把装饰粒子逐帧复制。
            int effectId = all.OfType<NetworkEffectEventChannel>().Select(e => (int)e.EffectId).DefaultIfEmpty(0).Max();
            foreach (var pool in root.GetComponentsInChildren<OneShotSpriteEffectPool2D>(true))
            {
                var relay = pool.gameObject.AddComponent<NetworkEffectEventChannel>();
                relay.Session = bindings.Session; relay.Pool = pool; relay.EffectId = checked((ushort)++effectId);
            }
            if (!driver.TryValidateConfiguration(out string reason) || !channel.TryValidateConfiguration(out reason)) throw new InvalidOperationException(reason);
            int changes = LevelSceneInstaller.Apply(bindings);
            if (!LevelSceneInstaller.TryValidateDerived(bindings, out reason)) throw new InvalidOperationException(reason);
            var config = new SerializedObject(bindings.Level.ChapterRunConfig);
            var segments = config.FindProperty("_segments");
            if (segments == null || segments.arraySize != 4) throw new InvalidOperationException("Expected four segment config.");
            segments.GetArrayElementAtIndex(3).FindPropertyRelative("_displayName").stringValue = "？？？";
            config.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver.Hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "World01 Kimi saved: one driver fixed-step, chapter/lifecycle/HUD/night/network, special pools/perception/rewards, effect channels; derived changes=" + changes;
        }
        private static void Append(Object owner, string field, Object item)
        {
            var so = new SerializedObject(owner); var list = so.FindProperty(field);
            int index = list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }
}
