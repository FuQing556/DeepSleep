using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Progression.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    public sealed class ControlSettingsPanel : MonoBehaviour
    {
        public Button AutomaticButton, AimAssistButton;
        public Text AutomaticLabel, AimAssistLabel;
        public string AutomaticTitle, AimAssistTitle, OnText, OffText;
        public Button EasyModeButton;
        public Text EasyModeLabel;
        public string EasyModeTitle;
        private void OnEnable()
        {
            AutomaticButton.onClick.AddListener(ToggleAutomatic); AimAssistButton.onClick.AddListener(ToggleAssist);
            if (EasyModeButton != null) EasyModeButton.onClick.AddListener(ToggleEasyMode);
            Refresh();
        }
        private void OnDisable()
        { AutomaticButton.onClick.RemoveListener(ToggleAutomatic); AimAssistButton.onClick.RemoveListener(ToggleAssist);
          if (EasyModeButton != null) EasyModeButton.onClick.RemoveListener(ToggleEasyMode); }
        private void ToggleEasyMode() { DifficultyPreferences.SetEasyMode(!DifficultyPreferences.EasyMode); Refresh(); }
        private void ToggleAutomatic() { PlayerControlPreferences.SetHarnessAutoFire(!PlayerControlPreferences.HarnessAutoFire); Refresh(); }
        private void ToggleAssist() { PlayerControlPreferences.SetAimAssist(!PlayerControlPreferences.AimAssist); Refresh(); }
        private void Refresh()
        {
            AutomaticLabel.text = AutomaticTitle + (PlayerControlPreferences.HarnessAutoFire ? OnText : OffText);
            AimAssistLabel.text = AimAssistTitle + (PlayerControlPreferences.AimAssist ? OnText : OffText);
            if (EasyModeLabel != null) EasyModeLabel.text = EasyModeTitle + (DifficultyPreferences.EasyMode ? OnText : OffText);
        }
    }
}
