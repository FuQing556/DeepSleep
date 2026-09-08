using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// 校准完成后交给激光执行层的唯一请求。
    /// 伤害查询与视觉表现必须共用其中的同一份快照。
    /// </summary>
    public sealed class HarnessTerminalLaserFireRequest
    {
        public HarnessTerminalLaserFireRequest(
            GameObject source,
            DamageHitbox2D primaryTarget,
            Vector2 primaryTargetPosition,
            BeamFireSnapshot beamSnapshot)
        {
            Source = source;
            PrimaryTarget = primaryTarget;
            PrimaryTargetPosition = primaryTargetPosition;
            BeamSnapshot = beamSnapshot;
        }

        public GameObject Source { get; }
        public DamageHitbox2D PrimaryTarget { get; }
        public Vector2 PrimaryTargetPosition { get; }
        public BeamFireSnapshot BeamSnapshot { get; }

        public bool IsValid =>
            Source != null &&
            PrimaryTarget != null &&
            BeamSnapshot != null &&
            BeamSnapshot.IsValid;
    }
}
