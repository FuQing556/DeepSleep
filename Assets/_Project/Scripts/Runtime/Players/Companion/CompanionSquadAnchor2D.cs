using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>双托管共享一个稳定编队中心，不让两个相同偏移的跟随目标互相推着漂移。</summary>
    public sealed class CompanionSquadAnchor2D : MonoBehaviour
    {
        public CoopSessionController Session;
        public Transform DeepSeek;
        public Transform Harness;
        private bool _active;
        private Vector2 _anchor;

        private void OnEnable()
        {
            if (Session == null) return;
            Session.SessionClosed += ResetAnchor;
            Session.SessionOpened += OnSessionOpened;
        }
        private void OnDisable()
        {
            if (Session != null)
            {
                Session.SessionClosed -= ResetAnchor;
                Session.SessionOpened -= OnSessionOpened;
            }
            ResetAnchor();
        }
        private void OnSessionOpened(bool authority) => ResetAnchor();
        private void ResetAnchor() => _active = false;

        public bool TryGetGoal(PlayerRole role, Vector2 formationOffset, out Vector2 destination)
        {
            destination = default;
            bool both = Session != null && (Session.IsSoloPlaying ||
                (Session.Phase == SessionPhase.Playing && Session.IsAuthority)) &&
                Session.GetRoleControl(PlayerRole.DeepSeek) != SlotControl.Human &&
                Session.GetRoleControl(PlayerRole.Harness) != SlotControl.Human;
            if (!both || DeepSeek == null || Harness == null) { _active = false; return false; }
            if (!_active) { _anchor = ((Vector2)DeepSeek.position + (Vector2)Harness.position) * .5f; _active = true; }
            destination = _anchor + (role == PlayerRole.DeepSeek ? formationOffset : -formationOffset) * .5f;
            return true;
        }
    }
}
