using System;
using System.IO;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Progression.Run;
using UnityEngine;
using Fact = DeepSleep.Runtime.Networking.NetworkMessageCatalog.CombatPresentationKind;

namespace DeepSleep.Runtime.Presentation.Audio
{
    /// <summary>
    /// 场景战斗声音入口。既有可靠表现只消费一次；补充消息只传原通道没有的动作事实。
    /// 不读对象出生/消失猜命中，不修改玩法、Unity 随机数或网络权威。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed partial class CombatAudioPresenter : MonoBehaviour
    {
        private const byte PresentationMessage = NetworkMessageCatalog.Authority.CombatPresentation;
        // 已校验、去重的权威事实也供成就读取；不依赖音量或声音是否成功播放。
        public event Action<NetworkMessageCatalog.CombatPresentationKind> FactPresented;
        public CoopSessionController Session;
        public ChapterRunController Chapter;
        public DeepSeekRiceAutoShooter Shooter;
        public RiceProjectilePool Rice;
        public HarnessTerminalLaserController Laser;
        public HarnessTerminalLaserDamageExecutor2D LaserDamage;
        public HarnessMeleeController Melee;
        public HarnessMeleeDamageExecutor2D MeleeDamage;
        public DeepSeekRiceGuardController Guard;
        public PlayerDamageReceiver2D DeepSeekDamage, HarnessDamage;
        public PlayerLifeStateController2D DeepSeekLife, HarnessLife;
        public PlayerReviveCoordinator2D DeepSeekRevive, HarnessRevive;
        public PlayerReviveActionChannel DeepSeekReviveChannel, HarnessReviveChannel;
        public NetworkCombatFeedbackChannel CombatFeedback;
        public NetworkPlayerHitFeedbackChannel PlayerFeedback;
        public EnemyActorPool2D[] EnemyPools = Array.Empty<EnemyActorPool2D>();
        public EnemyProjectilePool2D[] EnemyProjectiles = Array.Empty<EnemyProjectilePool2D>();
        [Tooltip("本关没有豆包遭遇时留空。")]
        public DoubaoWordWallEncounter2D Encounter;

        private uint _context, _sequence, _remoteContext, _lastReceived;
        private bool _received, _worldActive, _remoteWorld, _subscribed, _exiting;
        private bool _chargeSuppressed;
        private bool _wasCharging;
        private int _chargeHandle;
        private readonly int[] _reviveHandles = new int[2];
        private readonly int[] _rescueTargets = { -1, -1 };
        private readonly bool[] _downed = new bool[2];
        private readonly float[] _nextFactTime = new float[(int)Fact.SceneStateEnd + 1];
        private float _nextHsImpact;

        private GameAudioService Audio => GameAppRoot.Instance != null ? GameAppRoot.Instance.Audio : null;
        private bool IsReplica => Session != null && Session.Phase == SessionPhase.Playing && !Session.IsAuthority;
        private bool WorldAllowed => !_exiting && Session != null && !Session.IsExiting &&
            Chapter != null && Chapter.Phase == ChapterRunPhase.Combat &&
            Chapter.CombatWorld != null && Chapter.CombatWorld.Gate.CombatAllowed &&
            (Session.IsSoloPlaying || Session.Phase == SessionPhase.Playing);
        private bool CanPresent => WorldAllowed && (!IsReplica || _remoteWorld);

        public bool TryValidateConfiguration(out string reason)
        {
            reason = Session == null || Chapter == null || Shooter == null || Rice == null ||
                Laser == null || LaserDamage == null || Melee == null || MeleeDamage == null || Guard == null ||
                DeepSeekDamage == null || HarnessDamage == null || DeepSeekLife == null || HarnessLife == null ||
                DeepSeekRevive == null || HarnessRevive == null || DeepSeekReviveChannel == null ||
                HarnessReviveChannel == null || CombatFeedback == null || PlayerFeedback == null
                ? "核心武器、双方生命/救援及现有网络表现通道须显式装配。" : string.Empty;
            return reason.Length == 0;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[CombatAudio] " + reason, this); enabled = false; return; }
            ResetSession();
            Shooter.VolleyFired += OnVolley;
            Rice.DirectHitConfirmed += OnRiceHit;
            Rice.SplashConfirmed += OnSplash;
            Laser.FireRequested += OnLaserFire;
            LaserDamage.HitConfirmed += OnLaserHit;
            Melee.SwingStarted += OnSwing;
            Melee.WaveRequested += OnWave;
            MeleeDamage.DamageConfirmed += OnMeleeHit;
            Guard.Activated += OnGuardStarted;
            Guard.ChargeConsumed += OnGuardBlock;
            Guard.Ended += OnGuardEnded;
            DeepSeekDamage.DamageAccepted += OnDamage;
            HarnessDamage.DamageAccepted += OnDamage;
            DeepSeekLife.StateChanged += OnLifeState;
            HarnessLife.StateChanged += OnLifeState;
            DeepSeekReviveChannel.ChannelStarted += OnDeepSeekRescueStarted;
            HarnessReviveChannel.ChannelStarted += OnHarnessRescueStarted;
            DeepSeekReviveChannel.ChannelCancelled += OnDeepSeekRescueCancelled;
            HarnessReviveChannel.ChannelCancelled += OnHarnessRescueCancelled;
            DeepSeekRevive.ReviveCompleted += OnDeepSeekRevived;
            HarnessRevive.ReviveCompleted += OnHarnessRevived;
            CombatFeedback.WeaponImpactReceived += OnReplicaWeaponHit;
            PlayerFeedback.HitReceived += OnReplicaPlayerHit;
            Session.AuthorityMessage += Read;
            Session.SessionOpened += OnSessionOpened;
            Session.SessionClosed += ResetSession;
            Session.SceneExitStarted += OnSceneExit;
            Session.PeerJoined += OnPeerJoined;
            foreach (var pool in EnemyPools) if (pool != null && pool.GetComponent<EnemyContentAudio2D>() == null)
            { pool.ActorDespawned += OnEnemyDespawned; pool.ActorContactImpacted += OnEnemyDespawned; }
            foreach (var pool in EnemyProjectiles) if (pool != null) pool.ProjectileFired += OnSnakeFired;
            if (Encounter != null)
            {
                Encounter.BubblePopped += OnBubble;
                Encounter.BubbleImpacted += OnBubbleImpact;
                Encounter.FirstGroupAppeared += OnEncounterStarted;
                Encounter.BossRevealed += OnBossRevealed;
                Encounter.Completed += OnBossDefeated;
            }
            _subscribed = true;
            SubscribeKimi();
            SubscribeClaude();
        }

        private void OnDisable()
        {
            StopLoops();
            if (!_subscribed) return;
            UnsubscribeKimi();
            UnsubscribeClaude();
            Shooter.VolleyFired -= OnVolley;
            Rice.DirectHitConfirmed -= OnRiceHit;
            Rice.SplashConfirmed -= OnSplash;
            Laser.FireRequested -= OnLaserFire;
            LaserDamage.HitConfirmed -= OnLaserHit;
            Melee.SwingStarted -= OnSwing;
            Melee.WaveRequested -= OnWave;
            MeleeDamage.DamageConfirmed -= OnMeleeHit;
            Guard.Activated -= OnGuardStarted;
            Guard.ChargeConsumed -= OnGuardBlock;
            Guard.Ended -= OnGuardEnded;
            DeepSeekDamage.DamageAccepted -= OnDamage;
            HarnessDamage.DamageAccepted -= OnDamage;
            DeepSeekLife.StateChanged -= OnLifeState;
            HarnessLife.StateChanged -= OnLifeState;
            DeepSeekReviveChannel.ChannelStarted -= OnDeepSeekRescueStarted;
            HarnessReviveChannel.ChannelStarted -= OnHarnessRescueStarted;
            DeepSeekReviveChannel.ChannelCancelled -= OnDeepSeekRescueCancelled;
            HarnessReviveChannel.ChannelCancelled -= OnHarnessRescueCancelled;
            DeepSeekRevive.ReviveCompleted -= OnDeepSeekRevived;
            HarnessRevive.ReviveCompleted -= OnHarnessRevived;
            CombatFeedback.WeaponImpactReceived -= OnReplicaWeaponHit;
            PlayerFeedback.HitReceived -= OnReplicaPlayerHit;
            Session.AuthorityMessage -= Read;
            Session.SessionOpened -= OnSessionOpened;
            Session.SessionClosed -= ResetSession;
            Session.SceneExitStarted -= OnSceneExit;
            Session.PeerJoined -= OnPeerJoined;
            foreach (var pool in EnemyPools) if (pool != null)
            { pool.ActorDespawned -= OnEnemyDespawned; pool.ActorContactImpacted -= OnEnemyDespawned; }
            foreach (var pool in EnemyProjectiles) if (pool != null) pool.ProjectileFired -= OnSnakeFired;
            if (Encounter != null)
            {
                Encounter.BubblePopped -= OnBubble;
                Encounter.BubbleImpacted -= OnBubbleImpact;
                Encounter.FirstGroupAppeared -= OnEncounterStarted;
                Encounter.BossRevealed -= OnBossRevealed;
                Encounter.Completed -= OnBossDefeated;
            }
            _subscribed = false;
        }

        private void Update()
        {
            EnsureContext();
            ObserveWorld02Audio();
            if (!CanPresent) { StopLoops(); return; }
            // 客人校准状态由原 WeaponState 更新；不要求它补发 StateChanged。
            if (Laser.State != HarnessTerminalLaserState.Calibrating) _chargeSuppressed = false;
            bool charging = !_chargeSuppressed && Laser.State == HarnessTerminalLaserState.Calibrating && Laser.RemainingStateSeconds > 0f;
            if (charging && !_wasCharging && Audio != null)
                _chargeHandle = Audio.PlayTracked(AudioCue.HsCharge, Laser.BeamOriginPosition, 2, WeaponGain(PlayerRole.Harness));
            else if (!charging) StopCharge();
            _wasCharging = charging;
        }

        private void EnsureContext()
        {
            if (IsReplica) return;
            bool active = WorldAllowed;
            if (active == _worldActive) return;
            _worldActive = active;
            unchecked { _context++; }
            StopLoops();
            Array.Clear(_downed, 0, _downed.Length);
            Array.Clear(_nextFactTime, 0, _nextFactTime.Length);
            Send(active ? Fact.ContextStarted : Fact.ContextStopped, PlayerRole.DeepSeek, Vector2.zero);
        }

        private void Publish(Fact fact, PlayerRole role, Vector2 point, float minimumInterval = 0f)
        {
            if (IsReplica) return;
            EnsureContext();
            if (!CanPresent) return;
            int index = (int)fact;
            if (Time.unscaledTime < _nextFactTime[index]) return;
            _nextFactTime[index] = Time.unscaledTime + minimumInterval;
            Present(fact, role, point);
            Send(fact, role, point);
        }

        private void Send(Fact fact, PlayerRole role, Vector2 point)
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing) return;
            Session.SendAuthority(PresentationMessage, writer =>
            {
                writer.Write(_context); writer.Write(++_sequence);
                writer.Write((byte)fact); writer.Write((byte)role);
                writer.Write(point.x); writer.Write(point.y);
            }, true);
        }

        private void Read(byte message, BinaryReader reader)
        {
            if (message != PresentationMessage || !IsReplica ||
                !NetworkMessageCatalog.TryValidatePayload(message, NetworkMessageCatalog.Direction.AuthorityToPeer, reader, out _)) return;
            uint context = reader.ReadUInt32(), sequence = reader.ReadUInt32();
            Fact fact = (Fact)reader.ReadByte(); PlayerRole role = (PlayerRole)reader.ReadByte();
            Vector2 point = new(reader.ReadSingle(), reader.ReadSingle());
            if (_received && !RemoteCommandSource.IsNewer(sequence, _lastReceived)) return;
            _received = true; _lastReceived = sequence;
            if (fact == Fact.ContextStarted || fact == Fact.ContextStopped)
            {
                _remoteContext = context; _remoteWorld = fact == Fact.ContextStarted;
                _chargeSuppressed = false;
                Array.Clear(_downed, 0, _downed.Length);
                StopLoops();
                return;
            }
            if (!_remoteWorld || context != _remoteContext || !CanPresent) return;
            Present(fact, role, point);
        }

        private void Present(Fact fact, PlayerRole role, Vector2 point)
        {
            FactPresented?.Invoke(fact);
            if (fact >= Fact.KimiReveal && fact <= Fact.SceneStateEnd)
            {
                var cue = (AudioCue)((int)AudioCue.KimiReveal + (int)fact - (int)Fact.KimiReveal);
                if (cue == AudioCue.KimiFlute)
                { StopKimiFlute(); if (Audio != null) _fluteHandle = Audio.StartLoop(cue, point, (int)cue); return; }
                if (cue == AudioCue.KimiTide || cue == AudioCue.KimiInterrupt || cue == AudioCue.KimiDefeat || cue == AudioCue.KimiPhase)
                    StopKimiFlute();
                Play(cue, point, role);
                return;
            }
            switch (fact)
            {
                case Fact.RiceVolley: Play(AudioCue.DsShot, point, role, WeaponGain(role)); break;
                case Fact.RiceDirectHit: Play(AudioCue.DsHit, point, role, WeaponGain(role)); break;
                case Fact.RiceSplash: Play(AudioCue.DsSplash, point, role, WeaponGain(role)); break;
                case Fact.GuardStarted: Play(AudioCue.GuardStart, point, role); break;
                case Fact.GuardEnded: Play(AudioCue.GuardEnd, point, role, .65f); break;
                case Fact.SlashDown: Play(AudioCue.HsSlashDown, point, role, WeaponGain(role)); break;
                case Fact.SlashUp: Play(AudioCue.HsSlashUp, point, role, WeaponGain(role)); break;
                case Fact.SlashSweep: Play(AudioCue.HsSlashSweep, point, role, WeaponGain(role)); break;
                case Fact.PlayerDown:
                    _downed[(int)role] = true;
                    StopRevive(0); StopRevive(1);
                    Play(AudioCue.PlayerDown, point, role); break;
                case Fact.ReviveStarted: StartRevive((int)role, point); break;
                case Fact.ReviveCancelled: StopRevive((int)role); break;
                case Fact.ReviveCompleted:
                    _downed[(int)role] = false;
                    StopRevive((int)role); Play(AudioCue.ReviveDone, point, role); break;
                case Fact.BubblePopped: Play(AudioCue.BubblePop, point, role, .7f); break;
                case Fact.BossRevealed: Play(AudioCue.DoubaoReveal, point, role); break;
                case Fact.BossDefeated: Play(AudioCue.DoubaoDefeat, point, role); break;
                case Fact.EnemyDefeated: Play(AudioCue.EnemyDefeat, point, role, .7f); break;
                case Fact.SnakeFired: Play(AudioCue.SnakeShot, point, role, .7f); break;
                case Fact.BubbleImpacted: Play(AudioCue.BubbleImpact, point, role, .6f); break;
                case Fact.EncounterStarted: Play(AudioCue.DoubaoReveal, point, role, .4f); break;
            }
        }

        private void OnVolley(Vector2 point) => Publish(Fact.RiceVolley, PlayerRole.DeepSeek, point);
        private void OnRiceHit(RiceProjectileHitConfirmed hit)
        {
            if (!IsBubble(hit.Hitbox)) Publish(Fact.RiceDirectHit, PlayerRole.DeepSeek, hit.HitPoint, .045f);
        }
        private void OnSplash(Vector2 point, Vector2 direction) => Publish(Fact.RiceSplash, PlayerRole.DeepSeek, point, .065f);
        private void OnGuardStarted() => Publish(Fact.GuardStarted, PlayerRole.DeepSeek, Guard.transform.position);
        private void OnGuardEnded()
        {
            // OnDisable/倒地的取消不是破盾或自然到期。
            if (Guard.isActiveAndEnabled && DeepSeekLife.State == PlayerLifeState.Alive)
                Publish(Fact.GuardEnded, PlayerRole.DeepSeek, Guard.transform.position);
        }
        private void OnGuardBlock(PlayerDamageReceiver2D target, DamagePacket packet, int remaining)
        {
            EnsureContext();
            if (CanPresent) Play(AudioCue.GuardBlock, packet.HitPoint, PlayerRole.DeepSeek);
        }
        private void OnLaserFire(HarnessTerminalLaserFireRequest request)
        {
            EnsureContext(); StopCharge(); _chargeSuppressed = true;
            if (CanPresent && request != null && request.IsValid)
                Play(AudioCue.HsFire, request.BeamSnapshot.SourceOrigin, PlayerRole.Harness, WeaponGain(PlayerRole.Harness));
        }
        private void OnSwing()
        {
            if (IsReplica || Melee.Attack == null || Melee.Config == null) return;
            // 当前三招配置的顺序为下劈、上挑、横扫；比较姿态可识别运行时升级副本。
            var attacks = Melee.Config.Attacks;
            for (int i = 0; i < attacks.Length; i++)
                if (attacks[i].CharacterPose == Melee.Attack.CharacterPose)
                {
                    Publish(i == 0 ? Fact.SlashDown : i == 1 ? Fact.SlashUp : Fact.SlashSweep,
                        PlayerRole.Harness, Melee.transform.position);
                    return;
                }
        }
        private void OnWave(HarnessMeleeAttackConfig attack, Vector2 origin, float angle)
        {
            EnsureContext();
            if (CanPresent && attack != null)
                Play(AudioCue.HsWave, origin, PlayerRole.Harness,
                    WeaponGain(PlayerRole.Harness) * (attack.RuntimeEchoIndex > 0 ? .3f : .55f));
        }
        private static bool IsBubble(DamageHitbox2D hitbox) =>
            hitbox != null && hitbox.TryGetReceiver(out var receiver) && receiver is DoubaoWordWallBlock2D;
        private void OnLaserHit(HarnessTerminalLaserHitConfirmed hit)
        { if (!IsReplica && !IsBubble(hit.Hitbox)) WeaponImpact(hit.HitPoint); }
        private void OnMeleeHit(HarnessMeleeDamageHitConfirmed hit)
        { if (!IsReplica && !hit.IsSurface) WeaponImpact(hit.HitPoint); }
        private void OnReplicaWeaponHit(Vector2 point) { if (IsReplica) WeaponImpact(point); }
        private void WeaponImpact(Vector2 point)
        {
            EnsureContext();
            if (!CanPresent || Time.unscaledTime < _nextHsImpact) return;
            _nextHsImpact = Time.unscaledTime + .055f;
            Play(AudioCue.HsHit, point, PlayerRole.Harness, WeaponGain(PlayerRole.Harness));
        }
        private void OnDamage(PlayerDamageReceiver2D receiver, DamagePacket damage)
        {
            if (IsReplica) return;
            PlayerRole role = receiver == DeepSeekDamage ? PlayerRole.DeepSeek : PlayerRole.Harness;
            if (Life(role).State == PlayerLifeState.Downed) return;
            Hurt(role);
        }
        private void OnReplicaPlayerHit(PlayerRole role) { if (IsReplica) Hurt(role); }
        private void Hurt(PlayerRole role)
        {
            EnsureContext();
            if (CanPresent && !_downed[(int)role])
                Play(role == PlayerRole.DeepSeek ? AudioCue.PlayerHurtDs : AudioCue.PlayerHurtHs,
                    Life(role).transform.position, role);
        }
        private void OnLifeState(PlayerLifeStateController2D life, PlayerLifeState state)
        {
            if (state == PlayerLifeState.Downed)
                Publish(Fact.PlayerDown, life == DeepSeekLife ? PlayerRole.DeepSeek : PlayerRole.Harness, life.transform.position);
        }
        private void OnDeepSeekRescueStarted() => RescueStarted(0, DeepSeekReviveChannel);
        private void OnHarnessRescueStarted() => RescueStarted(1, HarnessReviveChannel);
        private void RescueStarted(int owner, PlayerReviveActionChannel channel)
        {
            if (IsReplica || channel.OfferedTarget == null) return;
            PlayerRole role = channel.OfferedTarget == DeepSeekLife ? PlayerRole.DeepSeek : PlayerRole.Harness;
            _rescueTargets[owner] = (int)role;
            Publish(Fact.ReviveStarted, role, channel.OfferedTarget.transform.position);
        }
        private void OnDeepSeekRescueCancelled() => RescueCancelled(0);
        private void OnHarnessRescueCancelled() => RescueCancelled(1);
        private void RescueCancelled(int owner)
        {
            int target = _rescueTargets[owner]; _rescueTargets[owner] = -1;
            if (target >= 0) Publish(Fact.ReviveCancelled, (PlayerRole)target, Life((PlayerRole)target).transform.position);
        }
        private void OnDeepSeekRevived(PlayerActor rescuer) => Publish(Fact.ReviveCompleted, PlayerRole.DeepSeek, DeepSeekLife.transform.position);
        private void OnHarnessRevived(PlayerActor rescuer) => Publish(Fact.ReviveCompleted, PlayerRole.Harness, HarnessLife.transform.position);
        private void OnBubble(Vector2 point) => Publish(Fact.BubblePopped, PlayerRole.DeepSeek, point, .06f);
        private void OnBubbleImpact(Vector2 point) => Publish(Fact.BubbleImpacted, PlayerRole.DeepSeek, point, .055f);
        private void OnEncounterStarted(Vector2 point) => Publish(Fact.EncounterStarted, PlayerRole.DeepSeek, point);
        private void OnBossRevealed(Vector2 point) => Publish(Fact.BossRevealed, PlayerRole.DeepSeek, point);
        private void OnBossDefeated(DoubaoWordWallEncounter2D encounter) =>
            Publish(Fact.BossDefeated, PlayerRole.DeepSeek, encounter.Boss.transform.position);
        private void OnEnemyDespawned(EnemyDespawnRequest2D request)
        {
            if (request.Reason == EnemyDespawnReason.Defeated && !request.SuppressKillReward)
                Publish(Fact.EnemyDefeated, PlayerRole.DeepSeek, request.EffectPosition, .065f);
        }
        private void OnSnakeFired(Vector2 point) => Publish(Fact.SnakeFired, PlayerRole.DeepSeek, point, .045f);

        private void StartRevive(int role, Vector2 point)
        {
            // 本批只有有限起音，没有无缝持续素材；维持阶段静音，重连不补播历史起音。
            if (_reviveHandles[role] == 0 && Audio != null)
                _reviveHandles[role] = Audio.PlayTracked(AudioCue.ReviveStart, point, role + 1);
        }
        private void StopRevive(int role)
        {
            if (_reviveHandles[role] != 0) Audio?.StopLoop(_reviveHandles[role]);
            _reviveHandles[role] = 0;
        }
        private void StopCharge()
        {
            if (_chargeHandle != 0) Audio?.StopLoop(_chargeHandle);
            _chargeHandle = 0; _wasCharging = false;
        }
        private void StopLoops() { StopCharge(); StopRevive(0); StopRevive(1); StopKimiFlute(); }
        private void Play(AudioCue cue, Vector2 point, PlayerRole role, float gain = 1f) => Audio?.Play(cue, point, (int)role + 1, gain);
        private PlayerLifeStateController2D Life(PlayerRole role) => role == PlayerRole.DeepSeek ? DeepSeekLife : HarnessLife;
        private float WeaponGain(PlayerRole role)
        {
            PlayerRole local = Session.Phase == SessionPhase.Playing ? Session.LocalRole : Session.Assignment.CurrentLocalPlayerRole;
            return role == local ? 1f : .7f;
        }
        private void OnSessionOpened(bool authority) => ResetSession();
        private void OnPeerJoined()
        {
            EnsureContext();
            if (Session.IsAuthority && _worldActive) Send(Fact.ContextStarted, PlayerRole.DeepSeek, Vector2.zero);
        }
        private void OnSceneExit() { _exiting = true; EnsureContext(); StopLoops(); }
        private void ResetSession()
        {
            StopLoops();
            _context = _sequence = _remoteContext = _lastReceived = 0;
            _received = _worldActive = _remoteWorld = _chargeSuppressed = _exiting = false;
            _rescueTargets[0] = _rescueTargets[1] = -1;
            Array.Clear(_downed, 0, _downed.Length);
            _nextHsImpact = 0f;
            Array.Clear(_nextFactTime, 0, _nextFactTime.Length);
        }
    }
}
