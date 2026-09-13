using System.IO;
using DeepSleep.Runtime.Combat.Beams;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>协议2：仅传权威几何与表现层数，客机不再检索分支目标。</summary>
    public static class NetworkBeamSnapshotCodec
    {
        public static void Write(BinaryWriter w, BeamFireSnapshot b)
        {
            w.Write(b.Sequence); Vec(w,b.SourceOrigin); Vec(w,b.AimDirection); w.Write(b.TargetLayers.value);
            w.Write((byte)b.LaneCount);
            for(int i=0;i<b.LaneCount;i++)
            {
                var lane=b.GetLane(i); w.Write(lane.LaneIndex); Vec(w,lane.Origin); Vec(w,lane.Direction);
                w.Write(lane.Length); w.Write(lane.Width); w.Write(lane.PrimaryTargetDamage); w.Write(lane.PiercingDamage);
                w.Write((byte)lane.VisualLayers); w.Write(lane.IsBranch);
            }
        }
        public static BeamFireSnapshot Read(BinaryReader r)
        {
            uint sequence=r.ReadUInt32(); Vector2 origin=Vec(r),direction=Vec(r); int mask=r.ReadInt32();
            int count=r.ReadByte(); if(count<1 || count>128)throw new InvalidDataException("激光束线数量超出协议上限。");
            var lanes=new BeamLaneSnapshot[count];
            for(int i=0;i<count;i++)
                lanes[i]=new BeamLaneSnapshot(r.ReadInt32(),Vec(r),Vec(r),r.ReadSingle(),r.ReadSingle(),
                    r.ReadSingle(),r.ReadSingle(),r.ReadByte(),r.ReadBoolean());
            var snapshot=new BeamFireSnapshot(sequence,origin,direction,mask,lanes);
            if(!snapshot.IsValid)throw new InvalidDataException("激光快照无效。");
            return snapshot;
        }
        private static void Vec(BinaryWriter w,Vector2 v){w.Write(v.x);w.Write(v.y);}
        private static Vector2 Vec(BinaryReader r)=>new(r.ReadSingle(),r.ReadSingle());
    }
}
