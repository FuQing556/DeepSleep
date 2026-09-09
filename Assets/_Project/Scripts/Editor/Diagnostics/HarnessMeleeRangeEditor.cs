using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    [CustomEditor(typeof(HarnessMeleeController))]
    public sealed class HarnessMeleeRangeEditor : UnityEditor.Editor
    {
        private int _previewAttack;
        private bool _showRanges = true;
        private bool _showReferences;

        public override void OnInspectorGUI()
        {
            _showReferences = EditorGUILayout.Foldout(_showReferences, "高级：组件引用", true);
            if (_showReferences) DrawDefaultInspector();
            EditorGUILayout.LabelField("HS 近战调节", EditorStyles.boldLabel);
            _showRanges = EditorGUILayout.Toggle("显示范围", _showRanges);
            _previewAttack = EditorGUILayout.Popup("预览动作", _previewAttack, new[] { "下劈", "上挑", "横劈" });
            var controller = (HarnessMeleeController)target;
            if (controller.Config != null && controller.Config.IsValid)
            {
                var settings = new SerializedObject(controller.Config);
                settings.Update();
                EditorGUILayout.LabelField("待机与测试", EditorStyles.boldLabel);
                Field(settings, "DurationSeconds", "技能持续（秒）");
                Field(settings, "CooldownSeconds", "退出后冷却（秒）");
                Field(settings, "ComboResetSeconds", "收刀后连段重置（秒）");
                Field(settings, "IdlePoseScale", "持剑待机：人物大小");
                Field(settings, "IdlePoseOffset", "持剑待机：人物偏移");
                Field(settings, "IdleSwordLength", "待机小剑长度");
                Field(settings, "IdleSwordAngle", "待机剑角度（-90朝下）");
                Field(settings, "IdleSwordOffset", "小剑相对发射口偏移");
                Field(settings, "WaveFadeSeconds", "剑气淡出（秒）");
                settings.ApplyModifiedProperties();
                var attack = new SerializedObject(controller.Config.Attacks[_previewAttack]);
                attack.Update();
                EditorGUILayout.LabelField("当前选择的这一招", EditorStyles.boldLabel);
                Field(attack, "CharacterScale", "人物大小");
                Field(attack, "CharacterOffset", "人物偏移");
                Field(attack, "SwordLength", "大剑长度（剑柄到剑尖）");
                Field(attack, "SwingSeconds", "挥剑总时长（秒）");
                Field(attack, "MotionKeys", "自由挥剑关键姿势（时间/剑柄/角度/大小）");
                Field(attack, "SwingOffset", "整套剑运动偏移");
                Field(attack, "FollowThroughSeconds", "收势淡出（仅表现，秒）");
                Field(attack, "FollowThroughDrift", "收势剑柄漂移");
                Field(attack, "WaveWidth", "剑气大小");
                Field(attack, "WaveRotation", "剑气倾斜角度");
                Field(attack, "WaveOffset", "剑气中心相对发射口位置");
                Field(attack, "WaveFlipX", "剑气左右翻转");
                Field(attack, "WaveFlipY", "剑气上下翻转");
                attack.ApplyModifiedProperties();
                if (GUILayout.Button("打开完整招式配置")) Selection.activeObject = attack.targetObject;
            }
            EditorGUILayout.HelpBox("黄色点是发射口（坐标基准）。关键姿势独立控制剑柄、角度、大小；剑气不会牵引剑尖。青色是刀刃运动，红色是剑气判定。人物大小不改变碰撞体。配置是资产，Play中修改也会保留。", MessageType.Info);
            if (GUI.changed) SceneView.RepaintAll();
        }

        private static void Field(SerializedObject obj, string property, string label)
            => EditorGUILayout.PropertyField(obj.FindProperty(property), new GUIContent(label), true);

        private void OnSceneGUI()
        {
            var controller = (HarnessMeleeController)target;
            if (!_showRanges || controller.Config == null || !controller.Config.IsValid) return;
            var attack = controller.Config.Attacks[_previewAttack];
            Vector2 origin = controller.GripPosition;
            float aim = controller.IsSwinging ? controller.AimDegrees : 0;
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(origin, Vector3.forward, .055f);
            Handles.Label(origin, "轨迹基准 = LaserOrigin（剑柄可移动）");
            Handles.color = Color.cyan;
            Vector2 previousTip = origin;
            for (int i = 0; i <= 30; i++)
            {
                MeleeSwordGeometry2D.Evaluate(attack, origin, aim, i / 30f, out Vector2 hilt, out Vector2 tip);
                Handles.DrawLine(hilt, tip);
                if (i > 0) Handles.DrawLine(previousTip, tip);
                previousTip = tip;
            }
            Handles.color = new Color(1f, .15f, .25f);
            Vector2 waveOrigin = MeleeSwordGeometry2D.WavePosition(attack, origin, aim);
            Vector3[] points = new Vector3[attack.WavePolygon.Length + 1];
            for (int i = 0; i < attack.WavePolygon.Length; i++)
            {
                points[i] = waveOrigin + MeleeSwordGeometry2D.WaveLocalVector(attack, attack.WavePolygon[i], aim);
            }
            points[points.Length - 1] = points[0];
            Handles.DrawAAPolyLine(3, points);
        }
    }
}
