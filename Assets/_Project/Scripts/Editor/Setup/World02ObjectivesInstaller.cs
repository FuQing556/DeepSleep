using System;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;

namespace DeepSleep.Editor.Setup
{
    /// <summary>Only updates the approved wave names and objectives; never reinstalls combat content.</summary>
    public static class World02ObjectivesInstaller
    {
        public const string RunPath = "Assets/_Project/Configs/Progression/CFG_ChapterRun_World02_2066.asset";

        [MenuItem("DeepSleep/Setup/Apply World02 Wave Objectives")]
        public static void Install()
        {
            var config = AssetDatabase.LoadAssetAtPath<ChapterRunConfig>(RunPath);
            if (config == null || config.CombatSegmentCount != 4)
                throw new InvalidOperationException("Expected the existing four-wave World02 configuration.");
            string[] names = { "递归调用", "来杯好茶", "销冠王老板", "？？？" };
            string[] labels = { "击败大型递归", "击败快应用", "击败迅雷", "？？？" };
            int[] counts = { 3, 6, 6, 1 };
            string[] paths = {
                "RecursiveJelly/CFG_Recursive_Large_Channel",
                "QuickApp/CFG_QuickApp_Channel",
                "Internet/CFG_Download_Channel"
            };
            var serialized = new SerializedObject(config);
            var segments = serialized.FindProperty("_segments");
            for (int i = 0; i < 4; i++)
            {
                var segment = segments.GetArrayElementAtIndex(i);
                segment.FindPropertyRelative("_displayName").stringValue = names[i];
                segment.FindPropertyRelative("_objectiveLabel").stringValue = labels[i];
                segment.FindPropertyRelative("_requiredDefeats").intValue = counts[i];
                segment.FindPropertyRelative("_objectiveMode").enumValueIndex = (int)(i < 3
                    ? ChapterObjectiveMode.EnemyChannel : ChapterObjectiveMode.EncountersOnly);
                var channel = i < 3 ? AssetDatabase.LoadAssetAtPath<EnemySpawnChannelDefinition>(
                    "Assets/_Project/Configs/Combat/Enemies/" + paths[i] + ".asset") : null;
                if (i < 3 && (channel == null || !config.GetSegment(i + 1).TryGetRule(channel, out var rule) || !rule.Enabled))
                    throw new InvalidOperationException("Objective channel must already be enabled: " + paths[i]);
                segment.FindPropertyRelative("_objectiveChannel").objectReferenceValue = channel;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!config.TryValidate(out var reason)) throw new InvalidOperationException(reason);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            var boss = AssetDatabase.LoadAssetAtPath<ClaudeEncounterConfig>(
                "Assets/_Project/Configs/Combat/Encounters/Claude/CFG_CL_Encounter.asset");
            if (boss == null) throw new InvalidOperationException("Missing Claude encounter config.");
            boss.Title = "权限之外";
            boss.Objective = "击败 Claude";
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssetIfDirty(boss);
        }
    }
}
