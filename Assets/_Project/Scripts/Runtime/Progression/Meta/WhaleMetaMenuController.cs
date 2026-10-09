using System.Collections.Generic;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation.Accessories;

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
        public HeadwearImageView[] HeadwearPreviews;
        public DeepSleep.Runtime.Presentation.Skins.PlayerSkinImageView[] SkinPreviews;

        private readonly List<MetaProductCardView> _shopCards = new();
        private readonly List<MetaProductCardView> _inventoryCards = new();

        private void Awake()
        {
            if (_profile == null && GameAppRoot.Instance != null)
            {
                _profile = GameAppRoot.Instance.Profile;
            }
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
            foreach (var preview in HeadwearPreviews) preview.Bind(_profile);
            foreach (var preview in SkinPreviews) preview.Bind(_profile);
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
            bool purchased = _profile.TryPurchase(product, out string message);
            GameAppRoot.Instance?.Audio?.Play(purchased ? AudioCue.Upgrade : AudioCue.UiReject);
            if (_feedback != null) _feedback.text = message;
            if (purchased)
                GameAppRoot.Instance.Achievements.Report(
                    AchievementTriggerIds.ProductPurchased);
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
                _shopCards[index].RenderEquipment(_profile, false, Equip);
                _inventoryCards[index].RenderEquipment(_profile, true, Equip);
                _inventoryCards[index].gameObject.SetActive(owned > 0);
                hasAnything |= owned > 0;
            }
            _inventoryEmpty.gameObject.SetActive(!hasAnything);
        }
        private void Equip(ShopProductDefinition product, PlayerRole role)
        {
            if (product.IsSkin)
            {
                bool wearingSkin = _profile.GetSkin(role) == product.ProductId;
                bool changed = _profile.TrySetSkin(role, wearingSkin ? null : product, out string skinMessage);
                GameAppRoot.Instance?.Audio?.Play(changed ? AudioCue.UiConfirm : AudioCue.UiReject);
                if (_feedback != null) _feedback.text = skinMessage;
                Render(); return;
            }
            bool wearing = _profile.GetAccessory(role, product.Slot) == product.ProductId;
            bool success = _profile.TrySetAccessory(role, product.Slot, wearing ? null : product, out string message);
            GameAppRoot.Instance?.Audio?.Play(success ? AudioCue.UiConfirm : AudioCue.UiReject);
            if (_feedback != null) _feedback.text = message;
            Render();
        }
    }
}
