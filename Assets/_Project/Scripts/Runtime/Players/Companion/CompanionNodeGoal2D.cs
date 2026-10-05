using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>
    /// 将真人离场意图或双 AI 小队目标转换成只读移动目标；不准备、不购买、不选奖励。
    /// 依赖同一会话、节点、控制分配、角色、角色碰撞体及 AI 战术配置，移动仍由命令源执行。
    /// </summary>
    public sealed class CompanionNodeGoal2D : MonoBehaviour
    {
        public RestNodePrototypeController2D Node;
        public CoopSessionController Session;
        public PlayerControlAssignment Assignment;
        public PlayerActor Actor;
        public Collider2D Shape;
        public CompanionTacticsConfig Config;

        private byte _cancelledRoles;
        private bool _subscribed;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Node == null || Session == null || Assignment == null || Actor == null ||
                Actor.Definition == null || Shape == null || Config == null || Config.ArrivalRadius <= 0f ||
                Session.Assignment != Assignment || (Actor != Session.DeepSeek && Actor != Session.Harness))
            {
                reason = "Node、Session、Assignment、Actor、Shape 或 Config 未正确装配到同一小队。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError("[CompanionNodeGoal] " + name + ": " + reason, this);
                enabled = false;
                return;
            }
            Node.PortalReadyCancelled += OnReadyCancelled;
            Node.StateChanged += OnNodeStateChanged;
            Session.SessionOpened += OnSessionOpened;
            Session.SessionClosed += ClearCancellation;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                Node.PortalReadyCancelled -= OnReadyCancelled;
                Node.StateChanged -= OnNodeStateChanged;
                Session.SessionOpened -= OnSessionOpened;
                Session.SessionClosed -= ClearCancellation;
                _subscribed = false;
            }
            ClearCancellation();
        }

        /// <summary>
        /// true 表示门移动目标有效；hold 仅在整个 body 已进门且抵达门中心附近时为 true。
        /// 根命令源收到 hold 后可停车，但必须继续逐次读取，真人离门/取消后立即撤销。
        /// </summary>
        public bool TryGetGoal(out Vector2 destination, out bool hold)
        {
            destination = default;
            hold = false;
            if (!_subscribed || !isActiveAndEnabled ||
                (Session.Phase != SessionPhase.Offline && Session.Phase != SessionPhase.Playing) ||
                (Session.Phase == SessionPhase.Playing && !Session.IsAuthority) ||
                IsHuman(Actor.Definition.Role) ||
                !Node.TryReadPortalGoal(Actor, Shape, out RestNodePortalGoal own)) return false;

            if (!Node.TryReadPortalGoal(Session.DeepSeek, null, out RestNodePortalGoal ds) ||
                !Node.TryReadPortalGoal(Session.Harness, null, out RestNodePortalGoal hs)) return false;
            RefreshCancellation(PlayerRole.DeepSeek, ds);
            RefreshCancellation(PlayerRole.Harness, hs);
            // 明确取消比“还站在门中”优先，包含托管期间用户仍可点击的取消按钮。
            if (_cancelledRoles != 0) return false;

            bool dsHuman = IsHuman(PlayerRole.DeepSeek);
            bool hsHuman = IsHuman(PlayerRole.Harness);
            bool wantsDeparture = (!dsHuman && !hsHuman) ||
                (dsHuman && (ds.Ready || ds.IsInside)) || (hsHuman && (hs.Ready || hs.IsInside));
            if (!wantsDeparture) return false;

            destination = own.Destination;
            hold = own.IsInside && own.FullyInside &&
                ((Vector2)Actor.transform.position - destination).sqrMagnitude <= Config.ArrivalRadius * Config.ArrivalRadius;
            return true;
        }

        /// <summary>没有持久追门路径可重置；控制权切换不能撤销真人明确的取消，离门/重准备才解除。</summary>
        public void ResetIntent()
        {
            if (Node == null || Node.State != RestNodeState.Open) ClearCancellation();
        }

        private bool IsHuman(PlayerRole role)
        {
            if (Session.Phase == SessionPhase.Playing || Session.IsSoloPlaying)
                return Session.GetRoleControl(role) == SlotControl.Human;
            return Assignment.CurrentLocalPlayerRole == role;
        }

        private void OnReadyCancelled(PlayerRole role)
        {
            if (role == PlayerRole.DeepSeek || role == PlayerRole.Harness)
                _cancelledRoles |= (byte)(1 << (int)role);
        }

        private void RefreshCancellation(PlayerRole role, RestNodePortalGoal goal)
        {
            if (!goal.IsInside || goal.Ready) _cancelledRoles &= (byte)~(1 << (int)role);
        }

        private void OnNodeStateChanged(RestNodeState state)
        {
            if (state != RestNodeState.Open) ClearCancellation();
        }

        private void OnSessionOpened(bool authority) => ClearCancellation();
        private void ClearCancellation() => _cancelledRoles = 0;
    }
}
