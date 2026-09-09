using System.IO;
using DeepSleep.Runtime.Input.Commands;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>固定字段协议。只接受方向/世界坐标，不把屏幕像素传给另一设备。</summary>
    public static class NetworkCommandCodec
    {
        public static void Write(BinaryWriter w, in PlayerCommand c)
        {
            w.Write(c.Sequence); w.Write(c.SimulationTick);
            w.Write((short)Mathf.RoundToInt(Mathf.Clamp(c.Move.x, -1, 1) * short.MaxValue));
            w.Write((short)Mathf.RoundToInt(Mathf.Clamp(c.Move.y, -1, 1) * short.MaxValue));
            w.Write((byte)c.Aim.Reference); w.Write(c.Aim.Value.x); w.Write(c.Aim.Value.y);
            w.Write((byte)c.PrimarySkill); w.Write((byte)c.SecondarySkill);
            w.Write((byte)c.ConfirmAim); w.Write((byte)c.CancelAim);
        }

        public static bool TryRead(BinaryReader r, out PlayerCommand command)
        {
            command = default;
            uint sequence = r.ReadUInt32(), tick = r.ReadUInt32();
            var move = new Vector2(r.ReadInt16() / (float)short.MaxValue, r.ReadInt16() / (float)short.MaxValue);
            var reference = (AimReference)r.ReadByte();
            var aim = new Vector2(r.ReadSingle(), r.ReadSingle());
            byte primary = r.ReadByte(), secondary = r.ReadByte(), confirm = r.ReadByte(), cancel = r.ReadByte();
            if ((byte)reference > (byte)AimReference.WorldPosition || !Finite(aim.x) || !Finite(aim.y) ||
                (primary | secondary | confirm | cancel) > 7) return false;
            command = new PlayerCommand(sequence, tick, Vector2.ClampMagnitude(move, 1),
                new AimIntent(reference, aim), (CommandButtonState)primary, (CommandButtonState)secondary,
                (CommandButtonState)confirm, (CommandButtonState)cancel, 0, 0);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
