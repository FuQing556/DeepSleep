using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/Claude/权限书")]
    public sealed class ClaudePermissionConfig : ScriptableObject
    {
        public float PhaseOneHealth, PhaseTwoHealth, WarningSeconds, SealSeconds;
        public string[] RoleLabels, PermissionLabels;
        public string WarningLabel, SealedLabel;

        public bool TryValidate(out string reason)
        {
            if (!float.IsFinite(PhaseOneHealth) || PhaseOneHealth < 1 ||
                !float.IsFinite(PhaseTwoHealth) || PhaseTwoHealth < 1 ||
                !float.IsFinite(WarningSeconds) || WarningSeconds < 0 ||
                !float.IsFinite(SealSeconds) || SealSeconds <= 0 ||
                RoleLabels == null || RoleLabels.Length != 2 ||
                PermissionLabels == null || PermissionLabels.Length != 3 ||
                string.IsNullOrWhiteSpace(WarningLabel) || string.IsNullOrWhiteSpace(SealedLabel))
            { reason = "生命、现实时间窗口及两角色/三权限文案须显式配置。"; return false; }
            foreach (string label in RoleLabels)
                if (string.IsNullOrWhiteSpace(label)) { reason = "角色文案为空。"; return false; }
            foreach (string label in PermissionLabels)
                if (string.IsNullOrWhiteSpace(label)) { reason = "权限文案为空。"; return false; }
            reason = string.Empty; return true;
        }
    }
}
