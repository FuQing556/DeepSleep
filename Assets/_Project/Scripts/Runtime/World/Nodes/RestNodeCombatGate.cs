using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Actions;
using UnityEngine;

namespace DeepSleep.Runtime.World.Nodes
{
    /// <summary>切段终止待发连射/残响；只锁战斗，不锁节点移动与调查。</summary>
    public sealed class RestNodeCombatGate : MonoBehaviour
    {
        [SerializeField] private RestNodePrototypeController2D _node;
        [SerializeField] private HarnessTerminalLaserController _laser;
        [SerializeField] private HarnessMeleeController _melee;
        [SerializeField] private PlayerActionGate[] _players;
        private void OnEnable()
        {
            if(_node==null || _laser==null || _melee==null || _players==null || _players.Length!=2)
            { Debug.LogError("[RestNodeCombatGate] 节点与两角色战斗引用未配置。",this); enabled=false; return; }
            _node.StateChanged+=OnState;
            _node.CombatSuspended+=Suspend;
            OnState(_node.State);
        }
        private void OnState(RestNodeState state)
        {
            if(state==RestNodeState.Combat || state==RestNodeState.Clearing)
            { foreach(var p in _players)p.ClearBlock(this); }
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
            if(_node!=null){_node.StateChanged-=OnState;_node.CombatSuspended-=Suspend;}
            if(_players!=null)foreach(var p in _players)if(p!=null)p.ClearBlock(this);
        }
    }
}
