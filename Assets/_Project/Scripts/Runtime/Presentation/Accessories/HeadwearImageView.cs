using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Presentation.Accessories
{
    [DefaultExecutionOrder(500)]
    public sealed class HeadwearImageView : MonoBehaviour
    {
        public Image Subject, Ornament;
        public HeadwearDefinition[] Catalog;
        public AccessorySlot Slot;
        private LocalPlayerProfileStore _profile;
        private bool _sessionEquipment;
        private ushort _deepSeekId, _harnessId;
        public void Bind(LocalPlayerProfileStore profile) => _profile = profile;
        public void SetSessionEquipment(ushort deepSeek, ushort harness)
        { _sessionEquipment = true; _deepSeekId = deepSeek; _harnessId = harness; }
        private void LateUpdate()
        {
            HeadwearDefinition item = null;
            if (_profile != null)
                foreach (var entry in Catalog)
                    if (entry.Product.Slot == Slot && entry.TryGetPose(Subject.sprite, out var binding))
                    {
                        bool equipped = _sessionEquipment
                            ? entry.NetworkId == (binding.Role == DeepSleep.Runtime.Players.Identity.PlayerRole.DeepSeek ? _deepSeekId : _harnessId)
                            : entry.Product.ProductId == _profile.GetAccessory(binding.Role, Slot);
                        if (equipped) { item = entry; break; }
                    }
            bool visible = item != null && Subject.enabled && Subject.gameObject.activeInHierarchy && Subject.sprite != null && item.TryGetPose(Subject.sprite, out _);
            Ornament.enabled = visible;
            if (!visible) return;
            item.TryGetPose(Subject.sprite, out var pose);
            Vector2 size = Subject.rectTransform.rect.size;
            if (Subject.preserveAspect)
            {
                float fit = Mathf.Min(size.x / Subject.sprite.rect.width, size.y / Subject.sprite.rect.height);
                size = Subject.sprite.rect.size * fit;
            }
            Vector2 crownSize = item.Sprite.rect.size * (size.x * pose.WidthRatio / item.Sprite.rect.width);
            var r = Ornament.rectTransform;
            // Image 子节点始终画在父 Image 上方；作为相邻兄弟才能真实支持背饰。
            if (r.parent != Subject.transform.parent) r.SetParent(Subject.transform.parent, false);
            int subjectIndex = Subject.transform.GetSiblingIndex();
            bool back = item.Layer == AccessoryLayer.Back;
            if (back && r.GetSiblingIndex() != subjectIndex - 1)
                r.SetSiblingIndex(r.GetSiblingIndex() < subjectIndex ? subjectIndex - 1 : subjectIndex);
            else if (!back && r.GetSiblingIndex() != subjectIndex + 1)
                r.SetSiblingIndex(r.GetSiblingIndex() < subjectIndex ? subjectIndex : subjectIndex + 1);
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = item.BaseAnchor;
            r.sizeDelta = crownSize;
            Vector2 offset = Subject.rectTransform.rect.center + Vector2.Scale(size, pose.Anchor - new Vector2(.5f, .5f));
            r.position = Subject.transform.TransformPoint(offset);
            r.localScale = Subject.transform.localScale;
            r.localRotation = Subject.transform.localRotation * Quaternion.Euler(0, 0, pose.Angle);
            Ornament.sprite = item.Sprite; Ornament.color = Subject.color;
        }
        private void OnDisable() { if (Ornament != null) Ornament.enabled = false; }
    }
}
