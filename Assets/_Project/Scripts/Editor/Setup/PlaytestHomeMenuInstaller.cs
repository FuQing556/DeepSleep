using System;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式整理主页与装扮入口，保留原商店/背包业务、主题素材和存档。</summary>
    public static class PlaytestHomeMenuInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing edits first.");
            const string path = "Assets/Scenes/MainMenu.unity";
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var menu = all.OfType<MainMenuController>().Single();
                var so = new SerializedObject(menu);
                Button Button(string field) => (Button)so.FindProperty(field).objectReferenceValue;
                GameObject Page(string field) => (GameObject)so.FindProperty(field).objectReferenceValue;
                var home = Page("_home").transform;
                var palette = all.OfType<UiThemeView>().First(t => t.DeepSeek != null && t.Harness != null);
                var appearance = Button("_appearanceButton");
                if (appearance == null)
                {
                    appearance = PlaytestAssistanceInstaller.ButtonCopy(Button("_startGame"), home,
                        "Appearance", "装扮", new Vector2(-455, -200), new Vector2(510, 100), palette);
                    so.FindProperty("_appearanceButton").objectReferenceValue = appearance;
                }
                Set(Button("_startGame"), home, "开始游戏", new Vector2(-455, 60), new Vector2(240, 80), 32);
                Set(Button("_bestiaryButton"), home, "图鉴", new Vector2(-590, -65), new Vector2(240, 80), 32);
                Set(appearance, home, "装扮", new Vector2(-320, -65), new Vector2(240, 80), 32);
                Set(Button("_achievementsButton"), home, "成就", new Vector2(-590, -175), new Vector2(240, 80), 32);
                Set(Button("_quitButton"), home, "退出游戏", new Vector2(-455, -285), new Vector2(240, 80), 32);
                var settings = all.OfType<Button>().Single(b => b.name == "AudioSettingsButton");
                Set(settings, home, "设置", new Vector2(-320, -175), new Vector2(240, 80), 32);

                var tabs = Page("_appearanceTabs");
                if (tabs == null)
                {
                    tabs = new GameObject("AppearanceTabs", typeof(RectTransform));
                    var rt = (RectTransform)tabs.transform;
                    rt.SetParent(home.parent, false); rt.anchorMin = rt.anchorMax = Vector2.one * .5f;
                    rt.sizeDelta = new Vector2(1100, 760);
                    so.FindProperty("_appearanceTabs").objectReferenceValue = tabs;
                }
                Set(Button("_inventoryButton"), tabs.transform, "我的装扮", new Vector2(-240, 235), new Vector2(430, 80), 32);
                Set(Button("_shopButton"), tabs.transform, "鲸元券商店", new Vector2(240, 235), new Vector2(430, 80), 32);
                foreach (string field in new[] { "_shop", "_inventory" })
                {
                    var page = Page(field);
                    var scroll = (RectTransform)page.transform.Find("ListScroll");
                    scroll.anchoredPosition = new Vector2(0, -45); scroll.sizeDelta = new Vector2(1200, 460);
                    foreach (var text in page.GetComponentsInChildren<Text>(true))
                        if (text.rectTransform.anchoredPosition.y > 300 && text.rectTransform.anchoredPosition.x < 0)
                            text.text = "装扮";
                }
                tabs.SetActive(false);
                // 页签置于各页面之上，设置遮罩与返回仍处于最高层。
                tabs.transform.SetSiblingIndex(Page("_inventory").transform.GetSiblingIndex() + 1);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return "Home four rows: Start; Bestiary/Appearance; Achievements/Settings; Quit. Shop/inventory share appearance tabs.";
            }
            finally
            {
                if (!loaded) EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(active);
            }
        }
        private static void Set(Button button, Transform parent, string text, Vector2 position, Vector2 size, int font)
        {
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = button.GetComponentInChildren<Text>(true); label.text = text; label.fontSize = font;
            var arrow = button.transform.Find("Arrow") as RectTransform;
            if (arrow != null)
            {
                arrow.anchorMin = arrow.anchorMax = new Vector2(1, .5f);
                arrow.anchoredPosition = new Vector2(-42, 0);
                arrow.gameObject.SetActive(size.x >= 400);
            }
        }
    }
}
