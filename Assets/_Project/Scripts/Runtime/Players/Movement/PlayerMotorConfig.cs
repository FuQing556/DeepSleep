using UnityEngine;

namespace DeepSleep.Runtime.Players.Movement
{
    /// <summary>
    /// 玩家自由移动所需的可调数据。运行时只读取，不修改该资产。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_PlayerMotor",
        menuName = "DeepSleep/配置/玩家移动")]
    public sealed class PlayerMotorConfig : ScriptableObject
    {
        [Header("速度")]
        [SerializeField, Min(0.01f)] private float maximumSpeed;
        [SerializeField, Min(0.01f)] private float acceleration;
        [SerializeField, Min(0.01f)] private float deceleration;

        [Header("逻辑玩法区域")]
        [SerializeField] private Rect movementBounds;

        public float MaximumSpeed => maximumSpeed;

        public float Acceleration => acceleration;

        public float Deceleration => deceleration;

        public Rect MovementBounds => movementBounds;

        public bool TryValidate(out string reason)
        {
            if (maximumSpeed <= 0f)
            {
                reason = "最大速度必须大于 0。";
                return false;
            }

            if (acceleration <= 0f)
            {
                reason = "加速度必须大于 0。";
                return false;
            }

            if (deceleration <= 0f)
            {
                reason = "减速度必须大于 0。";
                return false;
            }

            if (movementBounds.width <= 0f || movementBounds.height <= 0f)
            {
                reason = "移动区域的宽和高必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
