using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class MenuInteractionFixInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play first");
            var main = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var so = new SerializedObject(menu);
            if (so.FindProperty("_quitButton").objectReferenceValue == null)
            {
                var template = (Button)so.FindProperty("_achievementsButton").objectReferenceValue;
                var quit = Object.Instantiate(template, template.transform.parent);
                quit.name = "退出游戏";
                quit.onClick = new Button.ButtonClickedEvent();
                quit.GetComponentInChildren<Text>().text = "退出游戏";
                ((RectTransform)quit.transform).anchoredPosition = new Vector2(0, -315);
                so.FindProperty("_quitButton").objectReferenceValue = quit;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay_Prototype.unity");
            int count = 0;
            foreach (var image in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Image>(true)).ToArray())
            {
                if (image.name != "SessionOverlay" && image.name != "ExitConfirmation" && image.name != "ChapterSettlementPanel") continue;
                if (image.transform.Find("FullScreenBackdrop") != null) continue;
                var child = new GameObject("FullScreenBackdrop", typeof(RectTransform), typeof(Image), typeof(FullScreenBackdrop));
                child.transform.SetParent(image.transform, false);
                child.transform.SetAsFirstSibling();
                child.GetComponent<Image>().color = image.color;
                image.enabled = false;
                child.GetComponent<FullScreenBackdrop>().Fit();
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            return "Quit button installed; fullscreen backgrounds: " + count;
        }
    }
}
