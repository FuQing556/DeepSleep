using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    public sealed partial class HarnessTerminalLaserController
    {
        private bool _replica;
        private HarnessTerminalLaserState _replicaState;
        private float _replicaSeconds, _replicaProgress;
        /// <summary>仅供禁用模拟的客人端接收权威表现状态；不会选目标或请求伤害。</summary>
        public void ApplyReplicaState(HarnessTerminalLaserState state, float seconds, float progress, bool aiming, Vector2 target)
        {
            if (enabled) return;
            bool changed = _hasAimPoint != aiming;
            _replica = true; _replicaState = state; _replicaSeconds = seconds; _replicaProgress = progress;
            _hasAimPoint = aiming; _lastKnownTargetPosition = target;
            if (changed) TargetChanged?.Invoke(null);
        }
        public void PresentReplicaFire(HarnessTerminalLaserFireRequest request)
        {
            if (!enabled && request != null && request.IsValid) FireRequested?.Invoke(request);
        }
    }
}
