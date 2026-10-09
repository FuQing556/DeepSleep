using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudeEncounterChecks
    {
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Awake(object o)=>o.GetType().GetMethod("Awake",Private)?.Invoke(o,null);
        [MenuItem("DeepSleep/Diagnostics/Claude Encounter")]
        private static void Menu()=>Debug.Log(Run());
        public static string Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Run outside Play.");
            var original=SceneManager.GetActiveScene();
            float originalTimeScale=Time.timeScale;
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var physics=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);int checks=0;
            void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
            try
            {
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var e=all.OfType<ClaudeEncounter2D>().Single();var d=all.OfType<ClaudeChapterEncounterDriver2D>().Single();
                Check(e.TryValidateConfiguration(out _) && d.TryValidateConfiguration(out _),"Encounter assembly");
                Check(e.Config.PreludeSeconds==20 && e.Config.CastGapOne==1.2f && e.Config.CastGapTwo==.9f &&
                    e.Config.CastsPerCycle==5 && e.Config.CycleGapSeconds==3 && e.Config.PhaseChangeSeconds==2,"Approved timing");
                Check(e.Permissions.Config.WarningSeconds==0 && e.Config.PermissionCastSeconds==1.5f &&
                    e.Config.PermissionRecoverySeconds==5f,"Immediate books and five-second real-time recovery after cast");
                var chapter=new SerializedObject(d.Chapter);var objectives=chapter.FindProperty("_additionalObjectiveComponents");
                Check(Enumerable.Range(0,objectives.arraySize).Count(i=>objectives.GetArrayElementAtIndex(i).objectReferenceValue==d)==1,
                    "One registered chapter objective");
                var loop=new SerializedObject(all.OfType<FixedSimulationLoop>().Single());var steps=loop.FindProperty("worldStepComponents");
                Check(Enumerable.Range(0,steps.arraySize).Count(i=>steps.GetArrayElementAtIndex(i).objectReferenceValue==d)==1 &&
                    !Enumerable.Range(0,steps.arraySize).Any(i=>steps.GetArrayElementAtIndex(i).objectReferenceValue==e.Energy ||
                        steps.GetArrayElementAtIndex(i).objectReferenceValue==e.SecondaryEnergy ||
                        steps.GetArrayElementAtIndex(i).objectReferenceValue==e.SpatialCut || steps.GetArrayElementAtIndex(i).objectReferenceValue==e.TrackingCut),
                    "No double simulation of Claude modules");
                Awake(e.Actor.Body);Awake(e.Actor.PoseTransition);Awake(e.Actor.HitEffects);Awake(e.Actor);
                Awake(e.Energy);Awake(e.SecondaryEnergy);Awake(e.SpatialCut);Awake(e.TrackingCut);Awake(e);
                for(int i=0;i<2;i++)
                {
                    SceneManager.MoveGameObjectToScene(e.Actor.Targets[i].transform.root.gameObject,physics);
                    Awake(e.Actor.Targets[i].GetComponent<HealthComponent>());
                    Awake(e.Actor.Targets[i].GetComponent<PlayerDamageReceiver2D>());Awake(e.Actor.Targets[i]);
                    e.Actor.Targets[i].transform.position=new Vector2(-30,-20-i*5);
                }
                foreach(var book in e.Permissions.Books){book.Clear();Awake(book.GetComponent<DamageHitbox2D>());}
                int takeover=0,battle=0,complete=0;
                e.TakeoverRequested+=()=>takeover++;e.BattleStarted+=()=>battle++;e.Completed+=()=>complete++;
                e.Actor.FacingVisual.flipX = true;
                Check(e.Begin(931),"Begin prelude");
                Check(e.Actor.FacingVisual.flipX == (e.Config.EntryPosition.x < 0), "Entry facing uses new position, not previous actor position");
                e.Simulate(39.98f,19.99f);
                Check(e.State==ClaudeEncounterState.Prelude && takeover==0 && !e.Actor.Body.IsAlive,"Prelude consumes real seconds");
                e.Simulate(.02f,.01f);
                Check(e.State==ClaudeEncounterState.Entering && takeover==1 && e.Actor.Body.IsAlive && !e.Actor.Body.CanReceiveDamage,"20 real seconds begins protected entrance");
                e.Presentation.Advance(2.99f,false);e.Simulate(.1f,.1f);
                Check(e.State==ClaudeEncounterState.Entering,"Night transition blocks early attacks");
                e.Presentation.Advance(1.76f,false);e.Simulate(.05f,.05f);
                Check(e.State==ClaudeEncounterState.Casting && battle==1 && e.CastsStarted==1 && e.Actor.Body.CurrentHealth==10000,"Entry pause ends once and first cast starts without an extra gap");
                // Advance fixed-sized ticks; players remain outside the arena to avoid fixture deaths.
                var seen=new bool[4];ClaudeSkill previous=e.Skill;int last=e.CastsStarted;
                seen[(int)e.Skill] = true;
                bool hadBooks=e.Permissions.HasOpenBooks;
                float bookStartedAt = e.Skill==ClaudeSkill.PermissionBooks ? e.BattleRealSeconds : -1;
                if (e.Skill==ClaudeSkill.PermissionBooks)
                    Check(e.Actor.Pose==ClaudePose.Seal && e.Permissions.Books.Where(b=>b.IsOpen).All(b=>b.State==ClaudeBookState.Sealed && b.RemainingSeconds==10),
                        "Opening cast can immediately summon sealed books");
                int bookRecoveryChecks = 0;
                int stationaryRestChecks = 0, ordinaryMovingGapChecks = 0;
                var stationaryField=typeof(ClaudeEncounter2D).GetField("_stationaryGap",Private);
                var ageField=typeof(ClaudeEncounter2D).GetField("_age",Private);
                var gapField=typeof(ClaudeEncounter2D).GetField("_gap",Private);
                for(int n=0;n<2400;n++)
                {
                    bool wasResting=e.State==ClaudeEncounterState.Gap && (bool)stationaryField.GetValue(e);
                    Vector3 previousPosition=e.Actor.transform.position;
                    float previousAge=(float)ageField.GetValue(e);
                    e.Simulate(.05f,.025f);
                    if(wasResting && previousAge+.05f<e.Config.CycleGapSeconds-.001f)
                    {
                        Check(e.State==ClaudeEncounterState.Gap && e.Actor.Pose==ClaudePose.Idle &&
                            e.Actor.transform.position==previousPosition,"Cycle recovery stands still in idle pose");
                        Check(Mathf.Abs((float)gapField.GetValue(e)-3)<.001f &&
                            Mathf.Abs((float)ageField.GetValue(e)-previousAge-.05f)<.001f,
                            "Cycle rest retains existing three game-second duration");
                        stationaryRestChecks++;
                    }
                    if(e.State==ClaudeEncounterState.Gap && !(bool)stationaryField.GetValue(e))
                    {
                        Check(e.Actor.Pose==ClaudePose.Move,"Ordinary and book-specific recovery keep move pose");
                        ordinaryMovingGapChecks++;
                    }
                    if(!hadBooks && e.Permissions.HasOpenBooks)
                        Check(e.State==ClaudeEncounterState.Casting && e.Skill==ClaudeSkill.PermissionBooks &&
                            e.Actor.Pose==ClaudePose.Seal,"Books only spawn as a dedicated seal cast");
                    hadBooks=e.Permissions.HasOpenBooks;
                    if(e.CastsStarted!=last)
                    {
                        if(last>0)Check(previous!=e.Skill,"No consecutive skill repetition");
                        if (last > 0 && previous == ClaudeSkill.PermissionBooks)
                        {
                            float expectedBookGap = e.Config.PermissionCastSeconds + e.Config.PermissionRecoverySeconds;
                            Check(e.BattleRealSeconds - bookStartedAt >= expectedBookGap - .001f && e.BattleRealSeconds - bookStartedAt < expectedBookGap + .06f,
                                "Book cast plus configured recovery uses real seconds even at doubled game speed");
                            bookRecoveryChecks++;
                        }
                        previous=e.Skill;last=e.CastsStarted;seen[(int)e.Skill]=true;
                        if(e.Skill==ClaudeSkill.PermissionBooks)
                        {
                            bookStartedAt = e.BattleRealSeconds;
                            Check(e.Actor.Pose==ClaudePose.Seal && e.Permissions.Books.Count(b=>b.IsOpen)==2,
                                "Seal is counted as a regular cast and shows its own pose");
                            Check(e.Permissions.Books.Where(b=>b.IsOpen).All(b=>b.State==ClaudeBookState.Sealed && b.RemainingSeconds==10),
                                "Books seal during summon pose, no warning phase");
                        }
                    }
                    Check(e.State!=ClaudeEncounterState.Idle,"No silent cancellation during moving casts");
                    Vector2 center=(Vector2)e.Actor.transform.position+(Vector2)e.Actor.Body.Sphere.transform.TransformVector(e.Actor.Body.Sphere.offset);
                    Check(e.Playfield.WorldBounds.Contains(center),"Wrapped sphere center remains inside playfield");
                }
                Check(seen.All(x=>x) && e.CastsStarted>5,"All four skills cycle beyond one round");
                Check(bookRecoveryChecks > 0,"Observed at least one complete book recovery");
                Check(stationaryRestChecks>0 && ordinaryMovingGapChecks>0,"Observed stationary cycle rest and ordinary moving recovery");
                Check(e.BattleRealSeconds>59 && e.BattleRealSeconds<61,"Permission clock is real time, not doubled game time");
                // Repeated edge launches used to occasionally put the muzzle exactly
                // on Rect's excluded upper edge and cancel the whole encounter.
                for (int n=0;n<7200;n++)
                {
                    e.Simulate(.05f,.025f);
                    Check(e.State!=ClaudeEncounterState.Idle,"Long-run energy edge launch must not cancel encounter");
                }
                // Reach half HP only during a gap so the phase boundary is deterministic.
                while(e.State==ClaudeEncounterState.Casting)e.Simulate(.05f,.025f);
                var lethal=new DamagePacket(99999,Vector2.zero,Vector2.left,null);
                e.Actor.Body.TryReceiveDamage(in lethal);e.Simulate(.05f,.025f);
                Check(e.State==ClaudeEncounterState.PhaseChange && e.Actor.Body.CurrentHealth==5000 && !e.Permissions.HasOpenBooks,
                    "Phase boundary locks HP and returns every permission");
                Check(!e.Actor.Body.TryReceiveDamage(in lethal),"Phase pose is protected");
                e.Simulate(3.98f,1.99f);
                Check(e.State==ClaudeEncounterState.PhaseChange,"2 real-second phase pose at doubled game speed");
                e.Simulate(.02f,.01f);
                Check(e.Actor.Body.PhaseTwo && e.State==ClaudeEncounterState.Gap,"Phase II unlocks after pose");
                int waiting=0;
                while((e.State!=ClaudeEncounterState.Casting || e.Skill!=ClaudeSkill.Energy) && waiting++<4000)
                    e.Simulate(.05f,.025f);
                Check(waiting<4000,"Natural phase two energy cast reached");
                // 从场中央验证重叠；边沿向外发射会在同一刻碰墙爆炸，不能用于重叠断言。
                e.Actor.transform.position=Vector2.zero;
                // Launch now samples collider centers, rather than the earlier charge-start
                // point. Keep both fixture targets horizontal and outside the arena so the
                // first shot has a measurable flight without damaging fixture players.
                for(int i=0;i<2;i++) e.Actor.Targets[i].transform.position=new Vector2(-30,0);
                Physics2D.SyncTransforms();
                int firstFires=0,secondFires=0;
                Action onFirst=()=>firstFires++,onSecond=()=>secondFires++;
                e.Energy.Fired+=onFirst;e.SecondaryEnergy.Fired+=onSecond;
                bool simultaneous=false, releaseHeld=false, returnedToMove=false;int energyTicks=0;
                float sinceSecondFire=0;
                while(e.State==ClaudeEncounterState.Casting && e.Skill==ClaudeSkill.Energy && energyTicks++<600)
                {
                    e.Simulate(.05f,.025f);
                    if(secondFires>0 && e.State==ClaudeEncounterState.Casting)
                    {
                        if(sinceSecondFire<.24f)
                            Check(e.Actor.Pose==ClaudePose.EnergyRelease,"Final launch briefly holds release pose");
                        releaseHeld |= e.Actor.Pose==ClaudePose.EnergyRelease;
                        if(sinceSecondFire>.26f)
                        {
                            Check(e.Actor.Pose==ClaudePose.Move,"Release returns to move while balls still resolve");
                            returnedToMove=true;
                        }
                        sinceSecondFire+=.025f;
                    }
                    simultaneous |= e.Energy.State==ClaudeEnergyState.Flying && e.SecondaryEnergy.State==ClaudeEnergyState.Charging;
                    if(e.SecondaryEnergy.State==ClaudeEnergyState.Charging)
                        Check(e.Actor.Pose==ClaudePose.EnergyCharge && e.SecondaryEnergy.CurrentHealth==999,
                            "Second charge owns boss pose and 999HP");
                }
                e.Energy.Fired-=onFirst;e.SecondaryEnergy.Fired-=onSecond;
                Check(releaseHeld && returnedToMove,"Short release hold is independent of flight/explosion lifetime");
                Check(simultaneous && firstFires==1 && secondFires==1 && energyTicks<600,
                    "Exactly two consecutive launches with overlap/completion: first="+firstFires+", second="+secondFires+
                    ", overlap="+simultaneous+", ticks="+energyTicks+", state="+e.State);
                e.Actor.Body.TryReceiveDamage(in lethal);
                Check(e.State==ClaudeEncounterState.Complete && complete==1 && !e.Permissions.HasOpenBooks &&
                    e.Actor.Pose==ClaudePose.Defeated && !e.Energy.Shape.enabled,"Defeat cancels modules and retains defeated pose");
                e.Simulate(20,20);Check(complete==1,"No repeat completion");
                e.Cancel();e.Presentation.ResetPresentation();
                Check(e.State==ClaudeEncounterState.Idle && !e.Actor.Body.IsShown && !e.Permissions.HasOpenBooks,"Exit/reset cleanup");
                var effects=all.OfType<ChapterSceneEffectsController>().Single();Awake(effects);
                typeof(ChapterRunController).GetField("_isInitialized",Private).SetValue(effects.Chapter,true);
                typeof(ChapterRunController).GetField("_phase",Private).SetValue(effects.Chapter,ChapterRunPhase.Combat);
                typeof(ChapterRunController).GetField("_remainingCombatSeconds",Private).SetValue(effects.Chapter,999f);
                effects.BeginBossClock(927,false);
                foreach(SceneBattleEffect effect in Enum.GetValues(typeof(SceneBattleEffect)))
                    Check(effects.Count(effect)==0 && effects.Count(effect,true)==0,"Takeover clears ordinary scene states");
                effects.BeginBossClock(927,true);
                var full=effects.ActiveBossConfig.BuildSchedule(927,3,300);
                Check(effects.BossConfig != null && effects.BossConfig != effects.Config,
                    "Boss cadence is independent from ordinary waves");
                Check(full.Length > 0 && full[0].FirstWarningSeconds == 15 &&
                    effects.BossConfig.WarningSeconds == 2 && effects.BossConfig.DurationSeconds == 10,
                    "Boss first warning/timing");
                Check(effects.Config.Waves[2].FirstWarningSeconds == 5 &&
                    effects.Config.Waves[2].IntervalSeconds == new Vector2(6,8), "Third wave unchanged");
                for(int i=1;i<full.Length;i++)
                {
                    float interval=full[i].FirstWarningSeconds-full[i-1].FirstWarningSeconds;
                    Check(interval>=31.999f && interval<=42.001f && interval-12>=19.999f,
                        "Boss warnings leave at least twenty seconds quiet time");
                }
                for(float elapsed=0;elapsed<240;elapsed+=.5f)
                {
                    typeof(ChapterSceneEffectsController).GetField("_bossElapsed",Private).SetValue(effects,elapsed);
                    foreach(SceneBattleEffect effect in Enum.GetValues(typeof(SceneBattleEffect)))
                    {
                        Check(effects.Count(effect)==ChapterSceneEffectsConfig.CountAt(full,elapsed,effect,false),"Extended boss schedule retains exact pulse prefix");
                        Check(effects.Count(effect,true)==ChapterSceneEffectsConfig.CountAt(full,elapsed,effect,true),"Extended warnings remain consistent");
                    }
                }
                effects.EndBossClock();
                return $"Claude Encounter: {checks} checks passed; isolated Editor, no network/device or natural chapter acceptance.";
            }
            finally{EditorSceneManager.CloseScene(physics,true);EditorSceneManager.ClosePreviewScene(scene);SceneManager.SetActiveScene(original);Time.timeScale=originalTimeScale;}
        }
    }
}
