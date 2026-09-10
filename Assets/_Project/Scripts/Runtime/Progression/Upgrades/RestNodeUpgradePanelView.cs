using System;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Upgrades
{
    public sealed class RestNodeUpgradePanelView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private Text _title;
        [SerializeField] private Text _status;
        [SerializeField] private Button[] _cardButtons;
        [SerializeField] private Text[] _cardTitles;
        [SerializeField] private Text[] _cardDescriptions;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Text _refreshLabel;
        [SerializeField] private Button _closeButton;

        private Action<int> _selectHandler;
        private Action _refreshHandler;
        private PlayerRole _role;

        public bool IsOpen => _panel != null && _panel.alpha > 0.5f;
        public PlayerRole Role => _role;

        private void Awake()
        {
            for (int index = 0; index < _cardButtons.Length; index++)
            {
                int captured = index;
                _cardButtons[index].onClick.AddListener(
                    () => _selectHandler?.Invoke(captured));
            }
            _refreshButton.onClick.AddListener(
                () => _refreshHandler?.Invoke());
            _closeButton.onClick.AddListener(Hide);
            Hide();
        }

        public void Show(
            PlayerRole role,
            UpgradeDefinition[] offers,
            Func<UpgradeCardId, int> rankReader,
            int walletBalance,
            int refreshCost,
            Action<int> selectHandler,
            Action refreshHandler)
        {
            _role = role;
            _selectHandler = selectHandler;
            _refreshHandler = refreshHandler;
            _title.text = role == PlayerRole.DeepSeek
                ? "DS · Token 强化商店"
                : "HS · Token 强化商店";
            string roleName = role == PlayerRole.DeepSeek ? "DS" : "HS";
            _status.text =
                $"{roleName} TOKEN：{walletBalance} · 可连续购买，购买后刷新商品";

            for (int index = 0; index < _cardButtons.Length; index++)
            {
                bool valid = offers != null &&
                    index < offers.Length && offers[index] != null;
                _cardButtons[index].gameObject.SetActive(valid);
                if (!valid)
                {
                    continue;
                }

                UpgradeDefinition definition = offers[index];
                int currentRank = rankReader(definition.Id);
                int tokenCost = definition.GetTokenCost(currentRank);
                _cardTitles[index].text =
                    $"{definition.DisplayName}  {currentRank}→{currentRank + 1}";
                _cardDescriptions[index].text =
                    $"{definition.Description}\n\n价格：{tokenCost} TOKEN";
                _cardButtons[index].interactable =
                    walletBalance >= tokenCost;
            }

            _refreshButton.interactable = walletBalance >= refreshCost;
            _refreshLabel.text = refreshCost == 0
                ? "刷新（本节点首次免费）"
                : $"再次刷新 · {refreshCost} TOKEN";
            SetVisible(true);
        }

        public void ShowStatus(string message)
        {
            _status.text = message;
        }

        public void Hide()
        {
            _selectHandler = null;
            _refreshHandler = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_panel == null)
            {
                return;
            }
            _panel.alpha = visible ? 1f : 0f;
            _panel.interactable = visible;
            _panel.blocksRaycasts = visible;
        }
    }
}
