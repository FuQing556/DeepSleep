using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Actions;
using UnityEngine;

namespace DeepSleep.Runtime.World.Nodes
{
    /// <summary>由章节生命周期显式控制攻击许可；取消待发连射/残响，不锁移动与调查。</summary>
    public sealed class RestNodeCombatGate : MonoBehaviour
    {
        [SerializeField] private HarnessTerminalLaserController _laser;
        [SerializeField] private HarnessMeleeController _melee;
        [SerializeField] private PlayerActionGate[] _players;
        private bool _hasState;
        private bool _combatAllowed;
        public bool CombatAllowed => _hasState && _combatAllowed;
        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[RestNodeCombatGate] " + reason, this); enabled = false; return; }
            _hasState = false;
            SetCombatAllowed(false);
        }

        /// <summary>只清除本门自己的限制，不覆盖倒地/其他机制施加的限制。</summary>
        public void SetCombatAllowed(bool allowed)
        {
            if (_hasState && _combatAllowed == allowed) return;
            _hasState = true;
            _combatAllowed = allowed;
            if (allowed)
            { foreach (var player in _players) player.ClearBlock(this); }
            else Suspend();
        }
        private void Suspend()
        {
            _melee.CancelCombatSequence();
            _laser.CancelSelection();
            foreach(var p in _players)p.SetBlock(this,PlayerActionBlock.ActiveCombat|PlayerActionBlock.AutomaticCombat);
        }
        private void OnDisable()
        {
            if(_players!=null)foreach(var p in _players)if(p!=null)p.ClearBlock(this);
            _hasState = false;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_laser == null || _melee == null || _players == null || _players.Length != 2 ||
                _players[0] == null || _players[1] == null || _players[0] == _players[1])
            { reason = "激光、近战与两名不同玩家的战斗门必须完整配置。"; return false; }
            reason = string.Empty;
            return true;
        }
    }
}
