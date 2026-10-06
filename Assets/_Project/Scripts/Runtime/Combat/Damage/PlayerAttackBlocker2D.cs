using UnityEngine;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>不带生命的实体挡板；仅截断玩家攻击，不作为可锁定或可受伤对象。</summary>
    public sealed class PlayerAttackBlocker2D : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        public Color RestColor = new Color(.72f, .85f, .72f, 1);
        public float FlashSeconds = .12f;
        private float _flash;
        public event System.Action<Vector2> Blocked;
        public void NotifyBlocked() { _flash = FlashSeconds; Blocked?.Invoke(transform.position); }
        private void OnEnable() { _flash = 0; if (Renderer != null) Renderer.color = RestColor; }
        private void LateUpdate()
        {
            _flash = Mathf.Max(0, _flash - Time.deltaTime);
            if (Renderer != null) Renderer.color = Color.Lerp(RestColor, Color.white, _flash / FlashSeconds);
        }
    }
}
