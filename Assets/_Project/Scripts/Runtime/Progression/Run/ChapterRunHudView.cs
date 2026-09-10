using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Run
{
    public sealed class ChapterRunHudView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private Text _label;
        [SerializeField] private CanvasGroup _settlementPanel;
        [SerializeField] private Text _settlementLabel;
        [SerializeField] private Button _returnButton;

        public event Action ReturnRequested;

        private void OnEnable()
        {
            if (_returnButton != null)
            {
                _returnButton.onClick.AddListener(RequestReturn);
            }
        }

        private void OnDisable()
        {
            if (_returnButton != null)
            {
                _returnButton.onClick.RemoveListener(RequestReturn);
            }
        }

        public void Render(string text, bool visible, bool failure)
        {
            if (_panel == null || _label == null)
            {
                return;
            }

            _panel.alpha = visible ? 1f : 0f;
            _panel.interactable = false;
            _panel.blocksRaycasts = false;
            _label.text = text ?? string.Empty;
            _label.color = failure
                ? new Color(1f, 0.35f, 0.35f)
                : Color.white;
        }

        public void RenderSettlement(string text, bool visible)
        {
            if (_settlementPanel == null || _settlementLabel == null ||
                _returnButton == null)
            {
                return;
            }

            _settlementPanel.alpha = visible ? 1f : 0f;
            _settlementPanel.interactable = visible;
            _settlementPanel.blocksRaycasts = visible;
            _settlementLabel.text = text ?? string.Empty;
            _returnButton.gameObject.SetActive(visible);
        }

        private void RequestReturn() => ReturnRequested?.Invoke();
    }
}
