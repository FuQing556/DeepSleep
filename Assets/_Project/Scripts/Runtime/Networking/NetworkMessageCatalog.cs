using System;
using System.Collections.Generic;
using System.IO;
using DeepSleep.Runtime.Progression.Levels;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>
    /// 唯一消息编号与线格式目录。编号的身份是（方向，byte）；新增消息必须同时登记格式和检查。
    /// 目录只预检数据，不访问场景/玩法状态；成功路径不创建临时读取缓冲。
    /// </summary>
    public static class NetworkMessageCatalog
    {
        public const ushort ProtocolVersion = 4;
        public enum Direction { AuthorityToPeer, PeerToAuthority }

        public static class Authority
        {
            public const byte Welcome = 1, Start = 3, Room = 6;
            public const byte PlayerSnapshot = 32, WorldEntity = 33, WorldDespawn = 34, Effect = 35;
            public const byte WeaponState = 36, LaserFire = 37, MeleeWave = 38, GuardBlock = 39;
            public const byte RestNodeState = 40, CombatFeedback = 41, UpgradeSnapshot = 42;
            public const byte TokenBalance = 44, ChapterState = 45, PlayerHitFeedback = 46;
            // 40 已属于休息节点；旧豆包 40 从本协议版本起迁移，禁止与旧客户端混房。
            public const byte DoubaoSnapshot = 47;
        }

        public static class Peer
        {
            public const byte Ready = 2, Control = 4, Input = 5;
            // 41 在反方向表示准备请求；不是 Authority.CombatFeedback 的另一个订阅者。
            public const byte RestNodeReady = 41, UpgradeRequest = 43;
        }

        public readonly struct Definition
        {
            public Definition(byte id, Direction direction, string name, int minimum, int maximum)
            { Id = id; Flow = direction; Name = name; MinPayloadBytes = minimum; MaxPayloadBytes = maximum; }
            public byte Id { get; }
            public Direction Flow { get; }
            public string Name { get; }
            public int MinPayloadBytes { get; }
            public int MaxPayloadBytes { get; }
        }

        // 这些是线格式容量，不是技能/关卡调参。负载长度不包含消息编号首字节。
        private const int MaximumPayloadBytes = 16383;
        private const int MaximumDoubaoPhraseBytes = 64;
        private static readonly Definition[] Entries =
        {
            A(Authority.Welcome, nameof(Authority.Welcome), 7, 6 + 2 + LevelIdentityValidation.MaximumLevelIdUtf8Bytes),
            A(Authority.Start, nameof(Authority.Start), 0), A(Authority.Room, nameof(Authority.Room), 8),
            P(Peer.Ready, nameof(Peer.Ready), 1), P(Peer.Control, nameof(Peer.Control), 1),
            P(Peer.Input, nameof(Peer.Input), 29),
            A(Authority.PlayerSnapshot, nameof(Authority.PlayerSnapshot), 242),
            A(Authority.WorldEntity, nameof(Authority.WorldEntity), 38, 38 + 59 * 255),
            A(Authority.WorldDespawn, nameof(Authority.WorldDespawn), 8),
            A(Authority.Effect, nameof(Authority.Effect), 18),
            A(Authority.WeaponState, nameof(Authority.WeaponState), 70),
            A(Authority.LaserFire, nameof(Authority.LaserFire), 37 + 38, 37 + 38 * 128),
            A(Authority.MeleeWave, nameof(Authority.MeleeWave), 29),
            A(Authority.GuardBlock, nameof(Authority.GuardBlock), 28),
            A(Authority.RestNodeState, nameof(Authority.RestNodeState), 3),
            A(Authority.CombatFeedback, nameof(Authority.CombatFeedback), 29),
            P(Peer.RestNodeReady, nameof(Peer.RestNodeReady), 1),
            A(Authority.UpgradeSnapshot, nameof(Authority.UpgradeSnapshot), 40, 40 + 3 * 255),
            P(Peer.UpgradeRequest, nameof(Peer.UpgradeRequest), 2, 3),
            A(Authority.TokenBalance, nameof(Authority.TokenBalance), 13),
            A(Authority.ChapterState, nameof(Authority.ChapterState), 35),
            A(Authority.PlayerHitFeedback, nameof(Authority.PlayerHitFeedback), 17),
            A(Authority.DoubaoSnapshot, nameof(Authority.DoubaoSnapshot), 32, MaximumPayloadBytes)
        };
        public static IReadOnlyList<Definition> Definitions { get; } = Array.AsReadOnly(Entries);

        private static Definition A(byte id, string name, int min, int max = -1) =>
            new(id, Direction.AuthorityToPeer, name, min, max < 0 ? min : max);
        private static Definition P(byte id, string name, int min, int max = -1) =>
            new(id, Direction.PeerToAuthority, name, min, max < 0 ? min : max);

        public static bool TryGet(byte id, Direction direction, out Definition definition)
        {
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].Id == id && Entries[i].Flow == direction)
                { definition = Entries[i]; return true; }
            definition = default;
            return false;
        }

        /// <summary>检查定义本身；同方向重复编号即失败，不能悄悄交给多个系统串读。</summary>
        public static bool TryValidateDefinitions(out string reason)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                Definition entry = Entries[i];
                if (entry.Id == 0 || string.IsNullOrEmpty(entry.Name) || entry.MinPayloadBytes < 0 ||
                    entry.MaxPayloadBytes < entry.MinPayloadBytes || entry.MaxPayloadBytes > MaximumPayloadBytes)
                { reason = "网络目录存在无效编号、名称或负载边界。"; return false; }
                for (int j = 0; j < i; j++)
                    if (entry.Flow == Entries[j].Flow &&
                        (entry.Id == Entries[j].Id || entry.Name == Entries[j].Name))
                    { reason = "网络目录同方向消息编号或名称重复：" + entry.Name; return false; }
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>在分发给任何玩法订阅者之前检查完整帧（首字节为消息编号）。</summary>
        public static bool TryValidatePacket(byte[] packet, Direction direction, out string reason)
        {
            if (packet == null || packet.Length == 0)
            { reason = "网络帧为空。"; return false; }
            var cursor = new Cursor(packet, 1, packet.Length - 1);
            return Validate(packet[0], direction, ref cursor, out reason);
        }

        /// <summary>供关键接收器独立使用；无论成功/失败都保持读取位置，不允许预检产生部分状态。</summary>
        public static bool TryValidatePayload(byte id, Direction direction, BinaryReader reader, out string reason)
        {
            if (reader == null || !reader.BaseStream.CanSeek)
            { reason = "网络负载需要可定位的只读流。"; return false; }
            long position = reader.BaseStream.Position;
            long remaining = reader.BaseStream.Length - position;
            if (remaining < 0 || remaining > MaximumPayloadBytes)
            { reason = "网络负载长度越界。"; return false; }
            try
            {
                var cursor = new Cursor(reader.BaseStream, (int)remaining);
                return Validate(id, direction, ref cursor, out reason);
            }
            finally { reader.BaseStream.Position = position; }
        }

        private static bool Validate(byte id, Direction direction, ref Cursor cursor, out string reason)
        {
            if (!TryGet(id, direction, out Definition entry))
            { reason = "网络消息未登记或方向不符。"; return false; }
            if (cursor.Remaining < entry.MinPayloadBytes || cursor.Remaining > entry.MaxPayloadBytes)
            { reason = "网络负载长度不符合目录。"; return false; }
            try
            {
                if (direction == Direction.PeerToAuthority) ReadPeer(id, ref cursor);
                else ReadAuthority(id, ref cursor);
                if (cursor.Remaining != 0) throw new InvalidDataException();
            }
            catch (InvalidDataException)
            { reason = "网络负载含尾部字节或字段值无效。"; return false; }
            catch (IOException)
            { reason = "网络负载被截断、含尾部字节或字段值无效。"; return false; }
            reason = string.Empty;
            return true;
        }

        private static void ReadPeer(byte id, ref Cursor c)
        {
            switch (id)
            {
                case Peer.Ready: case Peer.Control: case Peer.RestNodeReady: c.Bool(); break;
                case Peer.Input:
                    c.Skip(16); // epoch、命令序号、模拟刻、两个量化方向 short。
                    c.Enum(2); c.Floats(2); for (int i = 0; i < 4; i++) c.Enum(7); break;
                case Peer.UpgradeRequest:
                    byte request = c.Byte(); c.Enum(1);
                    if (request == 2) c.Byte(); else if (request != 1) throw new InvalidDataException();
                    break;
                default: throw new InvalidDataException();
            }
        }

        private static void ReadAuthority(byte id, ref Cursor c)
        {
            switch (id)
            {
                case Authority.Welcome:
                    c.Enum(1); c.Skip(4); c.Bool();
                    c.StringBytes(LevelIdentityValidation.MaximumLevelIdUtf8Bytes, 1, true); break;
                case Authority.Start: break;
                case Authority.Room: c.Bool(); c.Bool(); c.Enum(2); c.Enum(2); c.Skip(4); break;
                case Authority.PlayerSnapshot:
                    c.Skip(8); byte firstRole = c.Byte(); ReadPlayer(firstRole, ref c);
                    byte secondRole = c.Byte(); if (firstRole == secondRole) throw new InvalidDataException();
                    ReadPlayer(secondRole, ref c); break;
                case Authority.WorldEntity:
                    c.Skip(8); c.Floats(3); c.Bool(); c.Skip(8); int layers = c.Byte();
                    for (int i = 0; i < layers; i++)
                    { c.Skip(4); c.Bool(); c.Floats(11); c.Skip(8); c.Bool(); c.Bool(); }
                    ReadHitFlash(ref c);
                    break;
                case Authority.WorldDespawn: c.Skip(8); break;
                case Authority.Effect: c.Skip(6); c.Floats(3); break;
                case Authority.WeaponState:
                    c.Skip(4); c.Enum(2); c.Floats(2); c.Bool(); c.Floats(2);
                    c.Bool(); c.Bool(); c.Skip(8); c.Floats(6); c.Bool(); c.Bool(); c.Skip(4); c.Floats(2); break;
                case Authority.LaserFire:
                    c.Skip(4); c.Floats(2); c.Skip(4); c.Floats(4); c.Skip(4); int lanes = c.Byte();
                    if (lanes < 1 || lanes > 128) throw new InvalidDataException();
                    for (int i = 0; i < lanes; i++)
                    {
                        c.Skip(4); c.Floats(4);
                        for (int f = 0; f < 4; f++) if (c.Float() <= 0f) throw new InvalidDataException();
                        byte visualLayers = c.Byte(); if (visualLayers < 1 || visualLayers > 3) throw new InvalidDataException();
                        c.Bool();
                    }
                    break;
                case Authority.MeleeWave: c.Skip(8); c.Floats(3); c.Byte(); c.Floats(2); break;
                case Authority.GuardBlock: c.Skip(4); c.Floats(5); c.Skip(4); break;
                case Authority.RestNodeState: c.Enum(4); c.Bool(); c.Bool(); break;
                case Authority.CombatFeedback:
                    c.Skip(4); c.Enum(3); c.Floats(4);
                    if (c.Float() < 0f || c.Float() < 0f) throw new InvalidDataException(); break;
                case Authority.UpgradeSnapshot:
                    c.Skip(26); ReadWallet(ref c); int count = c.Byte(); c.Skip(3 * count); break;
                case Authority.TokenBalance: ReadWallet(ref c); break;
                case Authority.ChapterState:
                    c.Enum(4); c.Enum(4); c.Skip(4); c.Float(); c.Skip(4); c.Floats(3); c.Bool(); c.Skip(4); c.Float(); break;
                case Authority.PlayerHitFeedback:
                    c.Skip(4); c.Enum(1);
                    if (Math.Abs(c.Float()) > 1.001f || Math.Abs(c.Float()) > 1.001f) throw new InvalidDataException();
                    float protection = c.Float(); if (protection < 0f || protection > 10f) throw new InvalidDataException(); break;
                case Authority.DoubaoSnapshot:
                    c.Skip(4); c.Enum(3); c.Bool(); c.Floats(2);
                    if (c.Float() < 0f) throw new InvalidDataException();
                    c.Bool(); if (c.Float() < 0f) throw new InvalidDataException();
                    ReadHitFlash(ref c);
                    int blocks = c.Byte();
                    for (int i = 0; i < blocks; i++)
                    {
                        c.Skip(4); c.Floats(2);
                        if (c.Float() <= 0f || c.Float() <= 0f) throw new InvalidDataException();
                        c.StringBytes(MaximumDoubaoPhraseBytes);
                    }
                    break;
                default: throw new InvalidDataException();
            }
        }

        private static void ReadHitFlash(ref Cursor c)
        {
            c.Skip(4);
            float age = c.Float();
            if (age < 0f || age > 1f) throw new InvalidDataException();
        }
        private static void ReadPlayer(byte role, ref Cursor c)
        {
            if (role > 1) throw new InvalidDataException();
            c.Floats(4); c.Bool(); byte facing = c.Byte();
            if (facing != 1 && facing != 255) throw new InvalidDataException();
            c.Skip(4); c.Floats(7); c.Bool(); c.Float(); c.Floats(7);
            c.Floats(2); c.Bool(); c.Bool(); c.Floats(2); c.Bool();
            c.Enum(4); c.Float(); c.Skip(4); c.Enum(4); c.Float();
        }
        private static void ReadWallet(ref Cursor c) { c.Skip(12); c.Bool(); }

        private struct Cursor
        {
            private readonly byte[] _bytes;
            private readonly Stream _stream;
            private int _position;
            private readonly int _end;
            public Cursor(byte[] bytes, int offset, int length)
            { _bytes = bytes; _stream = null; _position = offset; _end = offset + length; }
            public Cursor(Stream stream, int length)
            { _bytes = null; _stream = stream; _position = 0; _end = length; }
            public int Remaining => _end - _position;
            public byte Byte()
            {
                if (Remaining <= 0) throw new EndOfStreamException();
                int value = _bytes != null ? _bytes[_position] : _stream.ReadByte();
                if (value < 0) throw new EndOfStreamException();
                _position++; return (byte)value;
            }
            public void Skip(int length)
            {
                if (length < 0 || length > Remaining) throw new EndOfStreamException();
                _position += length;
                if (_stream != null) _stream.Position += length;
            }
            public void Bool() { if (Byte() > 1) throw new InvalidDataException(); }
            public void Enum(byte maximum) { if (Byte() > maximum) throw new InvalidDataException(); }
            public float Float()
            {
                int bits = Byte() | Byte() << 8 | Byte() << 16 | Byte() << 24;
                float value = BitConverter.Int32BitsToSingle(bits);
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException();
                return value;
            }
            public void Floats(int count) { for (int i = 0; i < count; i++) Float(); }
            public void StringBytes(int maximum, int minimum = 0, bool strictUtf8 = false)
            {
                uint length = 0;
                for (int shift = 0; shift < 35; shift += 7)
                {
                    byte value = Byte();
                    if (shift == 28 && value > 7) throw new InvalidDataException();
                    length |= (uint)(value & 127) << shift;
                    if ((value & 128) != 0) continue;
                    if (length < minimum || length > maximum || (strictUtf8 && shift > 0 && value == 0))
                        throw new InvalidDataException();
                    if (strictUtf8) Utf8((int)length); else Skip((int)length);
                    return;
                }
                throw new InvalidDataException();
            }
            private void Utf8(int length)
            {
                // 握手身份必须逐字节合法，不能让 ReadString 的替换字符把不同坏 ID 合并。
                // 只验证 UTF-8，不创建字符串/临时数组；包含 overlong、代理区与 >U+10FFFF 检查。
                if (length > Remaining) throw new EndOfStreamException();
                int end = _position + length;
                while (_position < end)
                {
                    byte first = Byte();
                    if (first <= 0x7f) continue;
                    int continuation;
                    byte secondMinimum = 0x80, secondMaximum = 0xbf;
                    if (first >= 0xc2 && first <= 0xdf) continuation = 1;
                    else if (first >= 0xe0 && first <= 0xef)
                    {
                        continuation = 2;
                        if (first == 0xe0) secondMinimum = 0xa0;
                        else if (first == 0xed) secondMaximum = 0x9f;
                    }
                    else if (first >= 0xf0 && first <= 0xf4)
                    {
                        continuation = 3;
                        if (first == 0xf0) secondMinimum = 0x90;
                        else if (first == 0xf4) secondMaximum = 0x8f;
                    }
                    else throw new InvalidDataException();
                    if (continuation > end - _position) throw new InvalidDataException();
                    byte second = Byte();
                    if (second < secondMinimum || second > secondMaximum) throw new InvalidDataException();
                    for (int i = 1; i < continuation; i++)
                    {
                        byte next = Byte();
                        if (next < 0x80 || next > 0xbf) throw new InvalidDataException();
                    }
                }
            }
        }
    }
}
