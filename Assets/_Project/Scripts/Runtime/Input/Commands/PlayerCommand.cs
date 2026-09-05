using UnityEngine;

namespace DeepSleep.Runtime.Input.Commands
{
    /// <summary>
    /// 一个控制源在一个模拟刻提交给玩法层的完整意图快照。
    /// 本地输入、同伴 AI 与远端网络输入都必须产生同一种命令。
    /// </summary>
    public readonly struct PlayerCommand
    {
        public PlayerCommand(
            uint sequence,
            uint simulationTick,
            Vector2 move,
            AimIntent aim,
            CommandButtonState primarySkill,
            CommandButtonState secondarySkill,
            CommandButtonState confirmAim,
            CommandButtonState cancelAim,
            CommandButtonState reconnect,
            CommandButtonState pause)
        {
            Sequence = sequence;
            SimulationTick = simulationTick;
            Move = move;
            Aim = aim;
            PrimarySkill = primarySkill;
            SecondarySkill = secondarySkill;
            ConfirmAim = confirmAim;
            CancelAim = cancelAim;
            Reconnect = reconnect;
            Pause = pause;
        }

        /// <summary>
        /// 控制源生成的递增序号，用于网络去重与顺序检查。
        /// </summary>
        public uint Sequence { get; }

        /// <summary>
        /// 这份命令希望作用于的固定模拟刻。
        /// </summary>
        public uint SimulationTick { get; }

        /// <summary>
        /// 未经玩法层修正的二维移动意图，允许保留摇杆力度。
        /// </summary>
        public Vector2 Move { get; }

        /// <summary>
        /// 瞄准方向、世界坐标或归一化屏幕坐标及其解释方式。
        /// </summary>
        public AimIntent Aim { get; }

        public CommandButtonState PrimarySkill { get; }

        public CommandButtonState SecondarySkill { get; }

        public CommandButtonState ConfirmAim { get; }

        public CommandButtonState CancelAim { get; }

        public CommandButtonState Reconnect { get; }

        /// <summary>
        /// 暂停或菜单请求。在线模式是否暂停由上层会话规则决定。
        /// </summary>
        public CommandButtonState Pause { get; }
    }
}
