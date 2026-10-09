using DeepSleep.Runtime.Combat.Encounters;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>通用BossDamageBody只读血条，沿用现有生成素材和固定裁切布局。</summary>
    [DefaultExecutionOrder(320)]
    public sealed class BossHealthHudView : MonoBehaviour
    {
        public BossDamageBody2D Boss;
        public BossBarrierFeedback2D Barrier;
        public CanvasGroup Visibility;
        public Image HealthFill;
        public Text HealthText;
        public string DisplayName;
        private float _lastHealth = -1, _lastMaximum = -1;
        private bool _lastPhase;

        private void Awake()
        {
            if (Boss == null || Visibility == null || HealthFill == null || HealthText == null)
            { Debug.LogError("[BossHUD] 显式uGUI引用缺失。", this); enabled = false; return; }
            Visibility.blocksRaycasts = Visibility.interactable = false;
            RenderNow();
        }
        private void LateUpdate() => RenderNow();
        public void RenderNow()
        {
            Visibility.alpha = Boss.IsAlive && Barrier != null ? Barrier.EntranceAlpha : 0;
            if (!Boss.IsAlive) return;
            HealthFill.fillAmount = Mathf.Clamp01(Boss.CurrentHealth / Boss.MaximumHealth);
            if (_lastHealth == Boss.CurrentHealth && _lastMaximum == Boss.MaximumHealth && _lastPhase == Boss.PhaseTwo) return;
            _lastHealth = Boss.CurrentHealth; _lastMaximum = Boss.MaximumHealth; _lastPhase = Boss.PhaseTwo;
            HealthText.text = DisplayName + (_lastPhase ? " · II  " : " · I  ") +
                Mathf.CeilToInt(_lastHealth) + " / " + Mathf.CeilToInt(_lastMaximum);
        }
    }
}
