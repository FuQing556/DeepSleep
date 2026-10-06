using System;
using System.Collections;
using System.Linq;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static partial class ChapterFlowLifecycleChecks
    {
        public static string StartDownloadEvasion()
        {
            if (!EditorApplication.isPlaying || GameAppRoot.Instance==null || Running || GameAppRoot.Instance.SceneRouter.IsTransitioning)
                return "Requires idle Boot/MainMenu Play.";
            Running=true; LastReport="";
            GameAppRoot.Instance.SceneRouter.StartCoroutine(DownloadEvasionGuard());
            return "Starting real arrow/AI steering collision checks";
        }
        private static IEnumerator DownloadEvasionGuard()
        {
            var router=GameAppRoot.Instance.SceneRouter;
            var isolation=new ProfileIsolation(GameAppRoot.Instance.Profile,GameAppRoot.Instance.Achievements.Definitions);
            Exception error=null;
            float scale=Time.timeScale; var mode=Physics2D.simulationMode;
            try
            {
                yield return Drive(DownloadEvasion(router),failure=>error=failure);
                Time.timeScale=1; Physics2D.simulationMode=mode;
                yield return Drive(Route(router,null),failure=>error=failure);
                isolation.RestoreAndVerify();
                Status=error==null?"PASSED controlled download evasion":"FAILED download evasion: "+error;
                LastReport=Status+"\n"+LastReport; Debug.Log(LastReport);
            }
            finally { isolation.Dispose(); Time.timeScale=scale; Physics2D.simulationMode=mode; Running=false; }
        }
        private static IEnumerator DownloadEvasion(GameSceneRouter router)
        {
            yield return Route(router,AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(LevelRoot+"World01_EarlyInternet.asset"));
            var chapter=One<ChapterRunController>(); var session=chapter.LevelBindings.Session;
            Require(session.Selection.TrySelect(PlayerRole.DeepSeek),"Selection");
            yield return Until(()=>chapter.Phase==ChapterRunPhase.Combat,"combat");
            Time.timeScale=0; Physics2D.simulationMode=SimulationMode2D.Script;
            chapter.CombatWorld.StopCombat(ChapterCombatStopReason.EncounterTakeover);
            chapter.CombatWorld.ResumeCombat();
            var pool=chapter.LevelBindings.Enemies.Select(e=>e.Pool).First(p=>p.Instances.Any(a=>a.GetComponent<DownloadChargeMotor2D>()!=null));
            int failures=0,total=0; uint tick=1000000;
            foreach(var brain in new[]{session.DeepSeekAi,session.HarnessAi})
            {
                var target=brain.Owner.GetComponent<DeepSleep.Runtime.Combat.Targeting.PlayerCombatTarget2D>();
                Require(target!=null,"Real target adapter");
                for(int test=0;test<10;test++)
                {
                    pool.DespawnAll(EnemyDespawnReason.RunReset);
                    Vector2 start=test==6 || test==7?new Vector2(0,test==6?4.5f:-4.5f):Vector2.zero;
                    brain.Life.RestoreAtCheckpoint(start,true,1,0);
                    brain.AllyLife.RestoreAtCheckpoint(new Vector2(-8,-4),true,1,0);
                    var allyTarget=brain.AllyLife.GetComponent<DeepSleep.Runtime.Combat.Targeting.PlayerCombatTarget2D>();
                    bool enabledTarget=allyTarget.enabled; allyTarget.enabled=false;
                    var allyBrain=brain==session.DeepSeekAi?session.HarnessAi:session.DeepSeekAi;
                    bool allyShapeEnabled=allyBrain.Shape.enabled; allyBrain.Shape.enabled=false;
                    brain.ResetIntent();
                    float angle=test<6?test*60:0;
                    Vector2 side=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                    Vector2 spawn=start+side*6;
                    spawn.y=Mathf.Clamp(spawn.y,-4.8f,4.8f);
                    var variation=new EnemySpawnVariation2D(Vector2.left,2.5f,0,0);
                    Require(pool.TryRent(spawn,in variation,out var arrow),"Rent actual arrow");
                    var motor=arrow.GetComponent<DownloadChargeMotor2D>();
                    Require(arrow.GetComponent<DeepSleep.Runtime.Combat.Perception.CombatPerceptionBody2D>().DownloadCharge==motor,"Explicit charge perception binding");
                    var contact=arrow.GetComponent<EnemyContactAttack2D>();
                    int hits=0,evades=0; Action<EnemyContactAttack2D,Vector2> onHit=(a,p)=>hits++;
                    contact.ImpactOccurred+=onHit;
                    var extraContacts=new System.Collections.Generic.List<EnemyContactAttack2D>();
                    if(test>=8)
                    {
                        Require(pool.TryRent(-spawn,in variation,out var opposite),"Opposite arrow");
                        extraContacts.Add(opposite.GetComponent<EnemyContactAttack2D>());
                        if(test==9)
                        {
                            Require(pool.TryRent(new Vector2(4.5f,4.5f),in variation,out var diagonal),"Third arrow");
                            extraContacts.Add(diagonal.GetComponent<EnemyContactAttack2D>());
                        }
                        foreach(var extra in extraContacts) extra.ImpactOccurred+=onHit;
                    }
                    try
                    {
                        for(int step=0;step<115 && pool.ActiveCount>0;step++)
                        {
                            Physics2D.SyncTransforms();
                            Require(brain.TryGetCommand(++tick,out var command),"AI command available");
                            if(test==8 && brain==session.DeepSeekAi && step%10==0)
                                LastReport+="trace t="+(step*.02f)+" pos="+brain.Body.position+" move="+command.Move+" arrow="+motor.State+" risk="+brain.Danger+"\n";
                            if(brain.Plan==CompanionPlan.Evade) evades++;
                            Vector2 position=brain.Body.position,velocity=brain.Body.linearVelocity;
                            Vector2 extent=brain.Shape.bounds.extents,offset=(Vector2)brain.Shape.bounds.center-position;
                            PlayerMovementStep.Calculate(ref position,ref velocity,command.Move,brain.MotorConfig,extent,offset,.02f);
                            brain.Body.position=position; brain.Body.linearVelocity=velocity;
                            // 不消费攻击/护盾命令：本测试只允许用真实移动躲过真实扫掠/触发命中。
                            foreach(var active in pool.Instances)
                                if(active.gameObject.activeSelf) active.GetComponent<DownloadChargeMotor2D>().Simulate(.02f);
                            Physics2D.Simulate(.02f);
                        }
                        total++; if(hits>0) failures++;
                        LastReport+=brain.Combat.Role+" case="+test+" impacts="+hits+" evadeTicks="+evades+" end="+brain.Body.position+"\n";
                    }
                    finally
                    {
                        contact.ImpactOccurred-=onHit;
                        foreach(var extra in extraContacts) extra.ImpactOccurred-=onHit;
                        allyTarget.enabled=enabledTarget; allyBrain.Shape.enabled=allyShapeEnabled;
                    }
                }
            }
            pool.DespawnAll(EnemyDespawnReason.RunReset);
            LastReport+="Cases="+total+" impacted="+failures+"; real prefabs/colliders/sweep/AI commands/motor; attacks and guard not consumed.\n";
            Require(failures==0,"Arrow collision cases failed: "+failures);
        }
    }
}
