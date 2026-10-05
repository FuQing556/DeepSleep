using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Common
{
    /// <summary>原生 uGUI 圆角渐变底板。物体须显式装配 RectTransform 和 CanvasRenderer。</summary>
    public sealed class UiSurfaceGraphic : MaskableGraphic
    {
        public Color TopColor = Color.white, BottomColor = Color.white, BorderColor = Color.clear;
        public Color ShadowColor = new Color(0, 0, 0, .18f);
        [Min(0)] public float Radius = 16f;
        [Min(0)] public float BorderWidth = 1f;
        [Min(0)] public float ShadowOffset = 3f;

        // 每个四分之一圆固定六段；仅在布局或配色变脏时重建，不做逐帧动画网格。
        private const int CORNER_SEGMENTS = 6;
        private const int OUTLINE_VERTICES = 4 * (CORNER_SEGMENTS + 1);

        /// <summary>替换皮肤色但保留 Graphic.color 和 Button ColorTint 的乘色语义。</summary>
        public void SetStyle(Color top, Color bottom, Color border, float radius)
        {
            TopColor = top; BottomColor = bottom; BorderColor = border;
            Radius = Mathf.Max(0, radius);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float radius = ClampRadius(rect, Radius);
            float border = BorderColor.a > 0
                ? Mathf.Clamp(BorderWidth, 0, Mathf.Min(rect.width, rect.height) * .5f)
                : 0;
            if (ShadowOffset > 0 && ShadowColor.a > 0)
            {
                Color shadow = ShadowColor;
                shadow.a *= Mathf.Max(TopColor.a, BottomColor.a);
                Rect shadowRect = rect;
                shadowRect.y -= ShadowOffset;
                Color softShadow = shadow;
                softShadow.a *= .4f;
                AddFill(mesh, Expand(shadowRect, ShadowOffset), radius + ShadowOffset, softShadow, softShadow);
                AddFill(mesh, shadowRect, radius, shadow, shadow);
            }
            Rect inside = Expand(rect, -border);
            if (border > 0 && BorderColor.a > 0)
                AddBorder(mesh, rect, inside, radius, Mathf.Max(0, radius - border));
            if (inside.width > 0 && inside.height > 0)
                AddFill(mesh, inside, Mathf.Max(0, radius - border), TopColor, BottomColor);
        }

        private void AddFill(VertexHelper mesh, Rect rect, float radius, Color top, Color bottom)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(rect.center, Color.Lerp(bottom, top, .5f) * color, Vector2.zero);
            radius = ClampRadius(rect, radius);
            for (int i = 0; i < OUTLINE_VERTICES; i++)
            {
                Vector2 point = OutlinePoint(rect, radius, i);
                Color shade = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                mesh.AddVert(point, shade * color, Vector2.zero);
            }
            for (int i = 0; i < OUTLINE_VERTICES; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % OUTLINE_VERTICES);
        }

        private void AddBorder(VertexHelper mesh, Rect outside, Rect inside, float outerRadius, float innerRadius)
        {
            int start = mesh.currentVertCount;
            Color tint = BorderColor * color;
            for (int i = 0; i < OUTLINE_VERTICES; i++)
            {
                mesh.AddVert(OutlinePoint(outside, outerRadius, i), tint, Vector2.zero);
                mesh.AddVert(OutlinePoint(inside, innerRadius, i), tint, Vector2.zero);
            }
            for (int i = 0; i < OUTLINE_VERTICES; i++)
            {
                int outer = start + i * 2, next = start + ((i + 1) % OUTLINE_VERTICES) * 2;
                mesh.AddTriangle(outer, next, outer + 1);
                mesh.AddTriangle(next, next + 1, outer + 1);
            }
        }

        private static Vector2 OutlinePoint(Rect rect, float radius, int index)
        {
            int corner = index / (CORNER_SEGMENTS + 1);
            int segment = index % (CORNER_SEGMENTS + 1);
            float angle = (corner * 90f + segment * 90f / CORNER_SEGMENTS) * Mathf.Deg2Rad;
            Vector2 center = new Vector2(
                corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Rect Expand(Rect rect, float amount) =>
            Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);

        private static float ClampRadius(Rect rect, float radius) =>
            Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
    }
}
