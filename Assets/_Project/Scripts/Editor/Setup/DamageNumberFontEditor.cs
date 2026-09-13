using DeepSleep.Runtime.Presentation.DamageNumbers;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Setup
{
    public sealed class DamageNumberFontEditor : EditorWindow
    {
        private const string Folder = "Assets/_Project/Configs/Presentation/DamageNumbers/";
        private DamageNumberStyleConfig _ds;
        private DamageNumberStyleConfig _hs;
        private Vector2 _scroll;

        [MenuItem("Tools/DeepSleep/伤害数字编辑器")]
        public static void Open() => GetWindow<DamageNumberFontEditor>("伤害数字编辑器");

        private void OnEnable()
        {
            _ds = AssetDatabase.LoadAssetAtPath<DamageNumberStyleConfig>(Folder + "CFG_DamageNumberStyle_DS.asset");
            _hs = AssetDatabase.LoadAssetAtPath<DamageNumberStyleConfig>(Folder + "CFG_DamageNumberStyle_HS.asset");
            minSize = new Vector2(560f, 600f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("100% 是校准后的默认大小，0% 隐藏伤害数字。两位角色分别调整；不会改变伤害、命中特效或 HUD。修改即时生效，点击保存写入项目。", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawStyle("DS · 冰蓝数字", _ds);
            DrawStyle("HS · 黑红数字", _hs);
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("保存字体设置", GUILayout.Height(30))) AssetDatabase.SaveAssets();
        }

        private void DrawStyle(string title, DamageNumberStyleConfig style)
        {
            if (style == null) { EditorGUILayout.LabelField(title + "：缺少样式配置"); return; }
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var serialized = new SerializedObject(style);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            var size = serialized.FindProperty("_displayScale");
            size.floatValue = EditorGUILayout.Slider("显示大小 (%)", size.floatValue * 100f, 0f, 200f) * 0.01f;
            EditorGUILayout.Slider(serialized.FindProperty("_decimalScale"), 1f, 3f, "小数点大小");
            EditorGUILayout.Slider(serialized.FindProperty("_decimalSpacingPixels"), 0f, 8f, "小数点两侧间距");
            EditorGUILayout.Slider(serialized.FindProperty("_glyphSpacingPixels"), -12f, 8f, "数字间距");
            if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Repaint(); }
            Rect preview = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(preview, new Color(0.13f, 0.23f, 0.36f));
            if (style.DisplayScale <= 0f) GUI.Label(preview, "0% · 伤害数字已隐藏", EditorStyles.centeredGreyMiniLabel);
            else
            {
                DrawNumber(style, "1.1", new Vector2(preview.x + preview.width * .23f, preview.y + 40));
                DrawNumber(style, "8.8", new Vector2(preview.x + preview.width * .73f, preview.y + 40));
                DrawNumber(style, "10.5", new Vector2(preview.x + preview.width * .23f, preview.y + 115));
                DrawNumber(style, "123.4", new Vector2(preview.x + preview.width * .73f, preview.y + 115));
            }
            EditorGUILayout.Space(12);
        }

        private static void DrawNumber(DamageNumberStyleConfig style, string value, Vector2 centre)
        {
            float scale = style.VisualScale * style.DisplayScale;
            float width = 0f;
            for (int i = 0; i < value.Length; i++)
            {
                if (!style.TryGetGlyph(value[i], out Sprite sprite)) continue;
                width += style.GetGlyphHeight(value[i]) * sprite.rect.width / sprite.rect.height;
                if (i > 0) width += style.GetGap(value[i - 1], value[i]);
            }
            float x = centre.x - width * scale * .5f;
            for (int i = 0; i < value.Length; i++)
            {
                if (!style.TryGetGlyph(value[i], out Sprite sprite)) continue;
                float h = style.GetGlyphHeight(value[i]) * scale;
                float w = h * sprite.rect.width / sprite.rect.height;
                Rect uv = sprite.rect;
                uv.x /= sprite.texture.width; uv.width /= sprite.texture.width;
                uv.y /= sprite.texture.height; uv.height /= sprite.texture.height;
                GUI.DrawTextureWithTexCoords(new Rect(x, centre.y - h * .5f - style.GetGlyphOffsetY(value[i]) * scale, w, h), sprite.texture, uv, true);
                x += w;
                if (i + 1 < value.Length) x += style.GetGap(value[i], value[i + 1]) * scale;
            }
        }
    }
}
