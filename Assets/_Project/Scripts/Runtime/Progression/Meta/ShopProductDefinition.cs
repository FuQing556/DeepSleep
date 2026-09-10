using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
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

        public string ProductId => _productId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public int Price => _price;
        public Sprite Icon => _icon;
        public bool Repeatable => _repeatable;

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

            reason = string.Empty;
            return true;
        }
    }
}
