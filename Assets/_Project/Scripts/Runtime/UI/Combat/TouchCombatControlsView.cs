using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>两角色共用的手机技能反馈与 HS 近战方向摇杆。只读玩法状态，不另跑冷却。</summary>
    public sealed class TouchCombatControlsView : MonoBehaviour
    {
        public TouchCommandSource Input;
        public PlayerCombatHudSource DeepSeek, Harness;
        public TouchCommandPad Skill, Melee, Exit;
        public Image CooldownMask;
        public ClaudePermissionModule2D Permissions;
        public Image MoveSeal, SkillSeal, MeleeSeal;
        public Text SkillLabel;
        public string ReadyText, ActiveText;
        private bool _melee;
        private int _shownSeconds = -1;
        private HudActionPhase _shownPhase;

        private void Awake()
        {
            if (Input == null || DeepSeek == null || Harness == null || Skill == null ||
                Melee == null || Exit == null || CooldownMask == null || SkillLabel == null)
            { Debug.LogError("[TouchCombatControlsView] 手机操作引用缺失。", this); enabled = false; }
        }

        private void LateUpdate()
        {
            var source = Input.Assignment.CurrentLocalPlayerRole == PlayerRole.Harness ? Harness : DeepSeek;
            if (!source.TryRead(out var state)) return;
            bool melee = source == Harness && Harness.Melee.IsMelee && !state.Downed;
            // 只显示权限书，不把救援、入场演出等普通门禁误画成封禁。
            bool moveSealed = false, skillSealed = false;
            if (Permissions != null)
                foreach (var book in Permissions.Books)
                    if (book.State == ClaudeBookState.Sealed && book.Role == source.Role)
                    { moveSealed |= book.Permission == ClaudePermission.Movement; skillSealed |= book.Permission == ClaudePermission.Skill; }
            MoveSeal.enabled = moveSealed;
            SkillSeal.enabled = MeleeSeal.enabled = skillSealed;
            if (_melee != melee)
            {
                Skill.Release(); Melee.Release(); Exit.Release();
                Input.ResetMeleeInput();
                _melee = melee;
            }
            Skill.gameObject.SetActive(!melee);
            Melee.gameObject.SetActive(melee);
            Exit.gameObject.SetActive(melee);
            float duration = source == Harness ? Harness.Melee.Config.CooldownSeconds : DeepSeek.Guard.CooldownDuration;
            bool cooling = state.SkillPhase == HudActionPhase.Cooldown;
            CooldownMask.enabled = cooling;
            CooldownMask.fillAmount = cooling && duration > 0 ? Mathf.Clamp01(state.SkillSeconds / duration) : 0;
            int seconds = Mathf.CeilToInt(state.SkillSeconds);
            if (seconds == _shownSeconds && state.SkillPhase == _shownPhase) return;
            _shownSeconds = seconds; _shownPhase = state.SkillPhase;
            SkillLabel.text = cooling ? seconds.ToString() : state.SkillPhase == HudActionPhase.Active ? ActiveText : ReadyText;
        }

        private void OnDisable()
        {
            if (Input != null) Input.ResetMeleeInput();
        }
    }
}
