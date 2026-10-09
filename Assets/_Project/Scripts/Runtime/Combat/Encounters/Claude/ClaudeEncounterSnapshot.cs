using DeepSleep.Runtime.Combat.Beams;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    /// <summary>整场完整覆盖帧。固定技能槽复用；仅同步几何与状态，不复制客户端伤害模拟。</summary>
    public sealed class ClaudeEncounterSnapshot
    {
        public const int MaximumCutLanes = 20;
        public uint Sequence, BarrierHit;
        public bool TakenOver, Shown, PhaseTwo, BossClock, BossStates;
        public ClaudeEncounterState EncounterState;
        public ClaudePose Pose;
        public Vector2 Position;
        public float Health, BarrierHitAge, BossElapsed;
        public int BossSeed;
        public ClaudePresentationState PresentationState;
        public float NightAlpha, FigureAlpha, RainAlpha, ShieldAlpha;
        public ClaudeEnergyState EnergyState;
        public Vector2 EnergyPosition, EnergyDirection;
        public float EnergyAge, EnergySpinAge, EnergyHealth;
        public ClaudeEnergyState SecondEnergyState;
        public Vector2 SecondEnergyPosition, SecondEnergyDirection;
        public float SecondEnergyAge, SecondEnergySpinAge, SecondEnergyHealth;
        public ClaudeTrackingCutState TrackingState;
        public int TrackingTarget, TrackingShots;
        public float TrackingClock, TrackingPhaseAge, MarkerRotation;
        public Vector2 MarkerPosition;
        public BeamLaneSnapshot TrackingLane;
        public int SecondaryTrackingTarget = -1;
        public Vector2 SecondaryMarkerPosition;
        public BeamLaneSnapshot SecondaryTrackingLane;
        public readonly BeamLaneSnapshot[] TrackingLanes = new BeamLaneSnapshot[6];
        public readonly float[] TrackingFiredAt = new float[6];
        public bool CutRunning;
        public int CutTotal, CutsFired;
        public float CutAge, NextCut;
        public readonly int[] CutCounts = new int[3];
        public readonly float[] CutAt = new float[3];
        public readonly BeamLaneSnapshot[][] CutLanes =
        {
            new BeamLaneSnapshot[MaximumCutLanes], new BeamLaneSnapshot[MaximumCutLanes],
            new BeamLaneSnapshot[MaximumCutLanes]
        };
        public readonly ClaudeBookSnapshot[] Books = new ClaudeBookSnapshot[4];
        public readonly uint[] BookHits = new uint[4];
        public readonly float[] BookHitAges = new float[4];

        // 结构/有限数由消息目录预检，配置边界及跨字段条件在提交之前统一验证。
        public bool IsValid(ClaudeEncounter2D encounter)
        {
            if (!float.IsFinite(ShieldAlpha) || ShieldAlpha < 0 || ShieldAlpha > 1 || Health < 0 || Health > encounter.Actor.Config.MaximumHealth ||
                (!Shown && Health != 0) || BossStates && !BossClock ||
                BarrierHitAge < 0 || BarrierHitAge > 1 || BossElapsed < 0 ||
                EnergyHealth < 0 || EnergyHealth > Mathf.Max(encounter.Energy.Config.PhaseOneHealth, encounter.Energy.Config.PhaseTwoHealth) ||
                CutTotal > 3 || CutsFired > CutTotal || (CutRunning && CutTotal != 1 && CutTotal != 3) ||
                TrackingShots > 3 || TrackingTarget < -1 || TrackingTarget > 1 ||
                SecondaryTrackingTarget < -1 || SecondaryTrackingTarget > 1) return false;
            if ((EnergyState == ClaudeEnergyState.Charging || EnergyState == ClaudeEnergyState.Flying) &&
                (EnergyHealth <= 0 || !Unit(EnergyDirection))) return false;
            if (SecondEnergyHealth < 0 || SecondEnergyHealth > encounter.SecondaryEnergy.Config.PhaseTwoHealth ||
                ((SecondEnergyState == ClaudeEnergyState.Charging || SecondEnergyState == ClaudeEnergyState.Flying) &&
                 (!PhaseTwo || SecondEnergyHealth <= 0 || !Unit(SecondEnergyDirection)))) return false;
            bool tracking = TrackingState == ClaudeTrackingCutState.Tracking || TrackingState == ClaudeTrackingCutState.Locked;
            if (tracking && (TrackingTarget < 0 || !TrackingLane.IsValid)) return false;
            if (tracking && SecondaryTrackingTarget >= 0 &&
                (SecondaryTrackingTarget == TrackingTarget || !SecondaryTrackingLane.IsValid)) return false;
            for (int i = 0; i < TrackingShots; i++)
                if (!TrackingLanes[i].IsValid || TrackingFiredAt[i] > TrackingClock) return false;
            for (int i = 3; i < 3 + TrackingShots; i++)
                if (TrackingLanes[i].Direction != Vector2.zero && (!TrackingLanes[i].IsValid || TrackingFiredAt[i] > TrackingClock)) return false;
            for (int i = 0; i < 3; i++)
            {
                if (CutCounts[i] > encounter.SpatialCut.Config.LineCount || CutCounts[i] > MaximumCutLanes) return false;
                if (i < CutsFired && CutAt[i] > CutAge) return false;
                for (int j = 0; j < CutCounts[i]; j++) if (!CutLanes[i][j].IsValid) return false;
            }
            int permissions = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!Books[i].IsValid(encounter.Permissions.Config)) return false;
                if (Books[i].State == ClaudeBookState.Hidden) continue;
                int bit = 1 << ((int)Books[i].Role * 3 + (int)Books[i].Permission);
                if ((permissions & bit) != 0) return false;
                permissions |= bit;
            }
            return true;
        }
        private static bool Unit(Vector2 v) => Mathf.Abs(v.sqrMagnitude - 1) <= .01f;
    }
}
