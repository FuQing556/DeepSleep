using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI;
using DeepSleep.Runtime.UI.CharacterSelection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class World02SceneEffectsChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static string RunAssets()
        {
            if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
            int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            try
            {
                var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var source=all.OfType<ChapterSceneEffectsController>().Single();
                Check(source.TryValidateConfiguration(out string reason),reason);
                var cfg=source.Config;
                Check(cfg.Waves.Length==4&&cfg.DurationSeconds==10&&cfg.WarningSeconds==2,"Ten seconds, two-second warning, waves1–4 before boss takeover");
                Check(cfg.Waves[0].TypeWeights==new Vector3(1,1,0)&&cfg.Waves[1].TypeWeights==Vector3.one&&cfg.Waves[2].TypeWeights==new Vector3(1,1,2),"User weights");
                var third=cfg.Waves[2];var fourth=cfg.Waves[3];
                Check(fourth.Wave==4&&fourth.FirstWarningSeconds==third.FirstWarningSeconds&&fourth.IntervalSeconds==third.IntervalSeconds&&fourth.TriggerProbability==third.TriggerProbability&&fourth.TypeWeights==third.TypeWeights,"Fourth-wave prelude uses third-wave environment rules");
                Check(cfg.BuildSchedule(1,4,65).Length>0&&cfg.BuildSchedule(0,1,45).Length==0,"Fourth-wave prelude draws, client waits seed");
                Check(source.BossConfig!=null&&source.BossConfig!=cfg&&source.BossConfig.Waves.Length==1&&source.BossConfig.Waves[0].FirstWarningSeconds==15&&source.BossConfig.Waves[0].IntervalSeconds==new Vector2(32,42),"Boss combat retains independent slower cadence");
                for(int wave=1;wave<=4;wave++)
                {
                    int[] totals=new int[3];int draws=0;float duration=wave==1?45:wave==2?55:wave==3?60:65;
                    for(int seed=1;seed<=2000;seed++)
                    {
                        var timeline=cfg.BuildSchedule(seed,wave,duration);var peer=cfg.BuildSchedule(seed,wave,duration);
                        Check(timeline.SequenceEqual(peer),"Same host seed yields same schedule");
                        foreach(var pulse in timeline)
                        {
                            totals[(int)pulse.Effect]++;draws++;
                            Check(pulse.DurationSeconds==10&&pulse.FirstWarningSeconds+pulse.WarningSeconds+pulse.DurationSeconds<=duration,"Duration and end cutoff");
                        }
                    }
                    var weights=cfg.Waves[wave-1].TypeWeights;float total=weights.x+weights.y+weights.z;
                    for(int type=0;type<3;type++)Check(Math.Abs((float)totals[type]/draws-weights[type]/total)<.03f,"Weighted frequency");
                }
                var one=new[]{new SceneEffectPulse{Effect=SceneBattleEffect.DoubleSpeed,FirstWarningSeconds=8,WarningSeconds=1,DurationSeconds=10}};
                Check(ChapterSceneEffectsConfig.CountAt(one,8.5f,SceneBattleEffect.DoubleSpeed,true)==1,"Warning");
                Check(ChapterSceneEffectsConfig.CountAt(one,9,SceneBattleEffect.DoubleSpeed,false)==1&&ChapterSceneEffectsConfig.CountAt(one,18.99f,SceneBattleEffect.DoubleSpeed,false)==1&&ChapterSceneEffectsConfig.CountAt(one,19,SceneBattleEffect.DoubleSpeed,false)==0,"Exact ten-second interval");
                foreach(var m in all.OfType<PlayerMovementMotor2D>())Check(m.SceneEffects==source,"Motor source");
                foreach(var p in all.OfType<NetworkMovementPrediction>())Check(p.SceneEffects==source,"Prediction source");
                Check(all.OfType<CoopSessionMenu>().Single().SceneEffects==source,"Pause source");
                var canvas=source.GetComponent<Canvas>();Check(canvas.transform.parent==null&&canvas.renderMode==RenderMode.ScreenSpaceOverlay,"Full-screen outside safe area");
                var layers=all.OfType<SceneEffectLayerView>().ToArray();Check(layers.Length==3,"Three independent layers");
                foreach(var layer in layers)
                {
                    Check(layer.Source==source&&layer.Edge.EdgeTexture!=null&&layer.Corners.Length==4,"Layer refs");
                    Check(layer.Edge.GetComponent<CanvasRenderer>()!=null,"Edge renderer");
                    var rt=layer.Edge.rectTransform;
                    Check(rt.anchorMin==Vector2.zero&&rt.anchorMax==Vector2.one&&rt.offsetMin==Vector2.zero&&rt.offsetMax==Vector2.zero,"Zero-edge margin");
                    Check(layer.Edge.Thickness>=100&&!layer.Edge.raycastTarget&&!layer.Group.blocksRaycasts,"Visible band, input pass-through");
                    var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(layer.Edge.EdgeTexture));
                    Check(importer.wrapModeU==TextureWrapMode.Mirror&&importer.wrapModeV==TextureWrapMode.Clamp&&!importer.mipmapEnabled,"No tiling discontinuity or inner wrap");
                    foreach(var corner in layer.Corners)Check(corner.GetComponent<UnityEngine.UI.Image>().preserveAspect&&!corner.GetComponent<UnityEngine.UI.Image>().raycastTarget,"Corner aspect/input");
                }
                var readout=all.OfType<SceneEffectsReadoutView>().Single();Check(readout.Source==source&&readout.Label.font!=null&&readout.Group!=null,"State readout");
                var theme=readout.GetComponent<DeepSleep.Runtime.UI.Common.UiThemeView>();
                Check(theme!=null&&theme.DeepSeek!=null&&theme.Harness!=null&&theme.Artwork.Length==2&&theme.Graphics.Any(g=>g.Target==readout.Label),"Readout uses shared DS/HS layered theme");
                Check(typeof(DeepSleep.Runtime.Combat.Enemies.EnemyMotor2D).GetMethod("BindMovementTempo")==null,"Superseded monster multiplier removed");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            foreach(string path in new[]{"Assets/Scenes/World01_EarlyInternet.unity","Assets/Scenes/Gameplay_Prototype.unity"})
            {
                var other=EditorSceneManager.OpenPreviewScene(path);
                try{foreach(var r in other.GetRootGameObjects())
                {
                    Check(r.GetComponentsInChildren<ChapterSceneEffectsController>(true).Length==0,"Other levels unchanged");
                    foreach(var m in r.GetComponentsInChildren<PlayerMovementMotor2D>(true))Check(m.SceneEffects==null,"Other motors normal");
                }}finally{EditorSceneManager.ClosePreviewScene(other);}
            }
            return "SCENE EFFECTS ASSETS PASS "+checks;
        }

        public static string RunPlay()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Isolated World02 Play required");
            var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var source=all.OfType<ChapterSceneEffectsController>().Single();var chapter=source.Chapter;
            if(!chapter.IsInitialized||chapter.IsChallenge)throw new Exception("Ordinary initialized chapter required");
            var phase=typeof(ChapterRunController).GetField("_phase",Private);var wave=typeof(ChapterRunController).GetField("_segmentNumber",Private);var seconds=typeof(ChapterRunController).GetField("_remainingCombatSeconds",Private);
            object oldPhase=phase.GetValue(chapter),oldWave=wave.GetValue(chapter),oldSeconds=seconds.GetValue(chapter);
            var original=source.Config;float oldScale=Time.timeScale;var scratch=UnityEngine.Object.Instantiate(original);
            int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
            void Set(float elapsed){wave.SetValue(chapter,2);phase.SetValue(chapter,ChapterRunPhase.Combat);seconds.SetValue(chapter,55-elapsed);}
            void Timeline(SceneEffectPulse[] pulses)
            {
                typeof(ChapterSceneEffectsController).GetField("_scheduleConfig",Private).SetValue(source,source.Config);
                typeof(ChapterSceneEffectsController).GetField("_scheduleSeed",Private).SetValue(source,chapter.SceneEffectSeed);
                typeof(ChapterSceneEffectsController).GetField("_scheduleWave",Private).SetValue(source,2);
                typeof(ChapterSceneEffectsController).GetField("_schedule",Private).SetValue(source,pulses);
            }
            var baseline=new[]{
                new SceneEffectPulse{Effect=SceneBattleEffect.DoubleSpeed,FirstWarningSeconds=12,WarningSeconds=1,DurationSeconds=4},
                new SceneEffectPulse{Effect=SceneBattleEffect.HalfSpeed,FirstWarningSeconds=14,WarningSeconds=1,DurationSeconds=4},
                new SceneEffectPulse{Effect=SceneBattleEffect.InvertedMovement,FirstWarningSeconds=24,WarningSeconds=1,DurationSeconds=4}};
            try
            {
                Timeline(baseline);
                Set(13.5f);Check(source.TimeMultiplier==2,"Double speed");Time.timeScale=1;source.ApplyCurrentState();Check(Time.timeScale==2,"Unified engine battle clock");
                Set(15.5f);source.ApplyCurrentState();Check(source.TimeMultiplier==1&&Time.timeScale==1,"Opposite states cancel");
                Check(source.Count(SceneBattleEffect.DoubleSpeed)==1&&source.Count(SceneBattleEffect.HalfSpeed)==1,"Both states retained");
                Set(17.5f);source.ApplyCurrentState();Check(source.TimeMultiplier==.5f&&Time.timeScale==.5f,"Half speed");
                Set(25.5f);Check(source.InvertsMovement&&source.TransformMovement(new Vector2(.7f,-.4f))==new Vector2(-.7f,.4f),"XY inverse preserves strength");
                Set(13.5f);Time.timeScale=0;source.ApplyCurrentState();Check(Time.timeScale==0,"Pause not stolen");
                source.Config=scratch;var stacked=new[]{
                    new SceneEffectPulse{Effect=SceneBattleEffect.DoubleSpeed,FirstWarningSeconds=12,WarningSeconds=1,DurationSeconds=4},
                    new SceneEffectPulse{Effect=SceneBattleEffect.DoubleSpeed,FirstWarningSeconds=14,WarningSeconds=1,DurationSeconds=4},
                    new SceneEffectPulse{Effect=SceneBattleEffect.InvertedMovement,FirstWarningSeconds=12,WarningSeconds=1,DurationSeconds=4},
                    new SceneEffectPulse{Effect=SceneBattleEffect.InvertedMovement,FirstWarningSeconds=14,WarningSeconds=1,DurationSeconds=4}};Timeline(stacked);
                Set(16);Check(source.TimeMultiplier==4&&!source.InvertsMovement,"Same speed stacks, two inversions cancel");
                Set(17.1f);Check(source.TimeMultiplier==2&&source.InvertsMovement,"Independent expiration");
                Set(19);Check(source.TimeMultiplier==1&&!source.InvertsMovement,"All states expired");
                stacked[0].Effect=SceneBattleEffect.HalfSpeed;stacked[1].Effect=SceneBattleEffect.HalfSpeed;
                Set(16);Check(source.TimeMultiplier==.25f,"Half speed stacks");
                Set(17.1f);
                var motor=all.OfType<PlayerMovementMotor2D>().First();var body=(Rigidbody2D)new SerializedObject(motor).FindProperty("body").objectReferenceValue;Vector2 oldVelocity=body.linearVelocity;
                body.linearVelocity=Vector2.zero;
                var command=new PlayerCommand(1,1,Vector2.right,default,default,default,default,default,default,default);
                motor.ConsumeCommand(in command,.02f);Check(body.linearVelocity.x<0,"Actual motor consumes inverted movement");body.linearVelocity=oldVelocity;
                var prediction=all.OfType<NetworkMovementPrediction>().First();var pendingType=typeof(NetworkMovementPrediction).GetNestedType("Pending",BindingFlags.NonPublic);object pending=Activator.CreateInstance(pendingType);
                pendingType.GetField("Move").SetValue(pending,source.TransformMovement(Vector2.right));pendingType.GetField("Dt").SetValue(pending,.02f);
                typeof(NetworkMovementPrediction).GetField("_velocity",Private).SetValue(prediction,Vector2.zero);
                var pos=typeof(NetworkMovementPrediction).GetField("_position",Private);pos.SetValue(prediction,Vector2.zero);Set(19);
                typeof(NetworkMovementPrediction).GetMethod("Step",Private).Invoke(prediction,new[]{pending});Check(((Vector2)pos.GetValue(prediction)).x<0,"ACK replay preserves captured direction after expiration");
                source.Config=original;Timeline(baseline);Set(15.5f);
                var readout=all.OfType<SceneEffectsReadoutView>().Single();typeof(SceneEffectsReadoutView).GetMethod("LateUpdate",Private).Invoke(readout,null);
                Check(readout.Label.text.Contains("实际速度 ×1")&&readout.Label.text.Contains("加速")&&readout.Label.text.Contains("半速"),"Cancellation readout retains statuses");
                foreach(var layer in all.OfType<SceneEffectLayerView>())
                {
                    layer.Group.alpha=.7f;float scroll=layer.Edge.Scroll;
                    typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);
                    if(source.Count(layer.Effect)>0)Check(layer.Group.alpha==.7f,"Pause freezes opacity");
                    phase.SetValue(chapter,ChapterRunPhase.Node);typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);Check(layer.Group.alpha==0,"Node clears layer even paused");phase.SetValue(chapter,ChapterRunPhase.Combat);
                }
                foreach(ChapterRunPhase state in Enum.GetValues(typeof(ChapterRunPhase)))if(state!=ChapterRunPhase.Combat)
                {phase.SetValue(chapter,state);Check(source.TimeMultiplier==1&&!source.InvertsMovement,"Non-combat reset");}
                Time.timeScale=1;source.ApplyCurrentState();Check(Time.timeScale==1,"Non-combat normal scale");
                phase.SetValue(chapter,ChapterRunPhase.Combat);Set(13.5f);Time.timeScale=0;
                var menu=all.OfType<CoopSessionMenu>().Single();typeof(CoopSessionMenu).GetField("_ownsPause",Private).SetValue(menu,true);typeof(CoopSessionMenu).GetField("_previousTimeScale",Private).SetValue(menu,.5f);
                typeof(CoopSessionMenu).GetMethod("RestorePause",Private).Invoke(menu,null);Check(Time.timeScale==2,"Menu resumes current state not stale saved multiplier");
            }
            finally{source.Config=original;typeof(ChapterSceneEffectsController).GetField("_scheduleConfig",Private).SetValue(source,null);UnityEngine.Object.Destroy(scratch);phase.SetValue(chapter,oldPhase);wave.SetValue(chapter,oldWave);seconds.SetValue(chapter,oldSeconds);Time.timeScale=oldScale;}
            return "SCENE EFFECTS PLAY PASS "+checks;
        }
    }
}
