using System.IO;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.UI.Combat;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>玩家权威快照的本地表现。没有任何伤害结算权限。</summary>
    public sealed class NetworkPlayerReplica : MonoBehaviour, IPlayerCombatHudSource
    {
        public PlayerRole Role;
        public CoopSessionController Session;
        public Rigidbody2D Body;
        public SpriteRenderer Visual;
        public Transform VisualRoot;
        public PlayerFacingController2D Facing;
        public PlayerCombatHudSource LocalHud;
        public NetworkSpriteCatalog Sprites;
        public DeepSleep.Runtime.Players.Revive.Presentation.PlayerReviveProtectionView2D ProtectionView;
        public SpriteRenderer PoseGhostRenderer;
        public NetworkMovementPrediction Prediction;
        public DeepSleep.Runtime.Players.Actions.PlayerActionGate Actions;
        public DeepSleep.Runtime.Players.LifeCycle.PlayerDownedVisualConfig PoseGhostConfig;
        private readonly DeepSleep.Runtime.Presentation.Poses.SpritePoseGhost2D _ghost = new();
        private PlayerCombatHudSnapshot _hud;
        private Vector2 _position;
        private bool _hasSnapshot;
        private void OnEnable() => Session.SessionOpened += Reset;
        private void OnDisable() { Session.SessionOpened -= Reset; _ghost.Clear(); }
        private void Reset(bool authority) { _hasSnapshot = false; _ghost.Clear(); }

        private void Awake()
        {
            if (Session == null || Body == null || Visual == null || VisualRoot == null || Facing == null ||
                LocalHud == null || Sprites == null || !Sprites.Initialize())
            { Debug.LogError("[NetworkPlayerReplica] 配置不完整。", this); enabled = false; }
        }

        public bool TryRead(out PlayerCombatHudSnapshot state)
        {
            if (Session.Phase == SessionPhase.Offline || Session.IsAuthority) return LocalHud.TryRead(out state);
            state = _hud; return _hasSnapshot;
        }

        public void Write(BinaryWriter w)
        {
            w.Write((byte)Role); w.Write(Body.position.x); w.Write(Body.position.y);
            w.Write(Body.linearVelocity.x); w.Write(Body.linearVelocity.y);
            w.Write(Actions.IsBlocked(DeepSleep.Runtime.Players.Actions.PlayerActionBlock.Movement));
            w.Write((sbyte)Facing.CurrentDirection); w.Write(Sprites.GetId(Visual.sprite));
            WriteVector(w, Visual.transform.localPosition); WriteVector(w, Visual.transform.localScale);
            w.Write(Visual.transform.localEulerAngles.z); w.Write(Visual.enabled); w.Write(Visual.color.a);
            WriteVector(w, VisualRoot.localPosition); WriteVector(w, VisualRoot.localScale);
            w.Write(VisualRoot.localEulerAngles.z);
            LocalHud.TryRead(out var h);
            w.Write(h.Health); w.Write(h.MaximumHealth); w.Write(h.Downed); w.Write(h.BeingRevived);
            w.Write(h.ReviveProgress); w.Write(h.ProtectionSeconds); w.Write(h.ReviveProtection);
            w.Write((byte)h.SkillPhase); w.Write(h.SkillSeconds); w.Write(h.Charges);
            w.Write((byte)h.WeaponPhase); w.Write(h.WeaponSeconds);
        }

        public void Read(BinaryReader r, uint ack)
        {
            var position = new Vector2(r.ReadSingle(), r.ReadSingle());
            var velocity = new Vector2(r.ReadSingle(), r.ReadSingle());
            bool blocked = r.ReadBoolean();
            var facing = (FacingDirection)r.ReadSByte(); uint sprite = r.ReadUInt32();
            Vector3 localPosition = ReadVector(r), localScale = ReadVector(r); float angle = r.ReadSingle();
            bool visible = r.ReadBoolean(); float alpha = r.ReadSingle();
            Vector3 rootPosition = ReadVector(r), rootScale = ReadVector(r); float rootAngle = r.ReadSingle();
            float health = r.ReadSingle(), max = r.ReadSingle(); bool downed = r.ReadBoolean(), reviving = r.ReadBoolean();
            float progress = r.ReadSingle(), protection = r.ReadSingle(); bool reviveProtection = r.ReadBoolean();
            var skill = (HudActionPhase)r.ReadByte(); float skillSeconds = r.ReadSingle(); int charges = r.ReadInt32();
            var weapon = (HudActionPhase)r.ReadByte(); float weaponSeconds = r.ReadSingle();
            if (Prediction != null) Prediction.Reconcile(ack, position, velocity, blocked || downed);
            _hud = new PlayerCombatHudSnapshot(health, max, downed, reviving, progress, protection, reviveProtection,
                Session.LocalRole == Role, skill, skillSeconds, charges, weapon, weaponSeconds);
            LocalHud.Revive.ApplyReplicaProgress(reviving, progress);
            if (ProtectionView != null) ProtectionView.ApplyReplicaProtection(reviveProtection);
            _position = position;
            if (!_hasSnapshot) { Body.position = position; Body.transform.position = position; }
            _hasSnapshot = true; Body.linearVelocity = velocity;
            if (Sprites.TryResolve(sprite, out var changedPose) && changedPose != Visual.sprite && PoseGhostRenderer != null && PoseGhostConfig != null)
                _ghost.Capture(Visual, PoseGhostRenderer, PoseGhostConfig.GhostStartAlpha, PoseGhostConfig.GhostFadeSeconds);
            Facing.SetDirection(facing);
            if (Sprites.TryResolve(sprite, out var pose)) Visual.sprite = pose;
            Visual.transform.localPosition = localPosition; Visual.transform.localScale = localScale;
            Visual.transform.localRotation = Quaternion.Euler(0, 0, angle);
            Visual.enabled = visible; var color = Visual.color; color.a = alpha; Visual.color = color;
            VisualRoot.localPosition = rootPosition; VisualRoot.localScale = rootScale;
            VisualRoot.localRotation = Quaternion.Euler(0, 0, rootAngle);
        }

        private void LateUpdate()
        {
            if (!_hasSnapshot || Session.IsAuthority || Session.Phase != SessionPhase.Playing) return;
            _ghost.Tick(Time.unscaledDeltaTime);
            float t = 1f - Mathf.Exp(-Session.Config.RemoteInterpolationSpeed * Time.unscaledDeltaTime);
            // 客人刚体不参加物理模拟，必须显式提交 Transform，不能等待物理步骤回写。
            Vector2 displayed = Prediction != null && Prediction.Active ? Prediction.Position :
                Vector2.Lerp(Body.transform.position, _position, t);
            Body.position = displayed; Body.transform.position = displayed;
        }
        private static void WriteVector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector3 ReadVector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
