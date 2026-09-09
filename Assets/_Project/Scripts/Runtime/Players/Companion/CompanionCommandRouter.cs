using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>开局选择真人角色后，只驱动另一名角色的大脑。</summary>
    public sealed class CompanionCommandRouter : MonoBehaviour, ICommandSource
    {
        public PlayerControlAssignment Assignment;
        public CompanionCommandSource2D DeepSeek;
        public CompanionCommandSource2D Harness;
        private CompanionCommandSource2D _active;

        public bool TryGetCommand(uint simulationTick, out PlayerCommand command)
        {
            command = default;
            if (Assignment == null || DeepSeek == null || Harness == null) return false;
            var next = Assignment.CurrentLocalPlayerRole == PlayerRole.DeepSeek ? Harness : DeepSeek;
            if (_active != next)
            {
                if (_active != null) _active.ReleaseControl();
                _active = next;
                _active.ResetIntent();
            }
            return _active.TryGetCommand(simulationTick, out command);
        }
    }
}
