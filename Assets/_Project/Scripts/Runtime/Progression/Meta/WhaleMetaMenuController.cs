using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Meta
{
    public sealed class WhaleMetaMenuController : MonoBehaviour
    {
        [SerializeField] private LocalPlayerProfileStore _profile;
        [SerializeField] private ShopProductDefinition[] _products;
        [SerializeField] private MetaProductCardView _cardPrefab;
        [SerializeField] private Transform _shopContent;
        [SerializeField] private Transform _inventoryContent;
        [SerializeField] private Text _shopBalance;
        [SerializeField] private Text _inventoryBalance;
        [SerializeField] private Text _levelSelectionBalance;
        [SerializeField] private Text _inventoryEmpty;
        [SerializeField] private Text _feedback;

        private readonly List<MetaProductCardView> _shopCards = new();
        private readonly List<MetaProductCardView> _inventoryCards = new();

        private void Awake()
        {
            if (_products == null || _cardPrefab == null)
            {
                enabled = false;
                return;
            }
            for (int index = 0; index < _products.Length; index++)
            {
                _shopCards.Add(Instantiate(_cardPrefab, _shopContent));
                _inventoryCards.Add(Instantiate(_cardPrefab, _inventoryContent));
            }
        }

        private void OnEnable()
        {
            if (_profile == null || !enabled) return;
            _profile.Changed += Render;
            Render();
        }

        private void OnDisable()
        {
            if (_profile != null) _profile.Changed -= Render;
        }

        private void Buy(ShopProductDefinition product)
        {
            _profile.TryPurchase(product, out string message);
            if (_feedback != null) _feedback.text = message;
            Render();
        }

        private void Render()
        {
            string balance = $"鲸元券：{_profile.WhaleVoucherBalance}";
            _shopBalance.text = balance;
            _inventoryBalance.text = balance;
            _levelSelectionBalance.text = balance;
            bool hasAnything = false;
            for (int index = 0; index < _products.Length; index++)
            {
                ShopProductDefinition product = _products[index];
                int owned = _profile.GetOwnedCount(product.ProductId);
                _shopCards[index].Render(
                    product,
                    owned,
                    true,
                    _profile.WhaleVoucherBalance >= product.Price,
                    Buy);
                _inventoryCards[index].Render(
                    product,
                    owned,
                    false,
                    false,
                    null);
                _inventoryCards[index].gameObject.SetActive(owned > 0);
                hasAnything |= owned > 0;
            }
            _inventoryEmpty.gameObject.SetActive(!hasAnything);
        }
    }
}
