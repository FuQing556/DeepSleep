using System;
using System.Linq;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class PlaytestCombatMenuInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var active = SceneManager.GetActiveScene();
            foreach (string level in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                string path = "Assets/Scenes/" + level + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    var menu = all.OfType<CoopSessionMenu>().Single();
                    var settings = all.OfType<AudioSettingsPanel>().Single();
                    var tabs = all.OfType<SettingsCategoryView>().Single();
                    var controls = all.OfType<ControlSettingsPanel>().Single();
                    menu.Settings = settings;
                    var group = (RectTransform)menu.PlayGroup.transform;
                    // 局内卡片小于大厅；不能再继承大厅 Body 的大幅上下留白。
                    group.SetParent(menu.MenuCard, false);
                    group.anchorMin = Vector2.zero; group.anchorMax = Vector2.one;
                    group.offsetMin = group.offsetMax = Vector2.zero; group.pivot = Vector2.one * .5f;
                    Place(controls.AutomaticButton.transform, tabs.ControlsPage.transform, 145, 740, 78);
                    Place(controls.AimAssistButton.transform, tabs.ControlsPage.transform, 45, 740, 78);
                    Place(menu.AiButton.transform, tabs.ControlsPage.transform, -55, 740, 78);
                    PlaceTop(menu.AutoBuyButton.transform, menu.PlayGroup.transform, -420, 740, 90);
                    PlaceTop(settings.OpenButton.transform, menu.PlayGroup.transform, -310, 740, 90);
                    settings.OpenButton.GetComponentInChildren<Text>(true).text = "设置";
                    PlaceTop(menu.PlayHint.transform, menu.PlayGroup.transform, -200, 820, 80);
                    menu.PlayHint.alignment = TextAnchor.MiddleCenter;
                    menu.PlayHint.fontSize = 30;
                    foreach (var b in new[] { menu.AiButton, menu.AutoBuyButton, settings.OpenButton, menu.CloseButton, menu.LeaveButton })
                        foreach (var t in b.GetComponentsInChildren<Text>(true)) t.fontSize = 32;
                    EditorUtility.SetDirty(menu);
                    if (PrefabUtility.IsPartOfPrefabInstance(menu)) PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            return "Combat menu: settings/rest auto-buy/resume/leave; AI under Controls; Escape closes nested settings first.";
        }
        private static void Place(Transform item, Transform parent, float y, float width, float height)
        {
            item.SetParent(parent, false);
            var rt = (RectTransform)item;
            rt.anchorMin = rt.anchorMax = Vector2.one * .5f;
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(width, height);
        }
        private static void PlaceTop(Transform item, Transform parent, float y, float width, float height)
        {
            Place(item, parent, y, width, height);
            var rect = (RectTransform)item;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = Vector2.one * .5f;
        }
    }
}
