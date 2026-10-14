using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Presentation
{
    /// <summary>权威端的事件姿态；不移动碰撞体，不重新抽皮肤，客机读既有 Sprite 快照。</summary>
    [DefaultExecutionOrder(400)]
    public sealed class PlayerActionPosePresenter : MonoBehaviour
    {
        public PlayerRole Role;
        public CoopSessionController Session;
        public SpriteRenderer Subject;
        public PlayerDownedVisual2D Visual;
        public PlayerLifeStateController2D Life;
        public PlayerMovementMotor2D Motor;
        public PlayerDamageReceiver2D Damage;
        public PlayerReviveActionChannel Revive;
        public HarnessMeleeController Melee;
        public HarnessTerminalLaserPresenter Laser;
        public PlayerUpgradeRuntimeState Upgrades;
        public ChapterRunController Chapter;
        public PlayerActionPoseSet DefaultPoses, SkinPoses;
        public DeepSleep.Runtime.Players.Orientation.PlayerFacingController2D Facing;
        public DeepSleep.Runtime.Combat.Weapons.DeepSeek.DeepSeekManualTargetController DeepSeekTarget;
        public ClaudeChapterEncounterDriver2D Claude;
        private float _hurt, _celebrate;
        private Sprite _lastApplied;

        private bool CanDraw => Session.Phase == SessionPhase.Offline || Session.IsAuthority;
        private void OnEnable()
        {
            Damage.DamageAccepted += Hurt;
            Upgrades.Applied += Upgraded;
        }
        private void OnDisable()
        {
            Damage.DamageAccepted -= Hurt;
            Upgrades.Applied -= Upgraded;
            if (Laser != null) Laser.SetIdleVariant(null);
            if (Laser != null) Laser.SetAimMovementOverride(null);
            _hurt = _celebrate = 0;
            if (Facing != null) Facing.MovementFallbackEnabled = false;
            _lastApplied = null;
        }
        private void Hurt(PlayerDamageReceiver2D receiver, DamagePacket packet)
        { if (CanDraw) _hurt = .55f; }
        private void Upgraded(PlayerRole role, UpgradeCardId id)
        { if (CanDraw && role == Role) _celebrate = .75f; }

        private void LateUpdate()
        {
            if (!CanDraw) return;
            float dt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
            _hurt = Mathf.Max(0, _hurt - dt); _celebrate = Mathf.Max(0, _celebrate - dt);
            if (Life.State != PlayerLifeState.Alive)
            {
                _hurt = _celebrate = 0; _lastApplied = null;
                if (Laser != null) Laser.SetIdleVariant(null);
                if (Laser != null) Laser.SetAimMovementOverride(null);
                return;
            }
            var poses = Visual.CurrentSkin != null ? SkinPoses : DefaultPoses;
            if (poses == null) return;
            bool movementFacing = Claude != null && Claude.HasTakenOver;
            if (Facing != null)
            {
                bool ended = Facing.MovementFallbackEnabled && !movementFacing;
                Facing.MovementFallbackEnabled = movementFacing;
                if (movementFacing && Mathf.Abs(Motor.Velocity.x) > .2f)
                    Facing.MovementFallback = Motor.Velocity.x < 0
                        ? DeepSleep.Runtime.Players.Orientation.FacingDirection.Left
                        : DeepSleep.Runtime.Players.Orientation.FacingDirection.Right;
                bool aiming = (DeepSeekTarget != null && DeepSeekTarget.HasLockedTarget) ||
                    (Laser != null && Laser.IsHoldingAttackPose) || (Melee != null && Melee.IsMelee);
                if ((movementFacing || ended) && !aiming) Facing.ResetToInitialDirection();
            }
            Sprite desired = Visual.SelectedAliveSprite;
            if (Chapter.Phase == ChapterRunPhase.Complete) desired = poses.Celebrate;
            else if (_hurt > 0) desired = poses.Hurt;
            else if (Revive.IsChanneling) desired = poses.Rescue;
            else if (_celebrate > 0) desired = poses.Celebrate;
            else desired = SelectMovementPose(poses, desired, Motor.Velocity, Facing != null ? Facing.Forward.x : 1f);

            bool retreat = movementFacing && Motor.Velocity.x * (Facing != null ? Facing.Forward.x : 1f) < -.2f;
            if (Laser != null) Laser.SetAimMovementOverride(retreat && _hurt <= 0 ? poses.Backward : null);

            // 受击短暂覆盖攻击外观，不中断攻击逻辑；其余姿态让位给攻击/近战。
            if (Laser != null) Laser.SetIdleVariant(desired);
            if (_hurt <= 0 && ((Melee != null && Melee.IsMelee) || (Laser != null && Laser.IsHoldingAttackPose && !retreat)))
            {
                if (_lastApplied == poses.Hurt)
                {
                    if (Melee != null && Melee.IsMelee)
                        Subject.sprite = Melee.IsSwinging ? Melee.Attack.CharacterPose : Melee.Config.IdlePose;
                    else if (Laser != null) Laser.RestoreAttackPoseAfterHurt();
                }
                _lastApplied = null; return;
            }
            if (desired == null || Subject.sprite == desired) { _lastApplied = desired; return; }
            if (_lastApplied != desired) Visual.CaptureCurrentPose();
            Subject.sprite = desired;
            _lastApplied = desired;
        }

        public static Sprite SelectMovementPose(PlayerActionPoseSet poses, Sprite idle, Vector2 velocity, float facingX)
        {
            if (velocity.sqrMagnitude <= .04f) return idle;
            // 纯上下移动沿用前飞；前后以当前朝向为基准，皮肤露牌不再限制移动切图。
            return velocity.x * facingX < -.2f ? poses.Backward : poses.Movement;
        }
    }
}
