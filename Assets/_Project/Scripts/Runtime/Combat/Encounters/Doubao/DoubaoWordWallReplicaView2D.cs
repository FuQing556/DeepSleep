using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Doubao
{
    /// <summary>豆包词墙的无碰撞客户端镜像，只负责插值显示。</summary>
    public sealed class DoubaoWordWallReplicaView2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TextMesh _label;
        [SerializeField, Min(1)] private int _charactersPerLine = 3;
        private string _phrase;

        private Vector3 _targetPosition;
        private float _interpolationSpeed = 30f;
        private bool _initialized;

        public uint ReplicationId { get; private set; }

        private void Awake()
        {
            if (_renderer == null || _label == null)
            {
                Debug.LogError($"[{nameof(DoubaoWordWallReplicaView2D)}] 必须显式配置渲染器与文字。", this);
                enabled = false;
            }
        }

        public void Apply(uint id, Vector2 position, Vector2 size, string phrase, float interpolationSpeed)
        {
            ReplicationId = id;
            _targetPosition = position;
            _interpolationSpeed = Mathf.Max(1f, interpolationSpeed);
            _renderer.transform.localScale = Vector3.one * (size.x / _renderer.sprite.bounds.size.x);
            if (_phrase != phrase)
            {
                _phrase = phrase;
                _label.text = DoubaoWordWallBlock2D.FormatPhrase(phrase, _charactersPerLine);
            }
            if (!_initialized) transform.position = position;
            _initialized = true;
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            ReplicationId = 0;
            _initialized = false;
            _label.text = string.Empty;
            _phrase = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            float t = 1f - Mathf.Exp(-_interpolationSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
        }
    }
}
