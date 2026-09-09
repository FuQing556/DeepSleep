using System.Collections.Generic;
using DeepSleep.Runtime.Input.Commands;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>
    /// 顺序消费可靠命令；没新命令时只保持连续输入，绝不重放 Pressed/Released。
    /// 超时归零，防止断线后持续移动或一直挥剑。身份校验由会话层负责。
    /// </summary>
    public sealed class RemoteCommandSource : ICommandSource
    {
        private readonly Queue<PlayerCommand> _queue;
        private readonly int _capacity;
        private readonly float _timeout;
        private PlayerCommand _last;
        private uint _received;
        private bool _hasReceived;
        private float _lastArrival;

        public uint LastConsumedSequence { get; private set; }
        public RemoteCommandSource(int capacity, float timeout)
        {
            _capacity = capacity; _timeout = timeout;
            _queue = new Queue<PlayerCommand>(capacity);
        }

        public bool Enqueue(in PlayerCommand command, float now)
        {
            if ((_hasReceived && !IsNewer(command.Sequence, _received)) || _queue.Count >= _capacity)
                return false;
            _received = command.Sequence; _hasReceived = true; _lastArrival = now;
            _queue.Enqueue(command);
            return true;
        }

        public bool TryGetCommand(uint tick, out PlayerCommand command)
        {
            if (_hasReceived && Time.unscaledTime - _lastArrival <= _timeout)
            {
                if (_queue.Count > 0)
                {
                    _last = _queue.Dequeue(); LastConsumedSequence = _last.Sequence;
                    command = Retick(_last, tick, false); return true;
                }
                command = Retick(_last, tick, true); return true;
            }
            _queue.Clear(); command = new PlayerCommand(_received, tick, Vector2.zero, default,
                0, 0, 0, 0, 0, 0); return true;
        }

        public void Clear()
        {
            _queue.Clear(); _last = default; _hasReceived = false; _received = 0;
            LastConsumedSequence = 0;
        }
        public void DiscardThrough(uint sequence)
        {
            if (_hasReceived && !IsNewer(sequence, _received)) return;
            _queue.Clear(); _last = default; _received = sequence; _hasReceived = true;
            _lastArrival = float.NegativeInfinity; LastConsumedSequence = sequence;
        }

        public static bool IsNewer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;
        private static CommandButtonState Hold(CommandButtonState value) => value & CommandButtonState.Held;
        private static PlayerCommand Retick(in PlayerCommand c, uint tick, bool heldOnly) =>
            new(c.Sequence, tick, c.Move, c.Aim,
                heldOnly ? Hold(c.PrimarySkill) : c.PrimarySkill,
                heldOnly ? Hold(c.SecondarySkill) : c.SecondarySkill,
                heldOnly ? Hold(c.ConfirmAim) : c.ConfirmAim,
                heldOnly ? Hold(c.CancelAim) : c.CancelAim, 0, 0);
    }
}
