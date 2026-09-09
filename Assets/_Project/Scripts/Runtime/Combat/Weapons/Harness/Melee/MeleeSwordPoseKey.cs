using System;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>一张关键姿势：剑柄、展开角度和大小独立于剑气。角度可连续跨越360度。</summary>
    [Serializable]
    public struct MeleeSwordPoseKey
    {
        [Range(0, 1)] public float Time;
        public Vector2 Hilt;
        public float Angle;
        [Min(.001f)] public float Scale;

        public MeleeSwordPoseKey(float time, Vector2 hilt, float angle, float scale)
        { Time = time; Hilt = hilt; Angle = angle; Scale = scale; }

        public Vector4 Channels => new(Hilt.x, Hilt.y, Angle, Scale);
    }
}
