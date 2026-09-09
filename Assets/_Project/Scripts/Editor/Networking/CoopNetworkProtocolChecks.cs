using System;
using System.IO;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Editor.Networking
{
    /// <summary>无场景修改的确定性协议检查，可由MCP重复运行。</summary>
    public static class CoopNetworkProtocolChecks
    {
        public static string Run()
        {
            int passed = 0;
            var command = new PlayerCommand(15, 20, new Vector2(0.25f, -0.5f),
                new AimIntent(AimReference.WorldPosition, new Vector2(2.5f, -3)),
                CommandButtonState.Pressed | CommandButtonState.Held, 0, CommandButtonState.Pressed, 0, 0, 0);
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
            NetworkCommandCodec.Write(w, command); stream.Position = 0;
            using var r = new BinaryReader(stream);
            Require(NetworkCommandCodec.TryRead(r, out var decoded) && decoded.Sequence == 15 &&
                Vector2.Distance(command.Move, decoded.Move) < 0.0001f && decoded.Aim.Value == command.Aim.Value, "round trip"); passed++;
            var source = new RemoteCommandSource(2, 1);
            Require(source.Enqueue(command, Time.unscaledTime), "first input accepted"); passed++;
            Require(!source.Enqueue(command, Time.unscaledTime), "duplicate rejected"); passed++;
            source.TryGetCommand(100, out var first); source.TryGetCommand(101, out var second);
            Require((first.PrimarySkill & CommandButtonState.Pressed) != 0 && second.PrimarySkill == CommandButtonState.Held &&
                second.ConfirmAim == 0 && second.SimulationTick == 101, "edge consumed once"); passed++;
            source.Clear(); source.TryGetCommand(102, out var neutral);
            Require(neutral.Move == Vector2.zero && neutral.PrimarySkill == 0, "takeover clears stale input"); passed++;
            Require(RemoteCommandSource.IsNewer(0, uint.MaxValue) && !RemoteCommandSource.IsNewer(uint.MaxValue, 0), "sequence wrap"); passed++;
            stream.SetLength(0);
            var invalid = new PlayerCommand(1, 1, Vector2.zero,
                new AimIntent(AimReference.WorldPosition, new Vector2(float.NaN, 0)), 0, 0, 0, 0, 0, 0);
            NetworkCommandCodec.Write(w, invalid); stream.Position = 0;
            Require(!NetworkCommandCodec.TryRead(r, out _), "NaN rejected"); passed++;
            stream.SetLength(0);
            invalid = new PlayerCommand(1, 1, Vector2.zero, new AimIntent(AimReference.NormalizedScreenPosition, Vector2.one), 0, 0, 0, 0, 0, 0);
            NetworkCommandCodec.Write(w, invalid); stream.Position = 0;
            Require(!NetworkCommandCodec.TryRead(r, out _), "screen-dependent aim rejected"); passed++;
            return passed + " protocol checks passed";
        }
        private static void Require(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("Network check failed: " + name); }
    }
}
