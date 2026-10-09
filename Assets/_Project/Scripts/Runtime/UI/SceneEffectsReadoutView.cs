using DeepSleep.Runtime.Progression.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI
{
    /// <summary>缓存层数，只有状态变化才生成文案；倍率抵消仍保留各个状态标识。</summary>
    public sealed class SceneEffectsReadoutView : MonoBehaviour
    {
        public ChapterSceneEffectsController Source;
        public Text Label;
        public CanvasGroup Group;
        public string FastLabel, SlowLabel, ReverseLabel, SpeedLabel, NormalDirectionLabel, ReversedDirectionLabel, WarningLabel;
        private int _fast=-1,_slow=-1,_reverse=-1,_warningFast=-1,_warningSlow=-1,_warningReverse=-1;
        private void Awake()
        {
            if(Source==null || Label==null || Group==null){Debug.LogError("[SceneEffectsReadout] 缺少状态源、文字或画布组。",this);enabled=false;return;}
            Label.enabled=false;Group.alpha=0;Group.blocksRaycasts=false;Group.interactable=false;
        }
        private void LateUpdate()
        {
            int fast=Source.Count(SceneBattleEffect.DoubleSpeed),slow=Source.Count(SceneBattleEffect.HalfSpeed),reverse=Source.Count(SceneBattleEffect.InvertedMovement);
            int wf=Source.Count(SceneBattleEffect.DoubleSpeed,true),ws=Source.Count(SceneBattleEffect.HalfSpeed,true),wr=Source.Count(SceneBattleEffect.InvertedMovement,true);
            int warnings=wf+ws+wr;
            if(fast==_fast && slow==_slow && reverse==_reverse && wf==_warningFast && ws==_warningSlow && wr==_warningReverse)return;
            _fast=fast;_slow=slow;_reverse=reverse;_warningFast=wf;_warningSlow=ws;_warningReverse=wr;
            Label.enabled=fast+slow+reverse+warnings>0;
            Group.alpha=Label.enabled?1:0;
            Label.text=$"{Status(FastLabel,fast,wf)}{Status(SlowLabel,slow,ws)}{Status(ReverseLabel,reverse,wr)}\n{SpeedLabel} ×{Source.TimeMultiplier:0.##} · {(Source.InvertsMovement?ReversedDirectionLabel:NormalDirectionLabel)}";
        }
        private string Status(string label,int count,int warnings) => count+warnings==0?string.Empty:$"{label}{(count>0?" ×"+count:string.Empty)}{(warnings>0?" "+WarningLabel:string.Empty)}   ";
    }
}
