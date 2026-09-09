using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    public sealed partial class HarnessMeleeController
    {
        /// <summary>镜像只触发表现事件；不执行 Sweep/Burst，不修改原始配置。</summary>
        public void ApplyReplicaState(bool melee, bool swinging, uint sequence, HarnessMeleeAttackConfig attack,
            float progress, float aim, float remaining, float cooldown)
        {
            if (enabled || (swinging && attack == null)) return;
            bool modeChanged = IsMelee != melee, start = swinging && (!IsSwinging || SwingSequence != sequence);
            bool finish = IsSwinging && !swinging;
            IsMelee = melee; IsSwinging = swinging; SwingSequence = sequence; Attack = attack;
            AimDegrees = aim; _elapsed = attack != null ? progress * attack.SwingSeconds : 0;
            _modeRemaining = remaining; _cooldown = cooldown;
            if (modeChanged) ModeChanged?.Invoke(melee);
            if (start) SwingStarted?.Invoke();
            if (finish && melee) SwingFinished?.Invoke();
        }
        public void PresentReplicaWave(HarnessMeleeAttackConfig attack, Vector2 origin, float angle)
        {
            if (!enabled && attack != null) WaveRequested?.Invoke(attack, origin, angle);
        }
    }
}
