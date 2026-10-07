using DeepSleep.Runtime.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Kimi
{
    /// <summary>纯表现：复用既有命中序号显示空心月光罩；退场影像不保留任何伤害或碰撞。</summary>
    [DefaultExecutionOrder(310)]
    public sealed class KimiBossPresentation2D : MonoBehaviour
    {
        public KimiBoss2D Boss;
        public SpriteHitFlash2D HitFlash;
        public SpriteRenderer MoonShield, DepartingBody, DepartingCloud;
        public float ShieldPeakAlpha, DepartureSeconds;
        [Range(0, 1)] public float ShieldIdleAlpha;
        public float EntryFadeSeconds;
        private float _entryAge;
        private bool _wasShown;
        private float _departureAge;
        private bool _departing;

        public bool TryValidateConfiguration(out string reason)
        {
            if (Boss == null || HitFlash == null || MoonShield == null || MoonShield.sprite == null ||
                DepartingBody == null || DepartingCloud == null || !(Boss.HitCollider is CircleCollider2D) ||
                !float.IsFinite(DepartureSeconds) || DepartureSeconds <= 0 ||
                !float.IsFinite(EntryFadeSeconds) || EntryFadeSeconds <= 0 ||
                !float.IsFinite(ShieldPeakAlpha) || ShieldPeakAlpha <= 0 || ShieldPeakAlpha > 1)
            { reason = "Kimi月光罩/退场渲染器与尺寸、透明度、时间需显式配置。"; return false; }
            reason = string.Empty; return true;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[KimiPresentation] " + reason, this); enabled = false; return; }
            ResetPresentation();
            Boss.Defeated += BeginDeparture;
        }

        public void BeginDeparture()
        {
            if (_departing) return;
            Copy(Boss.Body, DepartingBody);
            DepartingBody.sprite = Boss.Config.Poses[(int)KimiPose.Bow];
            Copy(Boss.Cloud, DepartingCloud);
            _departing = true; _departureAge = 0;
            // 正式本体立刻退出受击/感知；预装配的两张影像只负责可见退场。
            Boss.Body.enabled = Boss.Cloud.enabled = false;
            MoonShield.enabled = false;
        }

        private static void Copy(SpriteRenderer source, SpriteRenderer target)
        {
            target.sprite = source.sprite; target.color = source.color;
            target.flipX = source.flipX; target.flipY = source.flipY;
            target.sortingLayerID = source.sortingLayerID; target.sortingOrder = source.sortingOrder;
            target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            target.transform.localScale = source.transform.localScale;
            target.enabled = true;
        }

        private void LateUpdate()
        {
            if (Boss.IsAlive)
            {
                if (!_wasShown) _entryAge = 0;
                else _entryAge += Time.deltaTime;
                float entryAlpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_entryAge / EntryFadeSeconds));
                SetAlpha(Boss.Body, entryAlpha); SetAlpha(Boss.Cloud, entryAlpha);
            }
            _wasShown = Boss.IsAlive;
            if (_departing)
            {
                Boss.Body.enabled = Boss.Cloud.enabled = false;
                // 通关原有结算暂停 timeScale=0；只让这段纯退场影像继续，不推进战斗。
                _departureAge += Time.unscaledDeltaTime;
                float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(_departureAge / DepartureSeconds));
                SetAlpha(DepartingBody, alpha); SetAlpha(DepartingCloud, alpha);
                if (alpha <= 0) { _departing = false; DepartingBody.enabled = DepartingCloud.enabled = false; }
            }
            MoonShield.enabled = Boss.IsAlive;
            if (!MoonShield.enabled) return;
            // 客机受击体禁用时bounds为空；使用圆的几何参数，不依赖物理启用状态。
            var sphere = (CircleCollider2D)Boss.HitCollider;
            MoonShield.transform.position = sphere.transform.TransformPoint(sphere.offset);
            float diameter = sphere.radius * 2 * Mathf.Abs(sphere.transform.lossyScale.x);
            MoonShield.transform.localScale = Vector3.one * (diameter / MoonShield.sprite.bounds.size.x);
            // 每次实际扣血短暂变暗，再回到常态亮度；既有联网短闪序号直接复用。
            float flash = HitFlash.IsPlaying ? 1 - Mathf.SmoothStep(0, 1, HitFlash.NormalizedAge) : 0;
            SetAlpha(MoonShield, Mathf.Lerp(ShieldIdleAlpha, ShieldPeakAlpha, flash));
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        { var color = renderer.color; color.a = alpha; renderer.color = color; }

        public void ResetPresentation()
        {
            _departing = false; _departureAge = 0;
            _wasShown = false; _entryAge = 0;
            if (Boss != null && Boss.Body != null && Boss.Cloud != null)
            { SetAlpha(Boss.Body, 1); SetAlpha(Boss.Cloud, 1); }
            if (MoonShield != null) MoonShield.enabled = false;
            if (DepartingBody != null) DepartingBody.enabled = false;
            if (DepartingCloud != null) DepartingCloud.enabled = false;
        }

        private void OnDisable()
        { if (Boss != null) Boss.Defeated -= BeginDeparture; ResetPresentation(); }
    }
}
