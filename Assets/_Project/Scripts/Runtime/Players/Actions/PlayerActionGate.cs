using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Actions
{
    /// <summary>
    /// 合并多个系统对同一玩家施加的行动限制。
    /// 每个来源只能清除自己的限制，避免复活结束误解除眩晕或剧情限制。
    /// </summary>
    public sealed class PlayerActionGate : MonoBehaviour
    {
        private readonly Dictionary<object, PlayerActionBlock> _blocksByOwner =
            new Dictionary<object, PlayerActionBlock>();

        private PlayerActionBlock _combinedBlocks;

        public PlayerActionBlock CombinedBlocks => _combinedBlocks;

        public bool IsBlocked(PlayerActionBlock category)
        {
            return (_combinedBlocks & category) != 0;
        }

        public void SetBlock(object owner, PlayerActionBlock blocks)
        {
            if (owner == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerActionGate)}] 行动限制来源不能为空。",
                    this);
                return;
            }

            if (blocks == PlayerActionBlock.None)
            {
                ClearBlock(owner);
                return;
            }

            _blocksByOwner[owner] = blocks;
            RebuildCombinedBlocks();
        }

        public void ClearBlock(object owner)
        {
            if (owner == null || !_blocksByOwner.Remove(owner))
            {
                return;
            }

            RebuildCombinedBlocks();
        }

        private void RebuildCombinedBlocks()
        {
            _combinedBlocks = PlayerActionBlock.None;

            foreach (PlayerActionBlock blocks in _blocksByOwner.Values)
            {
                _combinedBlocks |= blocks;
            }
        }
    }
}
