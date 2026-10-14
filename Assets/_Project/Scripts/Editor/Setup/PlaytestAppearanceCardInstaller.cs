using System;
using System.Linq;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>仅调整装扮卡片排版与现有美术绑定，不改商品、价格、存档。</summary>
    public static class PlaytestAppearanceCardInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            const string path = "Assets/_Project/Prefabs/UI/Meta/PF_UI_MetaProductCard.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = new SerializedObject(root.GetComponent<MetaProductCardView>());
                T Field<T>(string name) where T : UnityEngine.Object => (T)so.FindProperty(name).objectReferenceValue;
                void Rect(RectTransform rt, float x, float y, float w, float h)
                {
                    rt.anchorMin = rt.anchorMax = Vector2.one * .5f;
                    rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
                }
                void Label(string field, float x, float y, float w, float h, int size)
                {
                    var text = Field<Text>(field); Rect(text.rectTransform, x, y, w, h);
                    text.fontSize = size; text.resizeTextForBestFit = false;
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                    text.verticalOverflow = VerticalWrapMode.Truncate;
                }
                ((RectTransform)root.transform).sizeDelta = new Vector2(1160, 210);
                var layout = root.GetComponent<LayoutElement>(); layout.preferredHeight = 210; layout.minHeight = 210;
                Rect(Field<Image>("_icon").rectTransform, -480, 0, 130, 150);
                Label("_name", -155, 62, 500, 48, 34);
                Label("_description", -155, -25, 500, 100, 26);
                Label("_owned", 180, 48, 170, 42, 26);
                Label("_price", 180, -48, 170, 42, 26);
                var theme = root.GetComponent<UiThemeView>();
                var artwork = theme.Artwork.ToList();
                foreach (string field in new[] { "_purchase", "_equipDeepSeek", "_equipHarness" })
                {
                    var button = Field<Button>(field);
                    Rect((RectTransform)button.transform, 425, field == "_purchase" ? 0 : field == "_equipDeepSeek" ? 46 : -46, 240, 80);
                    var label = button.GetComponentInChildren<Text>(true);
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(12, 4); label.rectTransform.offsetMax = new Vector2(-12, -4);
                    label.fontSize = 28; label.resizeTextForBestFit = false;
                    foreach (var image in button.GetComponentsInChildren<Image>(true))
                    {
                        if (artwork.Any(b => b.Target == image)) continue;
                        if (image.name != "Base" && image.name != "Frame" && image.name != "Ornament" && image.name != "Glow" && image.name != "Highlight") continue;
                        artwork.Add(new UiThemeView.ArtworkBinding { Target = image, Kind = image.name switch
                        {
                            "Base" => UiArtworkKind.ButtonBase, "Frame" => UiArtworkKind.ButtonFrame,
                            "Ornament" => UiArtworkKind.ButtonOrnament, _ => UiArtworkKind.ButtonGlow
                        }});
                    }
                }
                theme.Artwork = artwork.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return "Appearance cards: larger text/touch targets; equipment buttons follow both themes.";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
