using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>只做固定世界位置的淡出；不持续伤害、不跟随玩家。</summary>
    public sealed class MeleeWaveView2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        private Vector2 _position;
        private float _angle, _width, _duration, _elapsed;
        private Vector2 _mirror;
        public bool IsPlaying { get; private set; }

        public void Play(HarnessMeleeAttackConfig attack, Vector2 origin, float aim, float duration)
        {
            _renderer.sprite = attack.WaveSprite;
            _position = MeleeSwordGeometry2D.WavePosition(attack, origin, aim);
            _angle = MeleeSwordGeometry2D.WaveAngle(attack, aim);
            _mirror = MeleeSwordGeometry2D.WaveSigns(attack, aim);
            _width = attack.WaveWidth;
            _duration = duration;
            _elapsed = 0;
            IsPlaying = true;
            Render();
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration) { Clear(); return; }
            Render();
        }

        private void Render()
        {
            _renderer.enabled = true;
            float t = Mathf.Clamp01(_elapsed / _duration);
            _renderer.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, t));
            SetWorldSprite(_renderer, _position, _angle, _width);
            Vector3 scale = _renderer.transform.localScale;
            scale.x *= _mirror.x;
            scale.y *= _mirror.y;
            _renderer.transform.localScale = scale;
        }

        public void Clear()
        {
            IsPlaying = false;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnDisable() => Clear();

        public static void SetWorldSprite(SpriteRenderer renderer, Vector2 position, float angle, float width)
        {
            renderer.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
            float scale = width / Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
            Vector3 parent = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
            renderer.transform.localScale = new Vector3(scale / parent.x, scale / parent.y, 1);
        }
    }
}
