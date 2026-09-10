using System;
using UnityEngine;
using UnityEngine.UI;

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

        private ShopProductDefinition _product;
        private Action<ShopProductDefinition> _purchaseRequested;

        private void OnEnable()
        {
            if (_purchase != null) _purchase.onClick.AddListener(Buy);
        }

        private void OnDisable()
        {
            if (_purchase != null) _purchase.onClick.RemoveListener(Buy);
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
        }

        private void Buy()
        {
            if (_product != null) _purchaseRequested?.Invoke(_product);
        }
    }
}
