using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class PresentationPolishChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static string RunWorld02Play()
        {
            var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
            var pool=all.OfType<OneShotSpriteEffectPool2D>().First();
            uint recycled=pool.RecycledCount;int played=0;Action<Vector2,float> observe=(_,__)=>played++;pool.Played+=observe;
            try{for(int i=0;i<1024;i++)Check(pool.TryPlay(Vector2.zero,0),"New effect retained under overload");}
            finally{pool.Played-=observe;}
            Check(played==1024&&pool.RecycledCount>recycled,"Replicated play events retained");
            var config=(OneShotSpriteEffectConfig)typeof(OneShotSpriteEffectPool2D).GetField("_config",Private).GetValue(pool);
            Check(pool.TotalCount==config.MaximumCount&&pool.ActiveCount==pool.TotalCount,"Bounded effect capacity");
            pool.enabled=false;pool.enabled=true;Check(pool.ActiveCount==0,"Effect pool reset has no duplicates");
            Check(pool.TryPlay(Vector2.zero,0)&&pool.ActiveCount==1,"Effect available after reset");pool.enabled=false;pool.enabled=true;
            var numbers=all.OfType<CombatDamageNumberPresenter2D>().Single();uint before=numbers.PlayedCount,drop=numbers.CapacityDropCount;
            for(int i=0;i<1024;i++)numbers.ShowReplicaDamage(Vector2.zero,1,(i&1)!=0);
            Check(numbers.PlayedCount-before==1024&&numbers.CapacityDropCount==drop&&numbers.RecycledCount>0,"No new number dropped");
            Check(numbers.ActiveCount==256,"Bounded number capacity");numbers.enabled=false;numbers.enabled=true;
            Check(numbers.ActiveCount==0,"Number pool reset");numbers.ShowReplicaDamage(Vector2.zero,1,false);Check(numbers.ActiveCount==1,"Number reuse");numbers.enabled=false;numbers.enabled=true;
            var hs=all.OfType<HarnessLaserHitEffectPresenter2D>().Single();uint hsPlayed=hs.PlayedCount;
            for(int i=0;i<1024;i++)hs.PlayImpact(Vector2.zero,Vector2.right,.2f);
            Check(hs.PlayedCount-hsPlayed==1024&&hs.RecycledCount>0&&hs.TotalCount==128&&hs.ActiveCount==128,"HS bounded overload retains new hits");
            hs.enabled=false;hs.enabled=true;Check(hs.ActiveCount==0,"HS reset");hs.PlayImpact(Vector2.zero,Vector2.right,.2f);Check(hs.ActiveCount==1,"HS reuse");hs.enabled=false;hs.enabled=true;
            var theme=all.OfType<SceneEffectsReadoutView>().Single().GetComponent<DeepSleep.Runtime.UI.Common.UiThemeView>();
            theme.Apply(DeepSleep.Runtime.Players.Identity.PlayerRole.DeepSeek);
            Check(theme.Artwork.All(b=>b.Target.sprite==theme.DeepSeek.GetArtwork(b.Kind)),"Readout DS theme layers");
            theme.Apply(DeepSleep.Runtime.Players.Identity.PlayerRole.Harness);
            Check(theme.Artwork.All(b=>b.Target.sprite==theme.Harness.GetArtwork(b.Kind)),"Readout HS theme layers");
            theme.Apply(DeepSleep.Runtime.UI.Common.UiThemePreferences.Current);

            var src=all.OfType<ChapterSceneEffectsController>().Single();var chapter=src.Chapter;
            var type=typeof(ChapterRunController);var wave=type.GetField("_segmentNumber",Private);var phase=type.GetField("_phase",Private);var seconds=type.GetField("_remainingCombatSeconds",Private);
            var oldWave=wave.GetValue(chapter);var oldPhase=phase.GetValue(chapter);var oldSeconds=seconds.GetValue(chapter);float scale=Time.timeScale;
            var layer=all.OfType<SceneEffectLayerView>().First(l=>l.Effect==SceneBattleEffect.DoubleSpeed);
            try
            {
                wave.SetValue(chapter,1);phase.SetValue(chapter,ChapterRunPhase.Combat);Time.timeScale=1;
                typeof(ChapterSceneEffectsController).GetField("_scheduleConfig",Private).SetValue(src,src.Config);
                typeof(ChapterSceneEffectsController).GetField("_scheduleSeed",Private).SetValue(src,chapter.SceneEffectSeed);
                typeof(ChapterSceneEffectsController).GetField("_scheduleWave",Private).SetValue(src,1);
                typeof(ChapterSceneEffectsController).GetField("_schedule",Private).SetValue(src,new[]{new SceneEffectPulse{Effect=SceneBattleEffect.DoubleSpeed,FirstWarningSeconds=1,WarningSeconds=2,DurationSeconds=10}});
                int[] order={0,1,3,2};
                for(int step=0;step<4;step++)
                {
                    seconds.SetValue(chapter,45-(1+step*.5f));typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);
                    Check(!layer.Edge.enabled,"No warning border");
                    for(int rank=0;rank<4;rank++)Check((layer.Corners[order[rank]].GetComponent<UnityEngine.UI.Image>().color.a>0)==(rank<=step),"Sequential retained corners");
                }
                seconds.SetValue(chapter,42.01f);typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);
                Check(!layer.Edge.enabled&&src.TimeMultiplier==1,"Fourth corner stays alone until full two-second warning ends");
                seconds.SetValue(chapter,42f);typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);
                Check(layer.Edge.enabled&&src.TimeMultiplier==2,"Border and gameplay activate together");
                seconds.SetValue(chapter,33.1f);typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);float a=layer.Group.alpha;
                seconds.SetValue(chapter,32.9f);typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);Check(Math.Abs(layer.Group.alpha-a)>.05f,"Last two seconds blink");
                Time.timeScale=0;float frozen=layer.Group.alpha;typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);Check(layer.Group.alpha==frozen,"Paused blink frozen");
                phase.SetValue(chapter,ChapterRunPhase.Node);typeof(SceneEffectLayerView).GetMethod("LateUpdate",Private).Invoke(layer,null);Check(layer.Group.alpha==0&&!layer.Edge.enabled,"Node clears visuals");
            }
            finally{wave.SetValue(chapter,oldWave);phase.SetValue(chapter,oldPhase);seconds.SetValue(chapter,oldSeconds);Time.timeScale=scale;typeof(ChapterSceneEffectsController).GetField("_scheduleConfig",Private).SetValue(src,null);}
            return "PRESENTATION POLISH PLAY PASS "+checks;
        }

        public static string RunKimiFocusPlay()
        {
            KimiLaserPattern2D laser=null;
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()){var found=root.GetComponentInChildren<KimiLaserPattern2D>(true);if(found!=null)laser=found;}
            if(laser==null)throw new Exception("World01 Kimi scene required");
            var c=laser.Config;Vector2 direction=new Vector2(-1,.3f).normalized,origin=laser.Muzzle.position;
            laser.ApplyReplica(KimiLaserState.Charging,0,origin,direction);
            float start=laser.Focus.transform.eulerAngles.z;
            laser.ApplyReplica(KimiLaserState.Charging,c.ChargeSeconds*.5f,origin,direction);
            float mid=laser.Focus.transform.eulerAngles.z;
            laser.ApplyReplica(KimiLaserState.Charging,c.ChargeSeconds,origin,direction);
            float end=laser.Focus.transform.eulerAngles.z;
            laser.ApplyReplica(KimiLaserState.Firing,0,origin,direction);
            float fire=laser.Focus.transform.eulerAngles.z;
            laser.Cancel();
            if(Math.Abs(Mathf.DeltaAngle(end,fire))>.001f||Math.Abs(Mathf.DeltaAngle(start,end))<1||Math.Abs(Mathf.DeltaAngle(mid,end))>=Math.Abs(Mathf.DeltaAngle(start,end)))throw new Exception("Focus rotation discontinuity");
            return "KIMI FOCUS PLAY PASS: start="+start+", mid="+mid+", aligned="+end+", fired="+fire;
        }
    }
}
