using System.IO;
using System.Text;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>可靠传递成功命中反馈；房主保留本地订阅，客人只播放表现。</summary>
    public sealed class NetworkCombatFeedbackChannel : MonoBehaviour
    {
        private const byte Feedback = NetworkMessageCatalog.Authority.CombatFeedback;
        private enum Kind : byte { Rice, Laser, MeleeDamage, MeleeImpact }

        public CoopSessionController Session;
        public RiceProjectilePool Rice;
        public HarnessTerminalLaserDamageExecutor2D Laser;
        public HarnessMeleeDamageExecutor2D Melee;
        public HarnessLaserHitEffectPresenter2D Impacts;
        public CombatDamageNumberPresenter2D Numbers;
        private uint _sequence, _lastReceived;
        private bool _received, _subscribed;
        private bool _summaryPending, _diagnosticAuthority;
        // 通道计数按 SessionOpened 分段；池计数由 Presenter 自己维护，表示场景实例寿命累计。
        public uint ReceivedCount { get; private set; }
        public uint AcceptedCount { get; private set; }
        public uint PlayedCount { get; private set; }
        public uint DuplicateDropCount { get; private set; }
        public uint PhaseDropCount { get; private set; }

        public bool TryValidateConfiguration(out string reason)
        {
            reason = Session == null || Rice == null || Laser == null || Melee == null ||
                Impacts == null || Numbers == null ? "命中反馈的会话、伤害来源或表现引用不完整。" : string.Empty;
            return reason.Length == 0;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError("[NetworkCombatFeedback] " + reason, this);
                enabled = false;
                return;
            }
            Rice.HitConfirmed += OnRice;
            Laser.HitConfirmed += OnLaser;
            Melee.DamageConfirmed += OnMeleeDamage;
            Melee.HitConfirmed += OnMeleeImpact;
            Session.AuthorityMessage += Read;
            Session.SessionOpened += OnOpened;
            Session.SessionClosed += OnClosed;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                Rice.HitConfirmed -= OnRice;
                Laser.HitConfirmed -= OnLaser;
                Melee.DamageConfirmed -= OnMeleeDamage;
                Melee.HitConfirmed -= OnMeleeImpact;
                Session.AuthorityMessage -= Read;
                Session.SessionOpened -= OnOpened;
                Session.SessionClosed -= OnClosed;
                _subscribed = false;
            }
            Reset();
        }

        private void OnOpened(bool authority)
        {
            Reset();
            ReceivedCount = AcceptedCount = PlayedCount = DuplicateDropCount = PhaseDropCount = 0;
            _diagnosticAuthority = authority;
            _summaryPending = true;
        }

        private void OnClosed()
        {
            if (_summaryPending)
            {
                _summaryPending = false;
                Debug.Log("[NetworkCombatFeedback] Session closed. " + DescribeDiagnostics(), this);
            }
            // 保留诊断计数直到下一次开房/加入，便于退房后读取；OnDisable 不再次输出。
            Reset();
        }

        private void Reset() { _sequence = _lastReceived = 0; _received = false; }

        /// <summary>固定字段、有界摘要，无逐命中记录、坐标、地址或票据；可在移动包运行期读取。</summary>
        public string DescribeDiagnostics()
        {
            var report = new StringBuilder(512);
            report.Append("channel(session): authority=").Append(_diagnosticAuthority)
                .Append(", enabled=").Append(isActiveAndEnabled)
                .Append(", received=").Append(ReceivedCount)
                .Append(", accepted=").Append(AcceptedCount)
                .Append(", playedAnyView=").Append(PlayedCount)
                .Append(", duplicateDrop=").Append(DuplicateDropCount)
                .Append(", phaseDrop=").Append(PhaseDropCount);
            if (Impacts == null) report.Append("; impacts=missing");
            else report.Append("; impacts(sceneLifetime): enabled=").Append(Impacts.isActiveAndEnabled)
                .Append(", requested=").Append(Impacts.RequestedCount)
                .Append(", played=").Append(Impacts.PlayedCount)
                .Append(", disabledDrop=").Append(Impacts.DisabledDropCount)
                .Append(", activeViews=").Append(Impacts.ActiveCount);
            if (Numbers == null) report.Append("; numbers=missing");
            else report.Append("; numbers(sceneLifetime): enabled=").Append(Numbers.isActiveAndEnabled)
                .Append(", requested=").Append(Numbers.RequestedCount)
                .Append(", played=").Append(Numbers.PlayedCount)
                .Append(", disabledDrop=").Append(Numbers.DisabledDropCount)
                .Append(", capacityDrop=").Append(Numbers.CapacityDropCount)
                .Append(", invalidDrop=").Append(Numbers.InvalidDropCount)
                .Append(", activeViews=").Append(Numbers.ActiveCount);
            return report.ToString();
        }
        private void OnRice(RiceProjectileHitConfirmed hit)
            => Send(Kind.Rice, hit.HitPoint, hit.Direction, hit.DamageAmount, 0f);
        private void OnLaser(HarnessTerminalLaserHitConfirmed hit)
            => Send(Kind.Laser, hit.HitPoint, hit.Direction, hit.DamageAmount, hit.BeamWidth);
        private void OnMeleeDamage(HarnessMeleeDamageHitConfirmed hit)
            => Send(Kind.MeleeDamage, hit.HitPoint, hit.Direction, hit.DamageAmount, 0f);
        private void OnMeleeImpact(Vector2 point, float angle)
            => Send(Kind.MeleeImpact, point,
                MeleeSwordGeometry2D.Rotate(Vector2.right, angle), 0f, 0.15f);

        private void Send(Kind kind, Vector2 point, Vector2 direction, float amount, float width)
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing) return;
            // 与束体快照独立；不携带客户端没有的 Hitbox，也不依赖目标继续存活。
            Session.SendAuthority(Feedback, writer =>
            {
                writer.Write(++_sequence);
                writer.Write((byte)kind);
                writer.Write(point.x); writer.Write(point.y);
                writer.Write(direction.x); writer.Write(direction.y);
                writer.Write(amount); writer.Write(width);
            }, true);
        }

        private void Read(byte message, BinaryReader reader)
        {
            if (message != Feedback || Session.IsAuthority) return;
            ReceivedCount++;
            if (Session.Phase != SessionPhase.Playing) { PhaseDropCount++; return; }
            uint sequence = reader.ReadUInt32();
            Kind kind = (Kind)reader.ReadByte();
            Vector2 point = new(reader.ReadSingle(), reader.ReadSingle());
            Vector2 direction = new(reader.ReadSingle(), reader.ReadSingle());
            float amount = reader.ReadSingle(), width = reader.ReadSingle();
            if (kind > Kind.MeleeImpact || !Finite(point.x) || !Finite(point.y) ||
                !Finite(direction.x) || !Finite(direction.y) || !Finite(amount) || !Finite(width) ||
                amount < 0f || width < 0f) return;
            if (_received && !RemoteCommandSource.IsNewer(sequence, _lastReceived)) { DuplicateDropCount++; return; }
            _received = true; _lastReceived = sequence;
            AcceptedCount++;
            uint numbersBefore = Numbers.PlayedCount, impactsBefore = Impacts.PlayedCount;
            if (kind != Kind.MeleeImpact)
                Numbers.ShowReplicaDamage(point, amount, kind != Kind.Rice);
            if (kind == Kind.Laser || kind == Kind.MeleeImpact)
                Impacts.PlayImpact(point, direction, width);
            if (Numbers.PlayedCount != numbersBefore || Impacts.PlayedCount != impactsBefore) PlayedCount++;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
