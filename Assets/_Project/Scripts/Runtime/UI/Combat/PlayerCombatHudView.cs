using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>无交互的跨端状态面板。依赖显式注入，按显示精度变化更新文字。</summary>
    public sealed class PlayerCombatHudView : MonoBehaviour
    {
        public MonoBehaviour SourceComponent;
        public OpeningCharacterSelectionController Selection;
        public CanvasGroup Panel;
        public Text Header, HealthLabel, StatusLabel, SkillLabel, WeaponLabel;
        public RectTransform HealthFill;
        public Image ProtectionIcon;
        [Header("可替换文案")]
        public string CharacterName, LocalSuffix, CompanionSuffix, HpFormat, AliveText, DownedText,
            RevivingFormat, ProtectionFormat, HitProtectionFormat, SkillName, WeaponName,
            ReadyText, CooldownFormat, ActiveFormat, ChargesFormat, CalibrationFormat, MeleeText;
        private IPlayerCombatHudSource _source;
        private PlayerCombatHudSnapshot _last;
        private bool _hasLast;

        private void Awake()
        {
            _source = SourceComponent as IPlayerCombatHudSource;
            if (_source == null || Selection == null || Panel == null || Header == null || HealthLabel == null ||
                StatusLabel == null || SkillLabel == null || WeaponLabel == null || HealthFill == null || ProtectionIcon == null)
            {
                Debug.LogError("[CombatHUD] 只读数据源和视图引用必须完整装配。", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            Panel.alpha = Selection.IsSelectionComplete ? 1f : 0f;
            if (!Selection.IsSelectionComplete || !_source.TryRead(out var state)) return;
            Render(state);
        }

        public void Render(PlayerCombatHudSnapshot s)
        {
            if (!_hasLast || s.IsLocal != _last.IsLocal)
                Header.text = CharacterName + (s.IsLocal ? LocalSuffix : CompanionSuffix);
            if (!_hasLast || s.Health != _last.Health || s.MaximumHealth != _last.MaximumHealth)
            {
                HealthLabel.text = string.Format(HpFormat, s.Health, s.MaximumHealth);
                HealthFill.anchorMax = new Vector2(s.MaximumHealth > 0 ? Mathf.Clamp01(s.Health / s.MaximumHealth) : 0, 1);
            }
            if (!_hasLast || s.Downed != _last.Downed || s.BeingRevived != _last.BeingRevived ||
                s.ReviveProtection != _last.ReviveProtection || Tenth(s.ProtectionSeconds) != Tenth(_last.ProtectionSeconds) ||
                Mathf.RoundToInt(s.ReviveProgress * 100) != Mathf.RoundToInt(_last.ReviveProgress * 100))
            {
                StatusLabel.text = s.Downed ? s.BeingRevived ? string.Format(RevivingFormat, s.ReviveProgress * 100) : DownedText :
                    s.ProtectionSeconds > 0 ? string.Format(s.ReviveProtection ? ProtectionFormat : HitProtectionFormat,
                        Tenth(s.ProtectionSeconds) * 0.1f) : AliveText;
                ProtectionIcon.enabled = !s.Downed && s.ReviveProtection;
            }
            if (!_hasLast || s.SkillPhase != _last.SkillPhase || s.Charges != _last.Charges ||
                Tenth(s.SkillSeconds) != Tenth(_last.SkillSeconds))
            {
                SkillLabel.text = SkillName + PhaseText(s.SkillPhase, s.SkillSeconds) +
                    (s.SkillPhase == HudActionPhase.Active && s.Charges >= 0 ? string.Format(ChargesFormat, s.Charges) : "");
            }
            if (!_hasLast || s.WeaponPhase != _last.WeaponPhase || Tenth(s.WeaponSeconds) != Tenth(_last.WeaponSeconds))
                WeaponLabel.text = WeaponName + PhaseText(s.WeaponPhase, s.WeaponSeconds);
            _last = s;
            _hasLast = true;
        }

        private string PhaseText(HudActionPhase phase, float seconds) => phase switch
        {
            HudActionPhase.Active => string.Format(ActiveFormat, Tenth(seconds) * 0.1f),
            HudActionPhase.Cooldown => string.Format(CooldownFormat, Tenth(seconds) * 0.1f),
            HudActionPhase.Calibrating => string.Format(CalibrationFormat, Tenth(seconds) * 0.1f),
            HudActionPhase.Melee => MeleeText,
            _ => ReadyText
        };

        // 以0.1秒显示，避免每帧重建文本，也不会把尚未结束的保护显示成0秒。
        private static int Tenth(float value) => Mathf.CeilToInt(Mathf.Max(0, value) * 10);
    }
}
