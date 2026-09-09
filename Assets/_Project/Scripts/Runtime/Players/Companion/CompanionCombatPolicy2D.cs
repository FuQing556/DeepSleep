using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Companion
{
    /// <summary>角色专属按键策略。只读技能状态，绝不直接调用开火、扣血或激活技能。</summary>
    public sealed class CompanionCombatPolicy2D : MonoBehaviour
    {
        public PlayerRole Role;
        public CompanionTacticsConfig Config;
        public DeepSeekManualTargetController DeepSeekTarget;
        public DeepSeekRiceGuardController Guard;
        public HarnessTerminalLaserController Laser;
        public HarnessMeleeController Melee;
        private CombatPerceptionBody2D _lastTarget;
        private float _retryRemaining;
        private float _meleeIdle;
        private float _harnessAimRest;

        public bool IsValid => Config != null && (Role == PlayerRole.DeepSeek
            ? DeepSeekTarget != null && Guard != null : Laser != null && Melee != null);

        public void ResetIntent()
        {
            _lastTarget = null;
            _retryRemaining = 0f;
            _meleeIdle = 0f;
            _harnessAimRest = Config.HarnessAimRestSeconds;
        }

        public void Build(CompanionBattleSensor2D sensor, Vector2 position, bool rescue,
            bool guardWanted, bool emergency, float deltaTime,
            out AimIntent aim, out CommandButtonState skill,
            out CommandButtonState attack, out CommandButtonState cancel)
        {
            _retryRemaining = Mathf.Max(0f, _retryRemaining - deltaTime);
            skill = attack = cancel = CommandButtonState.None;
            var target = sensor.Target;
            bool hasTarget = target != null && target.IsObservable;
            aim = hasTarget ? new AimIntent(AimReference.WorldPosition, target.Position) : default;
            if (Role == PlayerRole.DeepSeek)
            {
                if (guardWanted && !Guard.IsActive && Guard.CooldownRemaining <= 0 && _retryRemaining <= 0)
                    PressSkill(ref skill);
                if (rescue || !hasTarget)
                {
                    if (DeepSeekTarget.HasLockedTarget) cancel = CommandButtonState.Pressed;
                    _lastTarget = null;
                }
                else if (target != _lastTarget || !DeepSeekTarget.HasLockedTarget)
                {
                    attack = CommandButtonState.Pressed;
                    _lastTarget = target;
                }
                return;
            }

            bool threatClose = sensor.TryGetNearestThreat(position, Config.MeleeAttackRange, out Vector2 closePoint);
            if (Melee.IsMelee)
            {
                bool swing = threatClose && (!rescue || emergency);
                if (swing)
                {
                    aim = new AimIntent(AimReference.WorldPosition, closePoint);
                    attack = CommandButtonState.Held;
                    _meleeIdle = 0f;
                }
                else _meleeIdle += deltaTime;
                if (!rescue && !Melee.ExitRequested && !Melee.IsSwinging && _meleeIdle >= Config.MeleeIdleExitSeconds &&
                    _retryRemaining <= 0) PressSkill(ref skill);
                return;
            }

            bool inEntryRange = sensor.TryGetNearestThreat(position, Config.MeleeEnterRange, out _);
            if (inEntryRange && (!rescue || emergency) &&
                (emergency || sensor.NearbyCount >= Config.MeleeClusterCount) &&
                Melee.CooldownRemaining <= 0 && _retryRemaining <= 0)
            {
                PressSkill(ref skill);
                return;
            }
            if (rescue)
            {
                if (Laser.HasAimPoint) cancel = CommandButtonState.Pressed;
                _harnessAimRest = Config.HarnessAimRestSeconds;
                return;
            }
            // 已承诺的瞄准即使目标死亡也完成；冷却中不预选，让待机姿态确实有停留。
            if (Laser.State != HarnessTerminalLaserState.Ready)
            {
                _harnessAimRest = Config.HarnessAimRestSeconds;
                return;
            }
            _harnessAimRest = Mathf.Max(0f, _harnessAimRest - deltaTime);
            if (hasTarget && !Laser.HasAimPoint && _harnessAimRest <= 0f)
                attack = CommandButtonState.Pressed;
        }

        private void PressSkill(ref CommandButtonState skill)
        {
            skill = CommandButtonState.Pressed;
            _retryRemaining = Config.SkillRetrySeconds;
        }
    }
}
