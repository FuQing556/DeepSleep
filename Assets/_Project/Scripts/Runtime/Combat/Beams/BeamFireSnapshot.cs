using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams
{
    /// <summary>
    /// 校准完成时冻结的一次激光开火结果。
    /// 配置热更新、目标移动或下一次输入都不能修改本次开火。
    /// </summary>
    public sealed class BeamFireSnapshot
    {
        private readonly BeamLaneSnapshot[] _lanes;

        public BeamFireSnapshot(
            uint sequence,
            Vector2 sourceOrigin,
            Vector2 aimDirection,
            LayerMask targetLayers,
            IReadOnlyList<BeamLaneSnapshot> lanes)
        {
            if (lanes == null)
            {
                throw new ArgumentNullException(nameof(lanes));
            }

            Sequence = sequence;
            SourceOrigin = sourceOrigin;
            AimDirection = aimDirection.normalized;
            TargetLayers = targetLayers;
            _lanes = new BeamLaneSnapshot[lanes.Count];

            for (int index = 0; index < lanes.Count; index++)
            {
                _lanes[index] = lanes[index];
            }
        }

        public uint Sequence { get; }
        public Vector2 SourceOrigin { get; }
        public Vector2 AimDirection { get; }
        public LayerMask TargetLayers { get; }
        public int LaneCount => _lanes.Length;

        public BeamLaneSnapshot GetLane(int index)
        {
            if (index < 0 || index >= _lanes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _lanes[index];
        }

        public bool IsValid
        {
            get
            {
                if (AimDirection.sqrMagnitude <= 0f ||
                    TargetLayers.value == 0 ||
                    _lanes.Length == 0)
                {
                    return false;
                }

                for (int index = 0; index < _lanes.Length; index++)
                {
                    if (!_lanes[index].IsValid)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
