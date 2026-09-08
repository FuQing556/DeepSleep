namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// Harness 终端激光的可同步运行状态。
    /// 数值未来可进入网络快照，已使用成员不得重新编号。
    /// </summary>
    public enum HarnessTerminalLaserState : byte
    {
        Ready = 0,
        Calibrating = 1,
        Cooldown = 2,
    }
}
