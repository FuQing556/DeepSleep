using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard
{
    /// <summary>显示护航法阵；外沿与饭碗中心轨迹共享同一个半径。</summary>
    public sealed class DeepSeekRiceGuardCircleView2D : MonoBehaviour
    {
        [SerializeField] private DeepSeekRiceGuardController _controller;
        [SerializeField] private DeepSeekRiceGuardOrbitView2D _orbitView;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _rotationDegreesPerSecond = -18f;
        [SerializeField, Min(0.01f)] private float _fadeInSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _activeMinimumAlpha = 0.78f;
        [SerializeField, Min(0f)] private float _breathsPerSecond = 0.65f;
        [SerializeField, Min(0f)] private float _warningPulsesPerSecond = 4f;

        private bool _shouldShow;
        private float _alpha;

        private void Awake()
        {
            if (_controller == null || _orbitView == null || _renderer == null || _renderer.sprite == null)
            {
                Debug.LogError($"[{nameof(DeepSeekRiceGuardCircleView2D)}] 控制器、轨道视图、渲染器或法阵贴图未配置。", this);
                enabled = false;
                return;
            }
            MatchOuterEdgeToBowlCenters();
            SetAlpha(0f);
            _renderer.enabled = false;
        }

        private void OnEnable()
        {
            if (_controller == null) return;
            _controller.Activated += OnActivated;
            _controller.Ended += OnEnded;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Activated -= OnActivated;
                _controller.Ended -= OnEnded;
            }
            _shouldShow = false;
            _alpha = 0f;
            if (_renderer != null) _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f) return;
            transform.Rotate(0f, 0f, _rotationDegreesPerSecond * Time.deltaTime, Space.Self);
            float target = _shouldShow ? GetActiveAlpha() : 0f;
            float duration = _shouldShow ? _fadeInSeconds : _fadeOutSeconds;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime / duration);
            SetAlpha(_alpha);
            _renderer.enabled = _alpha > 0f;
        }

        private void OnActivated()
        {
            MatchOuterEdgeToBowlCenters();
            _shouldShow = true;
            _renderer.enabled = true;
        }

        private void OnEnded() => _shouldShow = false;

        private void MatchOuterEdgeToBowlCenters()
        {
            float spriteDiameter = _renderer.sprite.bounds.size.x;
            if (spriteDiameter <= Mathf.Epsilon) return;
            transform.localScale = Vector3.one * (_orbitView.OrbitRadius * 2f / spriteDiameter);
        }

        private float GetActiveAlpha()
        {
            float frequency = _controller.IsWarning ? _warningPulsesPerSecond : _breathsPerSecond;
            if (frequency <= 0f) return 1f;
            float wave = (Mathf.Sin(Time.time * Mathf.PI * 2f * frequency) + 1f) * 0.5f;
            return Mathf.Lerp(_activeMinimumAlpha, 1f, wave);
        }

        private void SetAlpha(float value)
        {
            Color color = _renderer.color;
            color.a = value;
            _renderer.color = color;
        }
    }
}
