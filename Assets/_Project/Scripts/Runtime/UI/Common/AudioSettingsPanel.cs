using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>共用设置面板（保留原脚本资产身份）；音量和云密度仅保存在本机。</summary>
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        public CanvasGroup Panel;
        public Slider MasterSlider, SfxSlider, AmbienceSlider;
        public Text MasterValue, SfxValue, AmbienceValue;
        public Button OpenButton, CloseButton;
        public Slider CloudSlider;
        public Text CloudValue;

        private bool _dirty;
        private float _saveAt;
        private bool _ready;
        private const float SaveDelay = .4f;
        public bool IsOpen => Panel != null && Panel.interactable;
        private GameAudioService Audio => GameAppRoot.Instance != null ? GameAppRoot.Instance.Audio : null;

        private void Awake()
        {
            if (Panel == null || MasterSlider == null || SfxSlider == null || AmbienceSlider == null ||
                MasterSlider == SfxSlider || MasterSlider == AmbienceSlider || SfxSlider == AmbienceSlider)
            {
                Debug.LogError("[AudioSettingsPanel] 需要面板 CanvasGroup 与三个独立的音量 Slider。", this);
                enabled = false;
                return;
            }
            ConfigureSlider(MasterSlider);
            ConfigureSlider(SfxSlider);
            ConfigureSlider(AmbienceSlider);
            if (CloudSlider != null) ConfigureSlider(CloudSlider);
            SetVisible(false);
            _ready = true;
        }

        private void OnEnable()
        {
            if (!_ready) return;
            MasterSlider.onValueChanged.AddListener(OnVolumeChanged);
            SfxSlider.onValueChanged.AddListener(OnVolumeChanged);
            AmbienceSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (CloudSlider != null) CloudSlider.onValueChanged.AddListener(OnCloudChanged);
            if (OpenButton != null) OpenButton.onClick.AddListener(Open);
            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
            RefreshValues();
        }

        private void OnDisable()
        {
            FlushPending();
            if (!_ready) return;
            if (MasterSlider != null) MasterSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (SfxSlider != null) SfxSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (AmbienceSlider != null) AmbienceSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (CloudSlider != null) CloudSlider.onValueChanged.RemoveListener(OnCloudChanged);
            if (OpenButton != null) OpenButton.onClick.RemoveListener(Open);
            if (CloseButton != null) CloseButton.onClick.RemoveListener(Close);
            SetVisible(false);
        }

        private void Update()
        {
            if (_dirty && Time.unscaledTime >= _saveAt) FlushPending();
        }

        private void OnApplicationPause(bool paused) { if (paused) FlushPending(); }
        private void OnApplicationFocus(bool focused) { if (!focused) FlushPending(); }
        private void OnApplicationQuit() => FlushPending();

        public void Open()
        {
            if (!_ready || !isActiveAndEnabled || IsOpen) return;
            RefreshValues();
            SetVisible(true);
            Audio?.Play(AudioCue.UiOpen);
        }

        public void Close()
        {
            if (!_ready || !IsOpen) return;
            FlushPending();
            SetVisible(false);
            Audio?.Play(AudioCue.UiCancel);
        }

        private void RefreshValues()
        {
            GameAudioService audio = Audio;
            if (audio == null) return;
            MasterSlider.SetValueWithoutNotify(audio.MasterVolume);
            SfxSlider.SetValueWithoutNotify(audio.SfxVolume);
            AmbienceSlider.SetValueWithoutNotify(audio.AmbienceVolume);
            if (CloudSlider != null) CloudSlider.SetValueWithoutNotify(CloudPresentationPreferences.Density);
            RefreshLabels();
        }

        private void OnVolumeChanged(float ignored)
        {
            GameAudioService audio = Audio;
            if (!_ready || !IsOpen || audio == null) return;
            audio.SetVolumes(MasterSlider.value, SfxSlider.value, AmbienceSlider.value, save: false);
            RefreshLabels();
            _dirty = true;
            _saveAt = Time.unscaledTime + SaveDelay;
        }

        private void FlushPending()
        {
            if (!_dirty) return;
            GameAudioService audio = Audio;
            if (audio == null) return;
            audio.SetVolumes(audio.MasterVolume, audio.SfxVolume, audio.AmbienceVolume, save: true);
            CloudPresentationPreferences.Save();
            _dirty = false;
        }

        private void OnCloudChanged(float value)
        {
            if (!_ready || !IsOpen) return;
            CloudPresentationPreferences.SetDensity(value);
            RefreshLabels();
            _dirty = true; _saveAt = Time.unscaledTime + SaveDelay;
        }

        private void RefreshLabels()
        {
            if (MasterValue != null) MasterValue.text = Mathf.RoundToInt(MasterSlider.value * 100f) + "%";
            if (SfxValue != null) SfxValue.text = Mathf.RoundToInt(SfxSlider.value * 100f) + "%";
            if (AmbienceValue != null) AmbienceValue.text = Mathf.RoundToInt(AmbienceSlider.value * 100f) + "%";
            if (CloudValue != null) CloudValue.text = CloudSlider.value <= 0 ? "关闭" : Mathf.RoundToInt(CloudSlider.value * 100f) + "%";
        }

        private void SetVisible(bool visible)
        {
            if (Panel == null) return;
            Panel.alpha = visible ? 1f : 0f;
            Panel.interactable = Panel.blocksRaycasts = visible;
        }

        private static void ConfigureSlider(Slider slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }
}
