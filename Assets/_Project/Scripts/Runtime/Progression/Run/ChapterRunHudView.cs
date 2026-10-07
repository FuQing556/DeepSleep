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
        [SerializeField] private Button _retryButton;
        [SerializeField] private Text _returnButtonLabel;
        [SerializeField] private Vector2 _challengeReturnPosition;
        [SerializeField] private Vector2 _challengeReturnSize;
        private Vector2 _returnAnchorMin, _returnAnchorMax, _returnPosition, _returnSize;
        private RectTransform _returnRect;
        private bool _actionsConfigured, _challengeActions;

        public event Action ReturnRequested;
        public event Action RetryRequested;

        private void Awake() => CacheReturnLayout();

        private void CacheReturnLayout()
        {
            if (_returnButton == null || _returnRect != null) return;
            _returnRect = (RectTransform)_returnButton.transform;
            _returnAnchorMin = _returnRect.anchorMin; _returnAnchorMax = _returnRect.anchorMax;
            _returnPosition = _returnRect.anchoredPosition; _returnSize = _returnRect.sizeDelta;
        }

        private void OnEnable()
        {
            if (_returnButton != null)
            {
                _returnButton.onClick.AddListener(RequestReturn);
            }
            if (_retryButton != null) _retryButton.onClick.AddListener(RequestRetry);
        }

        private void OnDisable()
        {
            if (_returnButton != null)
            {
                _returnButton.onClick.RemoveListener(RequestReturn);
            }
            if (_retryButton != null) _retryButton.onClick.RemoveListener(RequestRetry);
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
        private void RequestRetry() => RetryRequested?.Invoke();

        public void ConfigureChallengeActions(bool challenge)
        {
            // 章节Awake可能先于HUD，不能把首次挑战布局漏掉或当作标准布局重新缓存。
            CacheReturnLayout();
            if (_actionsConfigured && _challengeActions == challenge) return;
            _actionsConfigured = true; _challengeActions = challenge;
            if (_retryButton != null) _retryButton.gameObject.SetActive(challenge);
            if (_returnButtonLabel != null) _returnButtonLabel.text = challenge ? "返回图鉴" : "返回关卡选择";
            if (_returnRect == null) return;
            _returnRect.anchorMin = challenge ? Vector2.one * .5f : _returnAnchorMin;
            _returnRect.anchorMax = challenge ? Vector2.one * .5f : _returnAnchorMax;
            _returnRect.anchoredPosition = challenge ? _challengeReturnPosition : _returnPosition;
            _returnRect.sizeDelta = challenge ? _challengeReturnSize : _returnSize;
        }
    }
}
