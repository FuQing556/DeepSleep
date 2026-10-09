using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Presentation.Skins
{
    /// <summary>局外/选角只展示固定不露牌常态；在饰品视图之前更新。</summary>
    [DefaultExecutionOrder(450)]
    public sealed class PlayerSkinImageView : MonoBehaviour
    {
        public Image Subject;
        public ShopProductDefinition[] Catalog;
        private LocalPlayerProfileStore _profile;
        private bool _sessionOverride;
        private ushort _sessionId;
        public void Bind(LocalPlayerProfileStore profile) => _profile = profile;
        public void SetSessionSkin(ushort id) { _sessionOverride = true; _sessionId = id; }
        private void LateUpdate()
        {
            if (_profile == null) return;
            foreach (var product in Catalog)
            {
                var skin = product.Skin;
                if (!skin.Contains(Subject.sprite)) continue;
                bool wearing = _sessionOverride ? _sessionId == skin.NetworkId : _profile.GetSkin(skin.Role) == product.ProductId;
                Subject.sprite = wearing ? skin.Preview : skin.DefaultAlive;
                return;
            }
        }
    }
}
