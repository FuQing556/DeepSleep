using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Meta
{
    public sealed class AchievementToastView : MonoBehaviour
    {
        [SerializeField] private AchievementService _service;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _card;
        [SerializeField] private Text _title;
        [SerializeField] private Text _description;
        [SerializeField, Min(0.1f)] private float _visibleSeconds = 2.6f;

        private readonly Queue<AchievementDefinition> _queue = new();
        private Coroutine _routine;
        private Vector2 _basePosition;

        private void Awake()
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _basePosition = _card.anchoredPosition;
        }

        private void OnEnable() => _service.Unlocked += Enqueue;
        private void OnDisable()
        {
            if (_service != null) _service.Unlocked -= Enqueue;
        }

        private void Enqueue(AchievementDefinition definition)
        {
            _queue.Enqueue(definition);
            if (_routine == null) _routine = StartCoroutine(ShowQueue());
        }

        private IEnumerator ShowQueue()
        {
            while (_queue.Count > 0)
            {
                AchievementDefinition definition = _queue.Dequeue();
                _title.text = "成就解锁 · " + definition.DisplayName;
                _description.text = definition.Description;
                yield return Fade(0f, 1f, -24f, 0f, 0.18f);
                yield return new WaitForSecondsRealtime(_visibleSeconds);
                yield return Fade(1f, 0f, 0f, 18f, 0.22f);
            }
            _routine = null;
        }

        private IEnumerator Fade(
            float fromAlpha,
            float toAlpha,
            float fromX,
            float toX,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                _group.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                Vector2 position = _basePosition;
                position.x += Mathf.Lerp(fromX, toX, t);
                _card.anchoredPosition = position;
                yield return null;
            }
            _group.alpha = toAlpha;
            Vector2 finalPosition = _basePosition;
            finalPosition.x += toX;
            _card.anchoredPosition = finalPosition;
        }
    }
}
