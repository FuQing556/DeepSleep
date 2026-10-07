using System;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Players.Companion;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class KimiRescuePerceptionChecks
    {
        public static string Run()
        {
            if(!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only")==null)
                throw new InvalidOperationException("仅在隔离Kimi验证场Play运行。");
            var boss=UnityEngine.Object.FindAnyObjectByType<KimiBoss2D>();
            var root=new GameObject("RescuePerceptionProbe");
            bool original=boss.Perception.PassiveAttackTarget;
            int count=0;
            void Check(bool value,string reason){if(!value)throw new Exception(reason);count++;}
            try
            {
                var registry=root.AddComponent<CombatPerceptionRegistry2D>();
                var sensor=root.AddComponent<CompanionBattleSensor2D>();sensor.Registry=registry;
                sensor.ObstacleRegistry=root.AddComponent<CompanionObstacleRegistry2D>();
                sensor.Config=AssetDatabase.LoadAssetAtPath<CompanionTacticsConfig>("Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset");
                Check(sensor.Initialize(),"Sensor configuration");
                boss.BeginAuthority(new Vector2(6,0),null,registry);Physics2D.SyncTransforms();
                Vector2 ally=boss.Perception.Position;
                Vector2 rescuer=ally+Vector2.left;
                boss.Perception.PassiveAttackTarget=false;sensor.Refresh(rescuer,ally);
                Check(sensor.PrioritizeRescueThreat(rescuer,ally,3.5f,.5f),"Reproduce old boss blocks rescue");
                Check(sensor.Danger(ally,Vector2.zero,.5f)>0,"Reproduce old false contact danger");
                boss.Perception.PassiveAttackTarget=true;sensor.Refresh(rescuer,ally);
                Check(sensor.Target==boss.Perception,"Boss remains normal attack target");
                Check(!sensor.PrioritizeRescueThreat(rescuer,ally,3.5f,.5f),"Boss no longer requires killing before rescue");
                Check(sensor.Danger(ally,Vector2.zero,.5f)==0,"No false contact danger for DS/HS");
                Check(sensor.NearbyCount==0,"Boss not a melee mob cluster");
                Check(UnityEngine.Object.FindAnyObjectByType<KimiHitCurtain2D>().Perception.PassiveAttackTarget,"Curtain configured passive");
                foreach(var mirror in UnityEngine.Object.FindObjectsByType<KimiMirrorSide2D>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    Check(mirror.Perception.PassiveAttackTarget,"Mirror configured passive");
                boss.Perception.PassiveAttackTarget=false;sensor.Refresh(rescuer,ally);
                Check(sensor.PrioritizeRescueThreat(rescuer,ally,3.5f,.5f),"Contact-danger classification still blocks rescue");
                return "PASS "+count+" rescue perception checks, old failure reproduced then removed; ordinary boss targeting preserved. Not full battle AI/device acceptance.";
            }
            finally {boss.Perception.PassiveAttackTarget=original;boss.ResetEncounter();UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
