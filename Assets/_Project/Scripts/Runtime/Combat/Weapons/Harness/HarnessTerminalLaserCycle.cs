using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// 只管理终端激光的校准与冷却时序。
    /// 目标选择、激光几何、伤害和表现均由其他对象负责。
    /// </summary>
    public sealed class HarnessTerminalLaserCycle
    {
        private float _phaseDurationSeconds;
        private float _remainingSeconds;

        public HarnessTerminalLaserState State { get; private set; } =
            HarnessTerminalLaserState.Ready;

        public float RemainingSeconds => _remainingSeconds;

        public bool IsCalibrationComplete =>
            State == HarnessTerminalLaserState.Calibrating &&
            _remainingSeconds <= 0f;

        public float PhaseProgress01 => State switch
        {
            HarnessTerminalLaserState.Ready => 1f,
            _ when _phaseDurationSeconds <= 0f => 1f,
            _ => 1f - Mathf.Clamp01(
                _remainingSeconds / _phaseDurationSeconds),
        };

        public bool TryStartOrRestartCalibration(float durationSeconds)
        {
            if (State == HarnessTerminalLaserState.Cooldown ||
                durationSeconds < 0f)
            {
                return false;
            }

            State = HarnessTerminalLaserState.Calibrating;
            _phaseDurationSeconds = durationSeconds;
            _remainingSeconds = durationSeconds;
            return true;
        }

        public bool TryCancelCalibration()
        {
            if (State != HarnessTerminalLaserState.Calibrating)
            {
                return false;
            }

            EnterReady();
            return true;
        }

        public bool TryEnterCooldown(float durationSeconds)
        {
            if (State != HarnessTerminalLaserState.Calibrating ||
                durationSeconds <= 0f)
            {
                return false;
            }

            State = HarnessTerminalLaserState.Cooldown;
            _phaseDurationSeconds = durationSeconds;
            _remainingSeconds = durationSeconds;
            return true;
        }

        public void Advance(float deltaTime)
        {
            if (State == HarnessTerminalLaserState.Ready ||
                deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds = Mathf.Max(
                0f,
                _remainingSeconds - deltaTime);

            if (State == HarnessTerminalLaserState.Cooldown &&
                _remainingSeconds <= 0f)
            {
                EnterReady();
            }
        }

        public void Reset()
        {
            EnterReady();
        }

        private void EnterReady()
        {
            State = HarnessTerminalLaserState.Ready;
            _phaseDurationSeconds = 0f;
            _remainingSeconds = 0f;
        }
    }
}
