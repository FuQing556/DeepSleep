using UnityEngine;

namespace DeepSleep.Runtime.World.Playfield
{
    /// <summary>
    /// 玩家、敌人、拾取与战斗查询共同使用的固定逻辑区域。
    /// 宽屏扩展只影响背景，不能修改该矩形。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_CombatPlayfield_",
        menuName = "DeepSleep/配置/世界/逻辑战斗区域")]
    public sealed class CombatPlayfieldConfig : ScriptableObject
    {
        [SerializeField] private Rect _worldBounds;

        public Rect WorldBounds => _worldBounds;

        public bool TryGetRayExitDistance(
            Vector2 origin,
            Vector2 direction,
            out float distance)
        {
            return CombatPlayfieldMath.TryGetRayExitDistance(
                _worldBounds,
                origin,
                direction,
                out distance);
        }

        public bool TryGetRayExitDistanceAfterIntersection(
            Vector2 origin,
            Vector2 direction,
            out float distance)
        {
            return CombatPlayfieldMath.TryGetRayExitDistanceAfterIntersection(
                _worldBounds,
                origin,
                direction,
                out distance);
        }

        public bool TryValidate(out string reason)
        {
            if (_worldBounds.width <= 0f ||
                _worldBounds.height <= 0f)
            {
                reason = "逻辑战斗区域的宽和高必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
