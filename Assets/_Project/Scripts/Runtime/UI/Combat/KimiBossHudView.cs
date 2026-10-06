using DeepSleep.Runtime.Combat.Encounters.Kimi;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>血量只读视图；填充图片在固定 RectMask2D 内，边框独立且不参与裁切。</summary>
    public sealed class KimiBossHudView : MonoBehaviour
    {
        public KimiBoss2D Boss;
        public CanvasGroup Visibility;
        public Image HealthFill;
        public Text HealthText;
        public GameObject ShieldPanel;
        public Text ShieldText;
        public string DisplayName;
        public KimiHitCurtain2D Curtain;
        private int _lastShield=-1;
        private float _lastHealth = -1;
        private bool _lastPhase;

        private void Awake()
        {
            if (Visibility == null || HealthFill == null || HealthText == null || ShieldPanel == null || ShieldText == null)
            { Debug.LogError("[KimiHUD] uGUI 引用缺失。", this); enabled = false; return; }
            Visibility.blocksRaycasts = Visibility.interactable = false;
            Visibility.alpha = 0;
            ShieldPanel.SetActive(false);
        }

        public void Bind(KimiBoss2D boss, KimiHitCurtain2D curtain = null)
        {
            Boss = boss;
            Curtain = curtain; _lastShield=-1;
            _lastHealth = -1;
            RenderNow();
        }

        private void LateUpdate() => RenderNow();

        public void RenderNow()
        {
            bool shown = Boss != null && Boss.IsShown;
            Visibility.alpha = shown ? 1 : 0;
            int shield=shown && Curtain!=null?Curtain.Remaining:0;
            if(shield!=_lastShield) { RenderShield(shield,Curtain!=null?Curtain.Maximum:0); _lastShield=shield; }
            if (!shown) return;
            HealthFill.fillAmount = Mathf.Clamp01(Boss.CurrentHealth / Boss.MaximumHealth);
            if (_lastHealth == Boss.CurrentHealth && _lastPhase == Boss.PhaseTwo) return;
            _lastHealth = Boss.CurrentHealth;
            _lastPhase = Boss.PhaseTwo;
            HealthText.text = DisplayName + (_lastPhase ? " · II  " : " · I  ") +
                Mathf.CeilToInt(_lastHealth) + " / " + Mathf.CeilToInt(Boss.MaximumHealth);
        }

        /// <summary>由后续次数盾控制器传入剩余次数；这里不保存第二套盾生命。</summary>
        public void RenderShield(int remaining, int maximum)
        {
            ShieldPanel.SetActive(remaining > 0 && maximum > 0);
            if (remaining > 0 && maximum > 0) ShieldText.text = remaining + " / " + maximum;
        }
    }
}
