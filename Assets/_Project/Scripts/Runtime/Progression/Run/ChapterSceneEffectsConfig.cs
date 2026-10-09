using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    public enum SceneBattleEffect { DoubleSpeed, HalfSpeed, InvertedMovement }

    [Serializable]
    public struct SceneEffectWaveRule
    {
        [Min(1)] public int Wave;
        [Min(0)] public float FirstWarningSeconds;
        public Vector2 IntervalSeconds;
        [Range(0,1)] public float TriggerProbability;
        public Vector3 TypeWeights; // 加速、半速、颠倒
    }

    public struct SceneEffectPulse
    {
        public SceneBattleEffect Effect;
        public float FirstWarningSeconds, WarningSeconds, DurationSeconds;
    }

    [CreateAssetMenu(menuName = "DeepSleep/Progression/Scene Effects", fileName = "CFG_SceneEffects_")]
    public sealed class ChapterSceneEffectsConfig : ScriptableObject
    {
        public SceneEffectWaveRule[] Waves;
        public float WarningSeconds=2;
        public float DurationSeconds=10;
        public float StopBeforeEndSeconds=12;

        public bool TryValidate(out string reason)
        {
            if(Waves==null||!Positive(WarningSeconds)||!Positive(DurationSeconds)||!Positive(StopBeforeEndSeconds)||StopBeforeEndSeconds<WarningSeconds+DurationSeconds)
            {reason="状态时长、预警或末尾停止窗口无效。";return false;}
            for(int i=0;i<Waves.Length;i++)
            {
                var r=Waves[i];var w=r.TypeWeights;
                if(r.Wave<1||!float.IsFinite(r.FirstWarningSeconds)||r.FirstWarningSeconds<0||
                    !Positive(r.IntervalSeconds.x)||!Positive(r.IntervalSeconds.y)||r.IntervalSeconds.y<r.IntervalSeconds.x||
                    !float.IsFinite(r.TriggerProbability)||r.TriggerProbability<0||r.TriggerProbability>1||
                    !float.IsFinite(w.x)||!float.IsFinite(w.y)||!float.IsFinite(w.z)||w.x<0||w.y<0||w.z<0||w.x+w.y+w.z<=0)
                {reason="状态波次、间隔、概率或权重无效。";return false;}
                for(int j=0;j<i;j++)if(Waves[j].Wave==r.Wave){reason="每波只配置一个抽取规则。";return false;}
            }
            reason = string.Empty; return true;
        }

        private static bool Positive(float n)=>float.IsFinite(n)&&n>0;

        // 房主seed与关卡配置确定整波抽取；不消费Unity全局随机数，不按帧重新抽。
        public SceneEffectPulse[] BuildSchedule(int seed,int wave,float waveDuration)
        {
            if(seed==0)return Array.Empty<SceneEffectPulse>();
            foreach(var r in Waves)if(r.Wave==wave)
            {
                uint state=unchecked((uint)seed ^ ((uint)wave*0x9E3779B9u));if(state==0)state=1;
                var result=new List<SceneEffectPulse>();
                for(float at=r.FirstWarningSeconds;at<=waveDuration-StopBeforeEndSeconds;)
                {
                    if(Next(ref state)<r.TriggerProbability)
                    {
                        var w=r.TypeWeights;float roll=Next(ref state)*(w.x+w.y+w.z);
                        var effect=roll<w.x?SceneBattleEffect.DoubleSpeed:roll<w.x+w.y?SceneBattleEffect.HalfSpeed:SceneBattleEffect.InvertedMovement;
                        result.Add(new SceneEffectPulse{Effect=effect,FirstWarningSeconds=at,WarningSeconds=WarningSeconds,DurationSeconds=DurationSeconds});
                    }
                    at+=Mathf.Lerp(r.IntervalSeconds.x,r.IntervalSeconds.y,Next(ref state));
                }
                return result.ToArray();
            }
            return Array.Empty<SceneEffectPulse>();
        }

        private static float Next(ref uint state)
        {state^=state<<13;state^=state>>17;state^=state<<5;return (state>>8)*(1f/16777216f);}

        public static int CountAt(SceneEffectPulse[] pulses,float elapsed,SceneBattleEffect effect,bool warning)
        {
            int count=0;
            foreach(var p in pulses)
            {
                float start=p.FirstWarningSeconds+(warning?0:p.WarningSeconds);
                if(p.Effect==effect&&elapsed>=start&&elapsed<start+(warning?p.WarningSeconds:p.DurationSeconds))count++;
            }
            return count;
        }
    }
}
