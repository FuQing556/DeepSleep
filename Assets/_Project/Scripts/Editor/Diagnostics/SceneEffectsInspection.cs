using System;
using System.Text;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>用户主动触发的只读诊断；不重新抽取，不改存档或玩法配置。</summary>
    public static class SceneEffectsInspection
    {
        public static string RunReticleSorting()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
            int checkedScenes=0;
            foreach(var path in new[]{"Assets/Scenes/World02_2066.unity","Assets/Scenes/World01_EarlyInternet.unity","Assets/Scenes/Gameplay_Prototype.unity"})
            {
                var scene=EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    int found=0;
                    foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                        if(renderer.name=="TargetReticle")
                        {
                            found++;
                            if(renderer.sprite==null||renderer.sortingLayerName!="UIWorld"||renderer.GetComponentInParent<UnityEngine.Rendering.SortingGroup>()!=null)
                                throw new Exception("HS reticle must be an independent UIWorld renderer: "+path);
                        }
                    if(found!=1)throw new Exception("Expected one HS reticle: "+path);
                    checkedScenes++;
                }
                finally{EditorSceneManager.ClosePreviewScene(scene);}
            }
            return "HS RETICLE SORTING PASS "+checkedScenes+" scenes";
        }

        [MenuItem("DeepSleep/调试/打印本局场景状态抽取记录")]
        private static void InspectMenu()=>Debug.Log(RunCurrent());

        public static string RunCurrent()
        {
            if(!EditorApplication.isPlaying)return "请在2066播放模式使用。";
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var source=root.GetComponentInChildren<ChapterSceneEffectsController>(true);
                if(source==null)continue;
                var chapter=source.Chapter;
                if(!chapter.IsInitialized)return "章节尚未初始化。";
                var report=new StringBuilder("SCENE EFFECT DRAW RECORD（预生成时间表，含尚未触发事件；不是历史触发日志）\n");
                report.AppendLine($"Seed={chapter.SceneEffectSeed}, Wave={chapter.SegmentNumber}, Phase={chapter.Phase}, Elapsed={source.ElapsedSeconds:0.00}s");
                for(int wave=1;wave<=chapter.LevelBindings.Level.ChapterRunConfig.CombatSegmentCount;wave++)
                {
                    report.AppendLine($"Wave {wave}:");
                    float duration=chapter.LevelBindings.Level.ChapterRunConfig.GetSegment(wave).DurationSeconds;
                    foreach(var p in source.Config.BuildSchedule(chapter.SceneEffectSeed,wave,duration))
                        report.AppendLine($"  {p.Effect}: warning {p.FirstWarningSeconds:0.00}s, active [{p.FirstWarningSeconds+p.WarningSeconds:0.00}, {p.FirstWarningSeconds+p.WarningSeconds+p.DurationSeconds:0.00})s");
                }
                report.AppendLine($"Now Fast={source.Count(SceneBattleEffect.DoubleSpeed)}, Slow={source.Count(SceneBattleEffect.HalfSpeed)}, Reverse={source.Count(SceneBattleEffect.InvertedMovement)}, Speed={source.TimeMultiplier}, Inverted={source.InvertsMovement}");
                return report.ToString();
            }
            return "当前场景没有场景状态控制器。";
        }

        public static string RunDistribution(ChapterSceneEffectsConfig config)
        {
            var random=new System.Random(20261008);
            var report=new StringBuilder();
            for(int wave=1;wave<=3;wave++)
            {
                float duration=wave==1?45:wave==2?55:60;
                long[] counts=new long[3];long draws=0,inverted=0,overlaps=0,samples=0;
                for(int run=0;run<10000;run++)
                {
                    var pulses=config.BuildSchedule(random.Next(1,int.MaxValue),wave,duration);
                    foreach(var p in pulses){counts[(int)p.Effect]++;draws++;}
                    for(int step=0;step<(int)(duration*10);step++)
                    {
                        float elapsed=step*.1f;
                        int f=ChapterSceneEffectsConfig.CountAt(pulses,elapsed,SceneBattleEffect.DoubleSpeed,false);
                        int s=ChapterSceneEffectsConfig.CountAt(pulses,elapsed,SceneBattleEffect.HalfSpeed,false);
                        int r=ChapterSceneEffectsConfig.CountAt(pulses,elapsed,SceneBattleEffect.InvertedMovement,false);
                        if((r&1)!=0)inverted++;if(f+s+r>1)overlaps++;samples++;
                    }
                }
                var weights=config.Waves[wave-1].TypeWeights;
                for(int kind=0;kind<3;kind++)if(Math.Abs((double)counts[kind]/draws-weights[kind]/(weights.x+weights.y+weights.z))>.015)
                    throw new Exception("Draw frequency mismatch at wave "+wave);
                report.AppendLine($"Wave{wave}: draws={draws}, Fast={counts[0]} ({100d*counts[0]/draws:0.00}%), Slow={counts[1]} ({100d*counts[1]/draws:0.00}%), Reverse={counts[2]} ({100d*counts[2]/draws:0.00}%), active inversion={100d*inverted/samples:0.00}% of whole wave, overlapping={100d*overlaps/samples:0.00}% of whole wave");
            }
            return report.ToString();
        }
    }
}
