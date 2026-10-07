using DeepSleep.Runtime.Presentation.Accessories;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Accessories
{
    /// <summary>编辑已有饰品配置，不创建场景物体，不修改人物素材或碰撞体。</summary>
    public sealed class AccessoryEditorWindow : EditorWindow
    {
        [SerializeField] private HeadwearDefinition _definition;
        [SerializeField] private int _poseIndex;
        [SerializeField] private HeadwearDefinition _previewCompanion;
        private Vector2 _scroll;
        private bool _dragging;
        private Vector2 _startMouse, _startAnchor;
        private static readonly string[] LayerNames = { "前饰（角色前方）", "背饰（角色后方）" };

        [MenuItem("DeepSleep/饰品编辑器")]
        public static void Open()
        {
            var window = GetWindow<AccessoryEditorWindow>("饰品编辑器");
            window.minSize = new Vector2(780, 600);
            if (Selection.activeObject is HeadwearDefinition selected) window._definition = selected;
            if (window._definition == null)
                window._definition = AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(
                    "Assets/_Project/Configs/Progression/Meta/CFG_Headwear_LittleCrown.asset");
            window.Show();
        }

        public static void Open(HeadwearDefinition definition)
        {
            Open();
            GetWindow<AccessoryEditorWindow>()._definition = definition;
        }

        private void OnEnable() { Undo.undoRedoPerformed += Repaint; }
        private void OnDisable() { Undo.undoRedoPerformed -= Repaint; }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var next = (HeadwearDefinition)EditorGUILayout.ObjectField("饰品配置", _definition, typeof(HeadwearDefinition), false);
                if (next != _definition) { _definition = next; _poseIndex = 0; _dragging = false; }
                if (_definition == null) { EditorGUILayout.HelpBox("选择一个饰品配置后开始调整。", MessageType.Info); return; }
                _previewCompanion = (HeadwearDefinition)EditorGUILayout.ObjectField("叠加预览（不修改它）", _previewCompanion, typeof(HeadwearDefinition), false);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(EditorUtility.IsDirty(_definition) ? "● 有未保存的修改" : "已保存", GUILayout.Width(180));
                if (GUILayout.Button("保存全部姿态", GUILayout.Width(140))) AssetDatabase.SaveAssetIfDirty(_definition);
                if (GUILayout.Button("定位配置", GUILayout.Width(100))) EditorGUIUtility.PingObject(_definition);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("左侧选动作；拖动右侧预览移动饰品，滚轮缩放。每个动作独立保存，可 Ctrl+Z 撤销。前/背分类对整个饰品生效。", MessageType.None);
                EditorGUI.BeginChangeCheck();
                int layer = EditorGUILayout.Popup("显示分类", (int)_definition.Layer, LayerNames);
                if (EditorGUI.EndChangeCheck())
                { Undo.RecordObject(_definition, "饰品前后层级"); _definition.Layer = (AccessoryLayer)layer; EditorUtility.SetDirty(_definition); }

                if (_definition.Poses == null || _definition.Poses.Length == 0)
                { EditorGUILayout.HelpBox("该配置还没有角色动作，请在 Inspector 的 Poses 中添加角色 Sprite。", MessageType.Warning); return; }
                _poseIndex = Mathf.Clamp(_poseIndex, 0, _definition.Poses.Length - 1);
                EditorGUILayout.BeginHorizontal();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Width(225));
                for (int i = 0; i < _definition.Poses.Length; i++)
                {
                    var entry = _definition.Poses[i];
                    var row = GUILayoutUtility.GetRect(205, 83);
                    if (GUI.Button(row, GUIContent.none)) { _poseIndex = i; _dragging = false; GUI.FocusControl(null); }
                    if (i == _poseIndex) EditorGUI.DrawRect(new Rect(row.x, row.y, 4, row.height), new Color(.2f,.8f,1));
                    if (entry.Pose != null) DrawSprite(Fit(new Rect(row.x + 5, row.y + 3, 72, 72), entry.Pose.rect.size), entry.Pose);
                    GUI.Label(new Rect(row.x + 82, row.y + 8, 120, 66), i + " · " + entry.Role + "\n" + PoseLabel(entry.Pose), EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.BeginVertical();
                var pose = _definition.Poses[_poseIndex];
                EditorGUILayout.LabelField(PoseLabel(pose.Pose), EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var anchor = EditorGUILayout.Vector2Field("位置（归一化）", pose.Anchor);
                float width = Mathf.Max(.001f, EditorGUILayout.FloatField("大小（等比）", pose.WidthRatio));
                float angle = EditorGUILayout.Slider("旋转", pose.Angle, -180, 180);
                if (EditorGUI.EndChangeCheck()) { pose.Anchor = anchor; pose.WidthRatio = width; pose.Angle = angle; WritePose(pose, "调整饰品姿态"); }
                var viewport = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawPreview(viewport, pose);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("请先退出播放模式再保存正式饰品配置。", MessageType.Warning);
        }

        private void WritePose(HeadwearPose pose, string undoName)
        {
            Undo.RecordObject(_definition, undoName);
            _definition.Poses[_poseIndex] = pose;
            EditorUtility.SetDirty(_definition);
            Repaint();
        }

        private void DrawPreview(Rect viewport, HeadwearPose pose)
        {
            EditorGUI.DrawRect(viewport, new Color(.09f,.13f,.18f));
            if (pose.Pose == null || _definition.Sprite == null) return;
            var character = Fit(new Rect(viewport.x + 35, viewport.y + 35, Mathf.Max(1,viewport.width - 70), Mathf.Max(1,viewport.height - 70)), pose.Pose.rect.size);
            var anchor = new Vector2(character.x + character.width * pose.Anchor.x, character.yMax - character.height * pose.Anchor.y);
            float width = character.width * pose.WidthRatio;
            float height = width * _definition.Sprite.rect.height / _definition.Sprite.rect.width;
            var ornament = new Rect(anchor.x - width * _definition.BaseAnchor.x, anchor.y - height * (1 - _definition.BaseAnchor.y), width, height);
            GUI.BeginClip(viewport);
            character.position -= viewport.position;
            ornament.position -= viewport.position;
            Vector2 pivot = anchor - viewport.position;
            DrawCompanion(character, pose.Pose, AccessoryLayer.Back);
            if (_definition.Layer == AccessoryLayer.Back) DrawOrnament(ornament, pivot, pose.Angle);
            DrawSprite(character, pose.Pose);
            if (_definition.Layer == AccessoryLayer.Front) DrawOrnament(ornament, pivot, pose.Angle);
            DrawCompanion(character, pose.Pose, AccessoryLayer.Front);
            EditorGUI.DrawRect(new Rect(pivot.x - 7, pivot.y - 1, 14, 2), Color.cyan);
            EditorGUI.DrawRect(new Rect(pivot.x - 1, pivot.y - 7, 2, 14), Color.cyan);
            GUI.EndClip();

            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (!GUI.enabled) return;
            if (e.type == EventType.MouseDown && e.button == 0 && viewport.Contains(e.mousePosition))
            { _dragging = true; _startMouse = e.mousePosition; _startAnchor = pose.Anchor; GUIUtility.hotControl = control; Undo.IncrementCurrentGroup(); e.Use(); }
            else if (e.type == EventType.MouseDrag && _dragging && GUIUtility.hotControl == control)
            {
                Vector2 delta = e.mousePosition - _startMouse;
                pose.Anchor = _startAnchor + new Vector2(delta.x / character.width, -delta.y / character.height);
                WritePose(pose, "拖动饰品"); e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragging)
            { _dragging = false; GUIUtility.hotControl = 0; e.Use(); }
            else if (e.type == EventType.ScrollWheel && viewport.Contains(e.mousePosition))
            { pose.WidthRatio = Mathf.Clamp(pose.WidthRatio * Mathf.Exp(-e.delta.y * .035f), .001f, 10); WritePose(pose, "缩放饰品"); e.Use(); }
        }

        private void DrawOrnament(Rect rect, Vector2 pivot, float angle)
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-angle, pivot);
            DrawSprite(rect, _definition.Sprite);
            GUI.matrix = matrix;
        }

        private void DrawCompanion(Rect character, Sprite poseSprite, AccessoryLayer layer)
        {
            var item = _previewCompanion;
            if (item == null || item == _definition || item.Layer != layer || item.Sprite == null || !item.TryGetPose(poseSprite, out var pose)) return;
            Vector2 anchor = new Vector2(character.x + character.width * pose.Anchor.x, character.yMax - character.height * pose.Anchor.y);
            float width = character.width * pose.WidthRatio;
            float height = width * item.Sprite.rect.height / item.Sprite.rect.width;
            var rect = new Rect(anchor.x - width * item.BaseAnchor.x, anchor.y - height * (1-item.BaseAnchor.y), width, height);
            var matrix = GUI.matrix; GUIUtility.RotateAroundPivot(-pose.Angle, anchor);
            DrawSprite(rect, item.Sprite); GUI.matrix = matrix;
        }

        private static Rect Fit(Rect area, Vector2 size)
        {
            float scale = Mathf.Min(area.width / size.x, area.height / size.y);
            Vector2 fitted = size * scale;
            return new Rect(area.center - fitted * .5f, fitted);
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            // 正式角色与饰品均为单图 Sprite；按源矩形保留透明边，不按可见像素裁剪。
            var uv = sprite.rect;
            uv.x /= sprite.texture.width; uv.width /= sprite.texture.width;
            uv.y /= sprite.texture.height; uv.height /= sprite.texture.height;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        }

        private static string PoseLabel(Sprite sprite)
        {
            if (sprite == null) return "未指定贴图";
            string name = sprite.name;
            if (name.Contains("Downed")) return "倒地";
            if (name.Contains("LaserFire")) return "激光开火";
            if (name.Contains("MeleeDown")) return "近战 · 下劈";
            if (name.Contains("MeleeUp")) return "近战 · 上挑";
            if (name.Contains("MeleeSweep")) return "近战 · 横扫";
            if (name.Contains("MeleeIdle")) return "近战 · 待机";
            return "普通姿态";
        }
    }

    [CustomEditor(typeof(HeadwearDefinition))]
    public sealed class HeadwearDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("打开饰品可视化编辑器", GUILayout.Height(30)))
                AccessoryEditorWindow.Open((HeadwearDefinition)target);
            DrawDefaultInspector();
        }
    }
}
