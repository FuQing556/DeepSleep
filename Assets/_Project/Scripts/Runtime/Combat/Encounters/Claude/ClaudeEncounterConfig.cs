using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    [CreateAssetMenu(menuName="DeepSleep/Combat/Claude/Encounter",fileName="CFG_CL_Encounter")]
    public sealed class ClaudeEncounterConfig : ScriptableObject
    {
        public BossPresentationTiming Timing;
        public float PhaseChangeSeconds => Timing.PhaseSeconds;
        public float PreludeSeconds, CastGapOne, CastGapTwo, CycleGapSeconds;
        [Tooltip("召书动作持续时间（现实战斗秒），不再与书的预警绑定。")]
        public float PermissionCastSeconds = 1.5f;
        [Tooltip("召书动作结束后的专属休息（现实战斗秒）；替代普通/每5招休息，不叠加。")]
        public float PermissionRecoverySeconds = 5f;
        public int CastsPerCycle;
        public Vector2 EntryPosition;
        public float MoveSpeed, SteeringIntervalSeconds;
        public string Title, Objective;
        public bool TryValidate(out string reason)
        {
            if (Timing == null || !Timing.TryValidate(out reason)) { reason="Claude共用演出配置缺失或无效。"; return false; }
            foreach (float n in new[]{PreludeSeconds,CastGapOne,CastGapTwo,CycleGapSeconds,PhaseChangeSeconds,
                MoveSpeed,SteeringIntervalSeconds,PermissionCastSeconds,PermissionRecoverySeconds})
                if (!float.IsFinite(n) || n<=0) { reason="Claude遭遇节奏必须明确且大于零。"; return false; }
            if (CastsPerCycle<1 || !float.IsFinite(EntryPosition.x) || !float.IsFinite(EntryPosition.y) ||
                string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Objective))
            { reason="Claude轮次、入场位置或任务文本缺失。";return false; }
            reason=string.Empty; return true;
        }
    }
}
