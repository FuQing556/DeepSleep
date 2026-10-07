using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Accessories
{
    // 在角色和网络表现之后取最终姿态；纯表现，不添加碰撞/属性。
    [DefaultExecutionOrder(500)]
    public sealed class HeadwearSpriteView : MonoBehaviour
    {
        public SpriteRenderer Subject, Ornament;
        public HeadwearDefinition Equipped { get; private set; }
        public void SetEquipped(HeadwearDefinition definition) => Equipped = definition;
        private void LateUpdate()
        {
            if (Subject == null || Ornament == null) return;
            bool shown = Equipped != null && Subject.enabled && Subject.gameObject.activeInHierarchy &&
                Equipped.TryGetPose(Subject.sprite, out _);
            Ornament.enabled = shown;
            if (!shown) return;
            Equipped.TryGetPose(Subject.sprite, out var pose);
            var bounds = Subject.sprite.bounds;
            Vector2 head = (Vector2)bounds.min + Vector2.Scale(bounds.size, pose.Anchor);
            float sx = Subject.flipX ? -1 : 1, sy = Subject.flipY ? -1 : 1;
            head = Vector2.Scale(head, new Vector2(sx, sy));
            float scale = bounds.size.x * pose.WidthRatio / Equipped.Sprite.bounds.size.x;
            var rotation = Quaternion.Euler(0, 0, pose.Angle);
            Vector2 basePoint = (Vector2)Equipped.Sprite.bounds.min +
                Vector2.Scale(Equipped.Sprite.bounds.size, Equipped.BaseAnchor);
            Vector3 correction = rotation * (Vector3)(basePoint * scale);
            Ornament.transform.localPosition = (Vector3)head - Vector3.Scale(correction, new Vector3(sx, sy, 1));
            Ornament.transform.localRotation = Quaternion.Euler(0, 0, pose.Angle * sx * sy);
            Ornament.transform.localScale = new Vector3(scale * sx, scale * sy, 1);
            Ornament.sprite = Equipped.Sprite;
            Ornament.color = Subject.color;
            Ornament.sortingLayerID = Subject.sortingLayerID;
            Ornament.sortingOrder = Subject.sortingOrder + (Equipped.Layer == AccessoryLayer.Back ? -1 : 1);
        }
        private void OnDisable() { if (Ornament != null) Ornament.enabled = false; }
    }
}
