using System;
using System.Collections.Generic;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard
{
    /// <summary>
    /// 一次共享护航的规则状态，不读输入、不查场景、不播放动画。
    /// 调用方先检查玩家在圈内、攻击可格挡，再提交本次攻击的唯一ID（不是敌人的ID）。
    /// </summary>
    public sealed class RiceGuardState
    {
        private readonly int _capacity;
        private readonly double _duration, _cooldown, _warning;
        private readonly HashSet<ulong> _blockedAttacks;

        public int RemainingCharges { get; private set; }
        public double RemainingSeconds { get; private set; }
        public double CooldownRemaining { get; private set; }
        public bool IsActive => RemainingCharges > 0 && RemainingSeconds > 0;
        public bool CanActivate => !IsActive && CooldownRemaining <= 0;
        public bool IsWarning => IsActive && RemainingSeconds <= _warning;

        public RiceGuardState(DeepSeekRiceGuardConfig config)
        {
            if (config == null || !config.IsValid) throw new ArgumentException("护航配置缺失或无效。", nameof(config));
            // 运行状态冻结规则，不在每次挡伤时重新读写共享资产。
            _capacity = config.Charges;
            _duration = config.DurationSeconds;
            _cooldown = config.CooldownSeconds;
            _warning = config.WarningSeconds;
            _blockedAttacks = new HashSet<ulong>(_capacity);
        }

        public bool TryActivate()
        {
            if (!CanActivate) return false;
            _blockedAttacks.Clear();
            RemainingCharges = _capacity;
            RemainingSeconds = _duration;
            return true;
        }

        /// <summary>
        /// true表示这次攻击已被挡住（含重复回调），而不是已经扣血。
        /// ID必须由伤害生产方分配；池对象再次发射必须使用新ID。0表示尚未编号，拒绝挡伤。
        /// </summary>
        public bool TryBlock(ulong attackId)
        {
            if (attackId == 0) return false;
            // 最后一碗消耗后也记住已挡攻击，防止同一攻击的第二个回调穿透刚破的盾。
            if (_blockedAttacks.Contains(attackId)) return true;
            if (!IsActive) return false;
            _blockedAttacks.Add(attackId);
            RemainingCharges--;
            if (RemainingCharges == 0) End();
            return true;
        }

        public void Simulate(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            double step = deltaTime;
            if (IsActive)
            {
                double activeStep = Math.Min(RemainingSeconds, step);
                RemainingSeconds -= activeStep;
                step -= activeStep;
                if (RemainingSeconds <= 0) End();
            }
            // 跨过到期时刻的一大步，剩余时间继续推进冷却，不凭空延长技能周期。
            if (!IsActive) CooldownRemaining = Math.Max(0, CooldownRemaining - step);
        }

        /// <summary>死亡等中止会结束保护并进入冷却，不能靠禁用再启用刷回六次。</summary>
        public void Cancel()
        {
            if (IsActive) End();
        }

        private void End()
        {
            RemainingCharges = 0;
            RemainingSeconds = 0;
            CooldownRemaining = _cooldown;
        }
    }
}
