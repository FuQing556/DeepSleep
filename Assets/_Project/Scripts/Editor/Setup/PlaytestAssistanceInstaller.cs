using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class PlaytestAssistanceInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing edits first.");
            var config = AssetDatabase.LoadAssetAtPath<HarnessTerminalLaserConfig>("Assets/_Project/Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset");
            config.AssistedSelectionHeightFraction = .1f; config.AutomaticScanInterval = .12f;
            EditorUtility.SetDirty(config);
            var network = AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            network.ProtocolVersion = NetworkMessageCatalog.ProtocolVersion; EditorUtility.SetDirty(network);
            AssetDatabase.SaveAssets();
            var active = SceneManager.GetActiveScene();
            foreach (string level in new[] { "MainMenu", "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                string path = "Assets/Scenes/" + level + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    foreach (var laser in roots.SelectMany(r => r.GetComponentsInChildren<HarnessTerminalLaserController>(true)))
                    {
                        laser.Perception = roots.SelectMany(r => r.GetComponentsInChildren<CombatPerceptionRegistry2D>(true)).Single();
                        laser.Permissions = roots.SelectMany(r => r.GetComponentsInChildren<ClaudePermissionModule2D>(true)).SingleOrDefault();
                        laser.EnergyBalls = roots.SelectMany(r => r.GetComponentsInChildren<ClaudeEnergyPattern2D>(true)).ToArray();
                        laser.ReviveChannel = laser.GetComponentInParent<PlayerReviveActionChannel>();
                        if (laser.ReviveChannel == null) throw new InvalidOperationException("HS revive channel not bound.");
                        EditorUtility.SetDirty(laser);
                        if (PrefabUtility.IsPartOfPrefabInstance(laser)) PrefabUtility.RecordPrefabInstancePropertyModifications(laser);
                    }
                    foreach (var settings in roots.SelectMany(r => r.GetComponentsInChildren<AudioSettingsPanel>(true))) InstallSettings(settings, roots);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            return "HS assistance + protocol19 configured; four scenes share Audio / Display / Controls tabs.";
        }

        private static void InstallSettings(AudioSettingsPanel settings, GameObject[] roots)
        {
            var panel = (RectTransform)settings.CloseButton.transform.parent;
            var tabs = panel.GetComponent<SettingsCategoryView>();
            if (tabs != null) return;
            tabs = panel.gameObject.AddComponent<SettingsCategoryView>();
            tabs.AudioPage = Page(panel, "AudioPage"); tabs.DisplayPage = Page(panel, "DisplayPage"); tabs.ControlsPage = Page(panel, "ControlsPage");
            var template = settings.CloseButton;
            var theme = roots.SelectMany(r => r.GetComponentsInChildren<UiThemeView>(true)).First(t => t.DeepSeek != null && t.Harness != null);
            tabs.AudioTab = ButtonCopy(template, panel, "AudioTab", "声音", new Vector2(-265, 238), new Vector2(230, 72), theme);
            tabs.DisplayTab = ButtonCopy(template, panel, "DisplayTab", "画面", new Vector2(0, 238), new Vector2(230, 72), theme);
            tabs.ControlsTab = ButtonCopy(template, panel, "ControlsTab", "操作", new Vector2(265, 238), new Vector2(230, 72), theme);
            Move(settings.MasterSlider.transform.parent, tabs.AudioPage.transform, new Vector2(0, 120));
            Move(settings.SfxSlider.transform.parent, tabs.AudioPage.transform, new Vector2(0, 0));
            Move(settings.AmbienceSlider.transform.parent, tabs.AudioPage.transform, new Vector2(0, -120));
            Move(settings.CloudSlider.transform.parent, tabs.DisplayPage.transform, new Vector2(0, 120));
            foreach (string heading in new[] { "SoundHeading", "DisplayHeading" })
                if (panel.Find(heading) != null) panel.Find(heading).gameObject.SetActive(false);
            if (panel.Find("HitShakeOption") != null) Move(panel.Find("HitShakeOption"), tabs.DisplayPage.transform, new Vector2(-190, -20));
            if (panel.Find("HitFlashOption") != null) Move(panel.Find("HitFlashOption"), tabs.DisplayPage.transform, new Vector2(190, -20));
            var controls = tabs.ControlsPage.AddComponent<ControlSettingsPanel>();
            controls.AutomaticTitle = "HS 摆烂（自动激光）："; controls.AimAssistTitle = "HS 辅助瞄准：";
            controls.OnText = "开"; controls.OffText = "关";
            controls.AutomaticButton = ButtonCopy(template, tabs.ControlsPage.transform, "HarnessAutomatic", "HS 摆烂（自动激光）：关", new Vector2(0, 120), new Vector2(740, 88), theme);
            controls.AimAssistButton = ButtonCopy(template, tabs.ControlsPage.transform, "AimAssist", "HS 辅助瞄准：开", new Vector2(0, 0), new Vector2(740, 88), theme);
            controls.AutomaticLabel = controls.AutomaticButton.GetComponentInChildren<Text>(true);
            controls.AimAssistLabel = controls.AimAssistButton.GetComponentInChildren<Text>(true);
            var hint = panel.Find("Hint").GetComponent<Text>(); hint.text = "设置自动保存 · 手机和电脑共用"; hint.fontSize = 26;
            hint.rectTransform.anchoredPosition = new Vector2(0, -248);
            var title = panel.Find("Title").GetComponent<Text>(); title.text = "设置";
            foreach (var label in tabs.AudioPage.GetComponentsInChildren<Text>(true)) label.fontSize = 30;
            foreach (var label in tabs.DisplayPage.GetComponentsInChildren<Text>(true)) label.fontSize = 30;
            tabs.DisplayPage.SetActive(false); tabs.ControlsPage.SetActive(false);
        }

        private static GameObject Page(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = Vector2.one * .5f; rect.sizeDelta = new Vector2(900, 420);
            return go;
        }
        private static void Move(Transform item, Transform parent, Vector2 position)
        { item.SetParent(parent, false); ((RectTransform)item).anchoredPosition = position; }

        public static Button ButtonCopy(Button template, Transform parent, string name, string label, Vector2 position, Vector2 size, UiThemeView palette)
        {
            var copy = UnityEngine.Object.Instantiate(template.gameObject, parent); copy.name = name;
            var rect = (RectTransform)copy.transform; rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var button = copy.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
            var text = copy.GetComponentInChildren<Text>(true); text.text = label; text.fontSize = 30;
            var theme = copy.GetComponent<UiThemeView>();
            if (theme == null) theme = copy.AddComponent<UiThemeView>();
            theme.DeepSeek = palette.DeepSeek; theme.Harness = palette.Harness;
            theme.Artwork = copy.GetComponentsInChildren<Image>(true).Where(i => i.name is "Base" or "Frame" or "Ornament" or "Glow" or "Highlight")
                .Select(i => new UiThemeView.ArtworkBinding { Target = i, Kind = i.name switch {
                    "Base" => UiArtworkKind.ButtonBase, "Frame" => UiArtworkKind.ButtonFrame,
                    "Ornament" => UiArtworkKind.ButtonOrnament, _ => UiArtworkKind.ButtonGlow } }).ToArray();
            theme.Graphics = new[] { new UiThemeView.GraphicBinding { Target = text, Tone = UiThemeView.GraphicTone.Text } };
            return button;
        }
    }
}
