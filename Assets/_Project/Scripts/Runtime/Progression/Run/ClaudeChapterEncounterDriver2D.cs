using System;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Simulation;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    public sealed class ClaudeChapterEncounterDriver2D : MonoBehaviour, IFixedSimulationStep,
        IChapterCombatTakeover, IChapterCombatLifecycle
    {
        public ChapterRunController Chapter;
        public ClaudeEncounter2D Encounter;
        public CoopSessionController Session;
        public ChapterSceneEffectsController SceneEffects;
        public int SegmentNumber;
        private bool _armed;
        public bool HasTakenOver {get;private set;}
        public bool IsComplete => Encounter.State==ClaudeEncounterState.Complete;
        public int DisplaySeconds => 999;
        public string DisplayTitle => Encounter.Config.Title;
        public string ObjectiveText => Encounter.Config.Objective;
        private bool CanAuthor => Session.Phase==SessionPhase.Offline || Session.IsAuthority;
        public bool TryValidateConfiguration(out string reason)
        {
            if(Chapter==null || Encounter==null || Session==null || SceneEffects==null || SegmentNumber<1 ||
                Chapter.CombatWorld==null || Encounter.Session!=Session || SceneEffects.Chapter!=Chapter ||
                Array.IndexOf(Chapter.CombatWorld.ParticipantComponents,this)<0)
            {reason="Claude章节绑定及生命周期登记缺失。";return false;}
            return Encounter.TryValidateConfiguration(out reason);
        }
        private void Awake()
        {
            if(!TryValidateConfiguration(out string reason)){Debug.LogError("[ClaudeChapter] "+reason,this);enabled=false;}
        }
        private void OnEnable()
        {
            if(Encounter==null)return;
            Encounter.TakeoverRequested+=OnTakeover;Encounter.BattleStarted+=OnBattle;
        }
        public bool IsRequiredForSegment(int segmentNumber) => isActiveAndEnabled && segmentNumber==SegmentNumber &&
            (!Chapter.IsChallenge || Chapter.Challenge.ChallengeKind==DeepSleep.Runtime.Progression.Bestiary.BestiaryChallengeKind.Claude);
        public void ResetForSegment(int segmentNumber)
        {StopCombat(ChapterCombatStopReason.EncounterTakeover);_armed=IsRequiredForSegment(segmentNumber);}
        public void Simulate(float seconds)
        {
            if(!_armed || !CanAuthor || Chapter.Phase!=ChapterRunPhase.Combat ||
                !IsRequiredForSegment(Chapter.SegmentNumber) || seconds<=0 || Time.timeScale<=0)return;
            if(Encounter.State==ClaudeEncounterState.Idle && !Encounter.Begin(Chapter.SceneEffectSeed,
                Chapter.IsChallenge ? Chapter.Challenge.PreludeSeconds : (float?)null))
            {_armed=false;Debug.LogError("[ClaudeChapter] 启动失败。",this);return;}
            Encounter.Simulate(seconds,seconds/Time.timeScale);
        }
        private void OnTakeover()
        {
            if(!_armed || !CanAuthor || Chapter.Phase!=ChapterRunPhase.Combat){Encounter.Cancel();return;}
            Chapter.CombatWorld.TakeOverCombat(this);HasTakenOver=true;
            SceneEffects.BeginBossClock(Chapter.SceneEffectSeed,false);
        }
        private void OnBattle()=>SceneEffects.BeginBossClock(Chapter.SceneEffectSeed,true);
        public void ApplyReplica(bool takenOver) => HasTakenOver = takenOver;
        public void StopCombat(ChapterCombatStopReason reason)
        {
            bool victory=reason==ChapterCombatStopReason.Settlement && IsComplete;
            _armed=false;HasTakenOver=false;SceneEffects.EndBossClock();
            if(victory)Encounter.Presentation.BeginDeparture();
            Encounter.Cancel(victory);
            if(!victory)Encounter.Presentation.ResetPresentation();
        }
        private void OnDisable()
        {
            if(Encounter!=null){Encounter.TakeoverRequested-=OnTakeover;Encounter.BattleStarted-=OnBattle;}
            if(Chapter!=null && Encounter!=null && SceneEffects!=null)StopCombat(ChapterCombatStopReason.SceneExit);
        }
    }
}
