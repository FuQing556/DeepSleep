using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams.Presentation
{
    /// <summary>
    /// 在存在父节点的情况下，仍以世界单位安排精灵尺寸。
    /// </summary>
    internal static class WorldSpriteGeometry2D
    {
        public static void ShowWithWorldDiameter(
            SpriteRenderer renderer,
            Vector2 position,
            float worldDiameter,
            float rotationDegrees,
            Color color)
        {
            Transform viewTransform = renderer.transform;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            float nativeDiameter = Mathf.Max(spriteSize.x, spriteSize.y);
            Vector3 parentScale = viewTransform.parent != null
                ? viewTransform.parent.lossyScale
                : Vector3.one;
            float uniformParentScale = Mathf.Max(
                Mathf.Abs(parentScale.x),
                Mathf.Abs(parentScale.y),
                0.0001f);
            float localScale =
                worldDiameter / (nativeDiameter * uniformParentScale);

            viewTransform.SetPositionAndRotation(
                position,
                Quaternion.Euler(0f, 0f, rotationDegrees));
            viewTransform.localScale =
                new Vector3(localScale, localScale, 1f);
            renderer.color = color;
            renderer.enabled = true;
        }

        public static Color WithMultipliedAlpha(
            Color color,
            float alphaMultiplier)
        {
            color.a *= Mathf.Clamp01(alphaMultiplier);
            return color;
        }

        public static float DirectionToAngle(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    }
}
