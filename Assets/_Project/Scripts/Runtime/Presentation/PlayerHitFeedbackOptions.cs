using UnityEngine;
using UnityEngine.UI;
using DeepSleep.Runtime.World.Cameras;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>本机偏好；不影响联网权威、玩法数值或其他机器。</summary>
    public sealed class PlayerHitFeedbackOptions : MonoBehaviour
    {
        private const string ShakeKey="DeepSleep.HitFeedback.Shake";
        private const string FlashKey="DeepSleep.HitFeedback.ReduceFlash";
        public static float ShakeStrength => Mathf.Clamp01(PlayerPrefs.GetFloat(ShakeKey,.65f));
        public static bool ReduceFlash => PlayerPrefs.GetInt(FlashKey,0)!=0;
        public Button ShakeButton, FlashButton;
        public Text ShakeLabel, FlashLabel;
        public CameraHorizontalLookAhead2D CameraFeedback;
        private void OnEnable()
        {
            ShakeButton.onClick.AddListener(CycleShake);FlashButton.onClick.AddListener(ToggleFlash);Refresh();
        }
        private void OnDisable()
        { ShakeButton.onClick.RemoveListener(CycleShake);FlashButton.onClick.RemoveListener(ToggleFlash); }
        private void CycleShake()
        {
            float current=ShakeStrength;
            PlayerPrefs.SetFloat(ShakeKey,current<=0f?.65f:current<.9f?1f:0f);PlayerPrefs.Save();
            CameraFeedback.ShakeStrength=ShakeStrength;
            if(ShakeStrength<=0)CameraFeedback.ResetHit();
            Refresh();
        }
        private void ToggleFlash()
        { PlayerPrefs.SetInt(FlashKey,ReduceFlash?0:1);PlayerPrefs.Save();Refresh(); }
        private void Refresh()
        {
            ShakeLabel.text="受击震屏："+(ShakeStrength<=0?"关闭":ShakeStrength<.9f?"轻微":"标准");
            FlashLabel.text="受击闪光："+(ReduceFlash?"减弱":"标准");
        }
    }
}
