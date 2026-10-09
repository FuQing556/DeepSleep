using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    public enum AccessorySlot { Front, Back }
    [CreateAssetMenu(
        fileName = "CFG_META_Product_",
        menuName = "DeepSleep/Progression/Meta Shop Product")]
    public sealed class ShopProductDefinition : ScriptableObject
    {
        [SerializeField] private string _productId;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField, Min(0)] private int _price;
        [SerializeField] private Sprite _icon;
        [SerializeField] private bool _repeatable = true;
        [SerializeField] private bool _isHeadwear;
        [SerializeField] private bool _isBackwear;
        [SerializeField] private PlayerSkinDefinition _skin;

        public string ProductId => _productId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public int Price => _price;
        public Sprite Icon => _icon;
        public bool Repeatable => _repeatable;
        public bool IsHeadwear => _isHeadwear;
        public bool IsAccessory => _isHeadwear || _isBackwear;
        public PlayerSkinDefinition Skin => _skin;
        public bool IsSkin => _skin != null;
        public AccessorySlot Slot => _isBackwear ? AccessorySlot.Back : AccessorySlot.Front;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_productId) ||
                string.IsNullOrWhiteSpace(_displayName))
            {
                reason = "商品 ID 与名称不能为空。";
                return false;
            }
            if (_price < 0)
            {
                reason = $"{_displayName} 的价格不能为负数。";
                return false;
            }
            if ((_isHeadwear && _isBackwear) || (IsAccessory && (_repeatable || _icon == null)))
            { reason = "头饰必须是非重复购买商品并配置图标。"; return false; }
            if (IsSkin && (IsAccessory || _repeatable || _icon == null || !_skin.TryValidate(out reason)))
            { reason = "服装必须独立于饰品、永久持有且配置有效姿态及图标。"; return false; }

            reason = string.Empty;
            return true;
        }
    }
}
