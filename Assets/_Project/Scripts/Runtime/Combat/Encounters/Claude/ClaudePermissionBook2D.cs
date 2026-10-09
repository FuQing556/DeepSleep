using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;
using UnityEngine.UI;
using DeepSleep.Runtime.UI.Common;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    public enum ClaudePermission : byte { Movement, PrimaryAttack, Skill }
    public enum ClaudeBookState : byte { Hidden, Warning, Sealed }
    public enum ClaudeBookCloseReason : byte { Reset, Broken, Expired, PlayerDowned }

    /// <summary>一书的权威状态；客机只还原显示和本书门禁，不独立判伤或到期。</summary>
    public readonly struct ClaudeBookSnapshot
    {
        public readonly Vector2 Position;
        public readonly PlayerRole Role;
        public readonly ClaudePermission Permission;
        public readonly ClaudeBookState State;
        public readonly float Health, RemainingSeconds;
        public readonly bool Mirrored;

        public ClaudeBookSnapshot(Vector2 position, PlayerRole role, ClaudePermission permission,
            ClaudeBookState state, float health, float remainingSeconds, bool mirrored)
        {
            Position = position; Role = role; Permission = permission; State = state;
            Health = health; RemainingSeconds = remainingSeconds; Mirrored = mirrored;
        }

        public bool IsValid(ClaudePermissionConfig config)
        {
            if (config == null || (uint)Role > 1 || (uint)Permission > 2 || (uint)State > 2 ||
                !float.IsFinite(Position.x) || !float.IsFinite(Position.y) ||
                !float.IsFinite(Health) || !float.IsFinite(RemainingSeconds)) return false;
            if (State == ClaudeBookState.Hidden) return Health == 0 && RemainingSeconds == 0;
            float duration = State == ClaudeBookState.Warning ? config.WarningSeconds : config.SealSeconds;
            return Health > 0 && Health <= Mathf.Max(config.PhaseOneHealth, config.PhaseTwoHealth) &&
                RemainingSeconds > 0 && RemainingSeconds <= duration;
        }
    }

    /// <summary>一书一权限一门禁来源；四个固定实例复用，不生成组件或清除其他来源。</summary>
    public sealed class ClaudePermissionBook2D : MonoBehaviour, IDamageReceiver, IDamageFeedbackSource
    {
        public Collider2D Shape;
        public SpriteRenderer Visual;
        public SpriteRenderer Decoration;
        private void LateUpdate()
        {
            if (Decoration == null) return;
            Decoration.enabled = IsOpen;
            Decoration.transform.position = Visual.transform.position;
            Decoration.sortingLayerID = Visual.sortingLayerID;
            Decoration.sortingOrder = Visual.sortingOrder - 1;
        }
        public CombatPerceptionBody2D Perception;
        public Text Label;
        public Canvas LabelCanvas;
        public Image Progress;
        public Image IdentityCircle;
        public UiThemePalette DeepSeekPalette, HarnessPalette;
        public Color DeepSeekInk, HarnessInk;
        private PlayerActionGate _gate;
        private PlayerLifeStateController2D _life;
        private CoopSessionController _session;
        private CombatPerceptionRegistry2D _registry;
        private bool _authority;
        private float _sealSeconds;
        private float _warningSeconds;
        public PlayerRole Role { get; private set; }
        public ClaudePermission Permission { get; private set; }
        public ClaudeBookState State { get; private set; }
        public float CurrentHealth { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsOpen => State != ClaudeBookState.Hidden;
        public bool CanReceiveDamage => IsOpen && _authority && isActiveAndEnabled &&
            (_session == null || _session.Phase == SessionPhase.Offline || _session.IsAuthority);
        public event Action<DamagePacket> DamageAccepted;
        public event Action FeedbackReset;
        public event Action<ClaudePermissionBook2D, ClaudeBookCloseReason> Closed;

        public static PlayerActionBlock BlockFor(ClaudePermission permission) => permission switch
        {
            ClaudePermission.Movement => PlayerActionBlock.Movement,
            ClaudePermission.PrimaryAttack => PlayerActionBlock.PrimaryAttack,
            ClaudePermission.Skill => PlayerActionBlock.Skill,
            _ => PlayerActionBlock.None
        };

        public bool TryValidateConfiguration(out string reason)
        {
            if (Shape == null || !Shape.isTrigger || Visual == null || Visual.sprite == null ||
                Perception == null || Perception.Shape != Shape || Label == null || LabelCanvas == null || Progress == null ||
                IdentityCircle == null || DeepSeekPalette == null || HarnessPalette == null ||
                DeepSeekPalette.CircleBase == null || DeepSeekPalette.CircleFrame == null ||
                HarnessPalette.CircleBase == null || HarnessPalette.CircleFrame == null)
            { reason = "书本Trigger、图片、感知与非交互uGUI标签须显式装配。"; return false; }
            reason = string.Empty; return true;
        }

        public bool Open(Vector2 position, PlayerRole role, ClaudePermission permission,
            ClaudePermissionConfig config, bool phaseTwo, PlayerActionGate gate,
            PlayerLifeStateController2D life, CoopSessionController session, CombatPerceptionRegistry2D registry)
        {
            if (!isActiveAndEnabled || IsOpen || !TryValidateConfiguration(out _) || config == null || !config.TryValidate(out _) ||
                gate == null || life == null || life.State != PlayerLifeState.Alive ||
                (uint)role > 1 || BlockFor(permission) == PlayerActionBlock.None ||
                (session != null && session.Phase != SessionPhase.Offline && !session.IsAuthority)) return false;
            _gate = gate; _life = life; _session = session; _registry = registry; _authority = true;
            Role = role; Permission = permission;
            CurrentHealth = Mathf.Floor(phaseTwo ? config.PhaseTwoHealth : config.PhaseOneHealth);
            _warningSeconds = config.WarningSeconds; _sealSeconds = config.SealSeconds;
            State = _warningSeconds > 0 ? ClaudeBookState.Warning : ClaudeBookState.Sealed;
            RemainingSeconds = State == ClaudeBookState.Warning ? _warningSeconds : _sealSeconds;
            if (State == ClaudeBookState.Sealed) _gate.SetBlock(this, BlockFor(Permission));
            transform.position = position; Shape.enabled = Visual.enabled = LabelCanvas.enabled = true;
            RenderPage(config);
            Progress.fillAmount = 1;
            _life.StateChanged += OnLifeChanged;
            if (registry != null) Perception.Register(registry);
            FeedbackReset?.Invoke();
            return true;
        }

        private void RenderPage(ClaudePermissionConfig config)
        {
            // 书页圆圈代表受封角色，不跟本机菜单主题变色，避免把HS书显示成DS身份。
            var palette = Role == PlayerRole.DeepSeek ? DeepSeekPalette : HarnessPalette;
            IdentityCircle.sprite = palette.CircleBase;
            Progress.sprite = palette.CircleFrame;
            Label.text = config.PermissionLabels[(int)Permission];
            Label.color = Role == PlayerRole.DeepSeek ? DeepSeekInk : HarnessInk;
        }

        public ClaudeBookSnapshot CaptureSnapshot() => IsOpen
            ? new ClaudeBookSnapshot(transform.position, Role, Permission, State, CurrentHealth, RemainingSeconds, Visual.flipX)
            : default;

        // 调用方先验证完整四书帧，避免只更新前几本后才发现后半帧无效。
        internal bool IsAuthorityBook => _authority;
        internal void ApplyReplica(in ClaudeBookSnapshot frame, ClaudePermissionConfig config, PlayerActionGate gate)
        {
            if (frame.State == ClaudeBookState.Hidden)
            { if (IsOpen) Clear(); return; }
            bool identityChanged = !IsOpen || Role != frame.Role || Permission != frame.Permission || _gate != gate;
            if (identityChanged) Clear();
            _authority = false; _gate = gate;
            Role = frame.Role; Permission = frame.Permission; State = frame.State;
            CurrentHealth = frame.Health; RemainingSeconds = frame.RemainingSeconds;
            _warningSeconds = config.WarningSeconds; _sealSeconds = config.SealSeconds;
            transform.position = frame.Position; Visual.flipX = frame.Mirrored;
            Shape.enabled = false; // 客机不能用本地碰撞提前击碎书或裁决伤害。
            Visual.enabled = LabelCanvas.enabled = true;
            RenderPage(config);
            Progress.fillAmount = RemainingSeconds / (State == ClaudeBookState.Warning ? _warningSeconds : _sealSeconds);
            _gate.SetBlock(this, State == ClaudeBookState.Sealed ? BlockFor(Permission) : PlayerActionBlock.None);
        }

        /// <summary>调用方传暂停感知现实战斗秒数，不乘场景倍速；大步长保留跨阶段余量。</summary>
        public void Advance(float realSeconds)
        {
            if (!CanReceiveDamage || !float.IsFinite(realSeconds) || realSeconds <= 0) return;
            if (_life.State != PlayerLifeState.Alive) { Clear(ClaudeBookCloseReason.PlayerDowned); return; }
            RemainingSeconds -= realSeconds;
            Progress.fillAmount = Mathf.Clamp01(RemainingSeconds / (State == ClaudeBookState.Warning ? _warningSeconds : _sealSeconds));
            if (RemainingSeconds > 0) return;
            if (State == ClaudeBookState.Warning)
            {
                RemainingSeconds += _sealSeconds;
                State = ClaudeBookState.Sealed;
                Progress.fillAmount = Mathf.Clamp01(RemainingSeconds / _sealSeconds);
                _gate.SetBlock(this, BlockFor(Permission));
            }
            if (RemainingSeconds <= 0) Clear(ClaudeBookCloseReason.Expired);
        }

        public bool TryReceiveDamage(in DamagePacket damage)
        {
            if (!CanReceiveDamage || !damage.IsValid) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage.Amount);
            DamageAccepted?.Invoke(damage);
            if (CurrentHealth <= 0) Clear(ClaudeBookCloseReason.Broken);
            return true;
        }

        private void OnLifeChanged(PlayerLifeStateController2D owner, PlayerLifeState state)
        { if (state != PlayerLifeState.Alive) Clear(ClaudeBookCloseReason.PlayerDowned); }

        public void Clear(ClaudeBookCloseReason reason = ClaudeBookCloseReason.Reset)
        {
            bool wasOpen = IsOpen;
            if (_gate != null) _gate.ClearBlock(this);
            if (_life != null) _life.StateChanged -= OnLifeChanged;
            if (_registry != null) _registry.Unregister(Perception);
            _gate = null; _life = null; _session = null; _registry = null; _authority = false;
            State = ClaudeBookState.Hidden; CurrentHealth = RemainingSeconds = _sealSeconds = 0;
            _warningSeconds = 0;
            if (Shape != null) Shape.enabled = false;
            if (Visual != null) Visual.enabled = false;
            if (Decoration != null) Decoration.enabled = false;
            if (LabelCanvas != null) LabelCanvas.enabled = false;
            FeedbackReset?.Invoke();
            if (wasOpen) Closed?.Invoke(this, reason);
        }
        private void Awake() => Clear();
        private void OnDisable() => Clear();
    }
}
