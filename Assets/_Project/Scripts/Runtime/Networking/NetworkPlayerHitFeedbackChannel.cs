using System.IO;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>
    /// 只传递主机已经扣血的受击事实；客人播放角色与本机反馈，不重新结算生命或无敌时间。
    /// 显式依赖同一会话、DeepSeek 与 Harness 两个受击表现组件。
    /// </summary>
    public sealed class NetworkPlayerHitFeedbackChannel : MonoBehaviour
    {
        private const byte PLAYER_HIT_FEEDBACK = NetworkMessageCatalog.Authority.PlayerHitFeedback;
        // 网络契约的拒收边界，不是角色的无敌调参；避免坏包制造无限闪烁。
        private const float MAXIMUM_PROTECTION_SECONDS = 10f;
        private const float DIRECTION_COMPONENT_LIMIT = 1.001f;

        public CoopSessionController Session;
        public PlayerHitFeedbackPresenter2D DeepSeek;
        public PlayerHitFeedbackPresenter2D Harness;

        private uint _sequence;
        private uint _lastReceived;
        private bool _received;
        private bool _subscribed;
        /// <summary>已校验、已去重的客人受伤事实，供额外表现复用同一消息。</summary>
        public event System.Action<PlayerRole> HitReceived;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Session == null || DeepSeek == null || Harness == null ||
                DeepSeek == Harness || DeepSeek.Receiver == null || Harness.Receiver == null ||
                DeepSeek.Receiver == Harness.Receiver)
            {
                reason = "Session、DeepSeek、Harness 或独立受伤入口未完整装配。";
                return false;
            }
            if (!MatchesRole(DeepSeek, PlayerRole.DeepSeek) || !MatchesRole(Harness, PlayerRole.Harness))
            {
                reason = "DeepSeek/Harness 表现槽位与 Actor.Definition.Role 不一致。";
                return false;
            }
            if (DeepSeek.Session != Session || Harness.Session != Session ||
                DeepSeek.Actor != Session.DeepSeek || Harness.Actor != Session.Harness)
            {
                reason = "受击表现与网络通道必须引用同一会话及该会话的角色。";
                return false;
            }
            if (!DeepSeek.TryValidateConfiguration(out reason) || !Harness.TryValidateConfiguration(out reason))
                return false;
            reason = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError("[NetworkPlayerHitFeedback] " + name + ": " + reason, this);
                enabled = false;
                return;
            }
            ResetState();
            DeepSeek.Receiver.DamageAccepted += OnDamageAccepted;
            Harness.Receiver.DamageAccepted += OnDamageAccepted;
            Session.AuthorityMessage += Read;
            Session.SessionOpened += OnSessionOpened;
            Session.SessionClosed += ResetState;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                if (DeepSeek != null && DeepSeek.Receiver != null)
                    DeepSeek.Receiver.DamageAccepted -= OnDamageAccepted;
                if (Harness != null && Harness.Receiver != null)
                    Harness.Receiver.DamageAccepted -= OnDamageAccepted;
                if (Session != null)
                {
                    Session.AuthorityMessage -= Read;
                    Session.SessionOpened -= OnSessionOpened;
                    Session.SessionClosed -= ResetState;
                }
                _subscribed = false;
            }
            ResetState();
        }

        private void OnSessionOpened(bool authority) => ResetState();

        private void ResetState()
        {
            _sequence = _lastReceived = 0;
            _received = false;
            if (DeepSeek != null) DeepSeek.ResetFeedback();
            if (Harness != null) Harness.ResetFeedback();
        }

        private void OnDamageAccepted(PlayerDamageReceiver2D receiver, DamagePacket damage)
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing) return;
            PlayerRole role;
            if (receiver == DeepSeek.Receiver) role = PlayerRole.DeepSeek;
            else if (receiver == Harness.Receiver) role = PlayerRole.Harness;
            else return;

            Vector2 direction = damage.Direction;
            float protection = receiver.RemainingInvulnerabilitySeconds;
            if (!ValidPayload(direction, protection)) return;
            Session.SendAuthority(PLAYER_HIT_FEEDBACK, writer =>
            {
                writer.Write(++_sequence);
                writer.Write((byte)role);
                writer.Write(direction.x);
                writer.Write(direction.y);
                writer.Write(protection);
            }, true);
        }

        private void Read(byte message, BinaryReader reader)
        {
            if (message != PLAYER_HIT_FEEDBACK || Session.IsAuthority ||
                Session.Phase != SessionPhase.Playing) return;

            // 未完整读完并通过校验的包不能推进去重序号。截断包由会话层统一拒绝。
            uint sequence = reader.ReadUInt32();
            PlayerRole role = (PlayerRole)reader.ReadByte();
            Vector2 direction = new(reader.ReadSingle(), reader.ReadSingle());
            float protection = reader.ReadSingle();
            if ((role != PlayerRole.DeepSeek && role != PlayerRole.Harness) ||
                !ValidPayload(direction, protection) ||
                reader.BaseStream.Position != reader.BaseStream.Length) return;
            if (_received && !RemoteCommandSource.IsNewer(sequence, _lastReceived)) return;

            _received = true;
            _lastReceived = sequence;
            HitReceived?.Invoke(role);
            (role == PlayerRole.DeepSeek ? DeepSeek : Harness).PlayReplica(direction, protection);
        }

        private static bool MatchesRole(PlayerHitFeedbackPresenter2D presenter, PlayerRole role) =>
            presenter.Actor != null && presenter.Actor.Definition != null &&
            presenter.Actor.Definition.Role == role;

        private static bool ValidPayload(Vector2 direction, float protection) =>
            Finite(direction.x) && Finite(direction.y) && Finite(protection) &&
            Mathf.Abs(direction.x) <= DIRECTION_COMPONENT_LIMIT &&
            Mathf.Abs(direction.y) <= DIRECTION_COMPONENT_LIMIT &&
            protection >= 0f && protection <= MAXIMUM_PROTECTION_SECONDS;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
