using DeepSleep.Runtime.Progression.Run;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI
{
    /// <summary>一状态一组独立边条/角落，动画用真实章节秒，暂停不流动，叠加不合并为白光。</summary>
    public sealed class SceneEffectLayerView : MonoBehaviour
    {
        public ChapterSceneEffectsController Source;
        public SceneBattleEffect Effect;
        public CanvasGroup Group;
        public SceneEffectEdgeGraphic Edge;
        public RectTransform[] Corners;
        [Range(0,1)] public float ActiveOpacity;
        [Range(0,1)] public float WarningOpacity;
        public float ScrollSpeed;
        [Min(0)] public float BreathSeconds;
        [Range(0,1)] public float BreathAmount;
        [Min(0)] public float CornerMotion;
        [Min(0)] public float FadeSeconds;
        [Min(0)] public float EndingWarningSeconds;
        [Min(0)] public float EndingBlinkCyclesPerSecond;
        [Range(0,1)] public float EndingBlinkMinimumAlpha;
        private Vector3[] _cornerScales;
        private Image[] _cornerImages;
        private Color[] _cornerColors;

        private void Awake()
        {
            if (Source == null || Group == null || Edge == null || Corners == null || BreathSeconds <= 0 || FadeSeconds <= 0)
            { Debug.LogError("[SceneEffectLayerView] 缺少源、边条、角落、组或动画参数。",this); enabled=false; return; }
            if(Corners.Length!=4||EndingWarningSeconds<=0||EndingBlinkCyclesPerSecond<=0)
            {Debug.LogError("[SceneEffectLayerView] 需要四角及结束闪烁参数。",this);enabled=false;return;}
            _cornerScales = new Vector3[Corners.Length];
            _cornerImages=new Image[Corners.Length];_cornerColors=new Color[Corners.Length];
            for(int i=0;i<Corners.Length;i++)
            {
                _cornerScales[i]=Corners[i].localScale;
                _cornerImages[i]=Corners[i].GetComponent<Image>();
                if(_cornerImages[i]==null){Debug.LogError("[SceneEffectLayerView] 角落缺少Image。",this);enabled=false;return;}
                _cornerColors[i]=_cornerImages[i].color;
            }
            Edge.enabled=false;
            Group.alpha=0; Group.blocksRaycasts=false; Group.interactable=false;
        }

        private void LateUpdate()
        {
            if(!Source.IsBattleActive){Group.alpha=0;Edge.enabled=false;return;}
            int count=Source.Count(Effect); int warnings=Source.Count(Effect,true);
            if(count==0 && warnings==0) {Group.alpha=Mathf.MoveTowards(Group.alpha,0,Time.timeScale>0?Time.unscaledDeltaTime/FadeSeconds:0);return;}
            if(Time.timeScale<=0)return;
            Source.GetVisualTiming(Effect,out float warningProgress,out float remaining);
            Edge.enabled=count>0;
            float elapsed=Source.ElapsedSeconds;
            float wave=Mathf.Sin(elapsed*2*Mathf.PI/BreathSeconds);
            // 每个状态保持各自色带；多个状态同时可见时减弱覆盖，而不是开新白色合成层。
            int types=(Source.Count(SceneBattleEffect.DoubleSpeed)>0?1:0)+(Source.Count(SceneBattleEffect.HalfSpeed)>0?1:0)+(Source.Count(SceneBattleEffect.InvertedMovement)>0?1:0);
            float target=(count>0?ActiveOpacity:WarningOpacity)*(1-BreathAmount*(wave+1)*.5f)/Mathf.Sqrt(Mathf.Max(1,types));
            if(count>0&&remaining<=EndingWarningSeconds)
                target*=Mathf.Lerp(EndingBlinkMinimumAlpha,1,(Mathf.Sin(remaining*2*Mathf.PI*EndingBlinkCyclesPerSecond)+1)*.5f);
            Group.alpha=target;
            Edge.Scroll=Effect==SceneBattleEffect.InvertedMovement ? Mathf.Sin(elapsed*ScrollSpeed) : elapsed*ScrollSpeed;
            Edge.SetVerticesDirty();
            for(int i=0;i<Corners.Length;i++)
            {
                // 装配数组是左上、右上、左下、右下；点亮顺序为左上→右上→右下→左下。
                int rank=i==2?3:i==3?2:i;
                Color color=_cornerColors[i];
                color.a=count>0?color.a:(warningProgress*4>=rank?1:0);
                _cornerImages[i].color=color;
                Corners[i].localScale=_cornerScales[i]*(1+wave*CornerMotion);
            }
        }

        private void OnDisable(){if(Group!=null)Group.alpha=0;}
    }
}
