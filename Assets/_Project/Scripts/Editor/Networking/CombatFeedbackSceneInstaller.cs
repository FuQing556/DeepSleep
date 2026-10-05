using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Networking
{
    public static class CombatFeedbackSceneInstaller
    {
        public static bool IsFeedbackPresentation(Behaviour component) => NetworkAuthorityRules.IsFeedbackPresentation(component);

        public static void Install(Scene scene)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var session = all.OfType<CoopSessionController>().Single();
            var channel = session.GetComponent<NetworkCombatFeedbackChannel>();
            if (channel == null) channel = session.gameObject.AddComponent<NetworkCombatFeedbackChannel>();
            channel.Session = session;
            channel.Rice = session.DeepSeek.GetComponent<RiceProjectilePool>();
            channel.Laser = session.Harness.GetComponent<HarnessTerminalLaserDamageExecutor2D>();
            channel.Melee = session.Harness.GetComponent<HarnessMeleeDamageExecutor2D>();
            channel.Impacts = session.Harness.GetComponent<HarnessLaserHitEffectPresenter2D>();
            channel.Numbers = all.OfType<CombatDamageNumberPresenter2D>().Single();
            if (!channel.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
            var gate = session.GetComponent<NetworkAuthorityGate>();
            NetworkAuthorityRules.Apply(gate);
            channel.Impacts.enabled = channel.Numbers.enabled = true;
            EditorUtility.SetDirty(channel);
            EditorUtility.SetDirty(gate);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("DeepSleep/开发/修复联机命中反馈与跳字")]
        public static void InstallAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式。");
            foreach (string path in new[] { "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try { Install(scene); EditorSceneManager.SaveScene(scene); }
                finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
