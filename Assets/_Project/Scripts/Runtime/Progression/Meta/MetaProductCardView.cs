using System;
using UnityEngine;
using UnityEngine.UI;
using DeepSleep.Runtime.Players.Identity;

namespace DeepSleep.Runtime.Progression.Meta
{
    public sealed class MetaProductCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _name;
        [SerializeField] private Text _description;
        [SerializeField] private Text _owned;
        [SerializeField] private Text _price;
        [SerializeField] private Button _purchase;
        [SerializeField] private Text _purchaseLabel;
        [SerializeField] private Button _equipDeepSeek, _equipHarness;
        [SerializeField] private Text _equipDeepSeekLabel, _equipHarnessLabel;
        private Action<ShopProductDefinition, PlayerRole> _equipRequested;

        private ShopProductDefinition _product;
        private Action<ShopProductDefinition> _purchaseRequested;

        private void OnEnable()
        {
            if (_purchase != null) _purchase.onClick.AddListener(Buy);
            if (_equipDeepSeek != null) _equipDeepSeek.onClick.AddListener(EquipDeepSeek);
            if (_equipHarness != null) _equipHarness.onClick.AddListener(EquipHarness);
        }

        private void OnDisable()
        {
            if (_purchase != null) _purchase.onClick.RemoveListener(Buy);
            if (_equipDeepSeek != null) _equipDeepSeek.onClick.RemoveListener(EquipDeepSeek);
            if (_equipHarness != null) _equipHarness.onClick.RemoveListener(EquipHarness);
        }

        public void Render(
            ShopProductDefinition product,
            int owned,
            bool shopMode,
            bool affordable,
            Action<ShopProductDefinition> purchaseRequested)
        {
            _product = product;
            _purchaseRequested = purchaseRequested;
            _name.text = product.DisplayName;
            _description.text = string.IsNullOrWhiteSpace(product.Description)
                ? "简介暂未填写"
                : product.Description;
            _owned.text = $"持有 ×{owned}";
            _price.text = shopMode ? $"{product.Price} 鲸元券" : string.Empty;
            _icon.sprite = product.Icon;
            _icon.enabled = product.Icon != null;
            _purchase.gameObject.SetActive(shopMode);
            _purchase.interactable = affordable &&
                (product.Repeatable || owned == 0);
            if (_purchaseLabel != null) _purchaseLabel.text = !product.Repeatable && owned > 0 ? "已拥有" : "购买";
        }

        public void RenderEquipment(LocalPlayerProfileStore profile, bool inventory,
            Action<ShopProductDefinition, PlayerRole> request)
        {
            _equipRequested = request;
            bool shown = inventory && _product.IsAccessory && profile.GetOwnedCount(_product.ProductId) > 0;
            _equipDeepSeek.gameObject.SetActive(shown); _equipHarness.gameObject.SetActive(shown);
            _equipDeepSeekLabel.text = profile.GetAccessory(PlayerRole.DeepSeek, _product.Slot) == _product.ProductId ? "DS · 摘下" : "DS · 佩戴";
            _equipHarnessLabel.text = profile.GetAccessory(PlayerRole.Harness, _product.Slot) == _product.ProductId ? "HS · 摘下" : "HS · 佩戴";
        }
        private void EquipDeepSeek() => _equipRequested?.Invoke(_product, PlayerRole.DeepSeek);
        private void EquipHarness() => _equipRequested?.Invoke(_product, PlayerRole.Harness);

        private void Buy()
        {
            if (_product != null) _purchaseRequested?.Invoke(_product);
        }
    }
}
