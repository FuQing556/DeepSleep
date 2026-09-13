using System;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.World.Playfield;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class LaserBoundaryRegression
    {
        public static string Run()
        {
            var config = AssetDatabase.LoadAssetAtPath<HarnessTerminalLaserConfig>("Assets/_Project/Configs/Combat/Harness/CFG_HA_TerminalLaser_Default.asset");
            var field = ScriptableObject.CreateInstance<CombatPlayfieldConfig>();
            var so = new SerializedObject(field);
            so.FindProperty("_worldBounds").rectValue = new Rect(-10, -5, 20, 10);
            so.ApplyModifiedPropertiesWithoutUndo();
            int count = 0;
            try
            {
                foreach (var origin in new[] { Vector2.zero, new Vector2(-11, 0), new Vector2(11, 0), new Vector2(0, 6), new Vector2(0, -6), new Vector2(10, 5) })
                foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down, new Vector2(1, 1).normalized })
                foreach (int ports in new[] { 0, 4 })
                {
                    if (!HarnessTerminalLaserSnapshotFactory.TryCreate(1, origin, direction, field, config, 1, 1, out var shot, out var reason, ports))
                        throw new Exception($"{origin}/{direction}/{ports}: {reason}");
                    if (!shot.IsValid || shot.LaneCount != config.BaseLaneCount + ports || shot.SourceOrigin != origin)
                        throw new Exception("Invalid snapshot or moved muzzle");
                    for (int i = 0; i < shot.LaneCount; i++)
                    {
                        var lane = shot.GetLane(i);
                        bool intersects = field.TryGetRayExitDistanceAfterIntersection(lane.Origin, lane.Direction, out float expected);
                        if (!intersects) expected = field.WorldBounds.size.magnitude;
                        if (Mathf.Abs(lane.Length - expected * config.BeamLengthMultiplier) > .001f)
                            throw new Exception("Range regression");
                    }
                    count++;
                }
                if (HarnessTerminalLaserSnapshotFactory.TryCreate(1, new Vector2(float.NaN, 0), Vector2.right, field, config, 1, 1, out _, out _))
                    throw new Exception("NaN was accepted");
                return $"PASS: {count} edge/multi-port shots; normal ranges preserved; NaN rejected.";
            }
            finally { UnityEngine.Object.DestroyImmediate(field); }
        }
    }
}
