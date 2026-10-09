using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class AutoBuyBuffInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            int count = 0;
            foreach (string path in new[] { "Assets/Scenes/Gameplay_Prototype.unity", "Assets/Scenes/World01_EarlyInternet.unity", "Assets/Scenes/World02_2066.unity" })
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var menu = roots.SelectMany(r => r.GetComponentsInChildren<CoopSessionMenu>(true)).Single();
                    menu.Upgrades = roots.SelectMany(r => r.GetComponentsInChildren<RestNodeUpgradeController>(true)).Single();
                    if (menu.AutoBuyButton == null)
                    {
                        var source = menu.AiButton;
                        var button = UnityEngine.Object.Instantiate(source, source.transform.parent);
                        button.name = "AutoBuyBuffButton";
                        button.onClick = new Button.ButtonClickedEvent();
                        foreach (var label in button.GetComponentsInChildren<Text>(true)) label.text = "自动购买 Buff";
                        var rect = (RectTransform)button.transform;
                        rect.anchoredPosition = ((RectTransform)source.transform).anchoredPosition + new Vector2(0, -rect.sizeDelta.y - 16);
                        var theme = menu.GetComponent<UiThemeView>();
                        var artwork = new List<UiThemeView.ArtworkBinding>(theme.Artwork);
                        foreach (var binding in theme.Artwork.Where(b => b.Target.transform.IsChildOf(source.transform)))
                        {
                            var copy = binding;
                            string relative = AnimationUtility.CalculateTransformPath(binding.Target.transform, source.transform);
                            copy.Target = (relative.Length == 0 ? button.transform : button.transform.Find(relative)).GetComponent<Image>();
                            artwork.Add(copy);
                        }
                        var graphics = new List<UiThemeView.GraphicBinding>(theme.Graphics);
                        foreach (var binding in theme.Graphics.Where(b => b.Target.transform.IsChildOf(source.transform)))
                        {
                            var copy = binding;
                            string relative = AnimationUtility.CalculateTransformPath(binding.Target.transform, source.transform);
                            copy.Target = (relative.Length == 0 ? button.transform : button.transform.Find(relative)).GetComponent<Graphic>();
                            graphics.Add(copy);
                        }
                        theme.Artwork = artwork.ToArray(); theme.Graphics = graphics.ToArray();
                        EditorUtility.SetDirty(theme);
                        menu.AutoBuyButton = button;
                    }
                    var autoRect = (RectTransform)menu.AutoBuyButton.transform;
                    autoRect.anchoredPosition = ((RectTransform)menu.AiButton.transform).anchoredPosition + new Vector2(0, -autoRect.sizeDelta.y - 16);
                    menu.AutoBuyButton.gameObject.SetActive(false);
                    EditorUtility.SetDirty(menu);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    count++;
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            return "Auto-buy installed in " + count + " gameplay scenes.";
        }
    }
}
