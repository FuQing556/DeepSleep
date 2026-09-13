using System.Collections.Generic;
using DeepSleep.Runtime.AppFlow;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Meta
{
    public sealed class AchievementMenuController : MonoBehaviour
    {
        [SerializeField] private AchievementCardView _cardPrefab;
        [SerializeField] private Transform _content;
        [SerializeField] private Text _summary;

        private readonly List<AchievementCardView> _cards = new();
        private AchievementService _service;

        private void Awake()
        {
            _service = GameAppRoot.Instance.Achievements;
            AchievementDefinition[] definitions = _service.Definitions;
            for (int index = 0; index < definitions.Length; index++)
                _cards.Add(Instantiate(_cardPrefab, _content));
        }

        private void OnEnable()
        {
            if (_service != null) _service.Unlocked += OnUnlocked;
            Render();
        }

        private void OnDisable()
        {
            if (_service != null) _service.Unlocked -= OnUnlocked;
        }

        private void OnUnlocked(AchievementDefinition definition) => Render();

        private void Render()
        {
            if (_service == null) return;
            AchievementDefinition[] definitions = _service.Definitions;
            int unlockedCount = 0;
            for (int index = 0; index < definitions.Length; index++)
            {
                bool unlocked = _service.IsUnlocked(definitions[index]);
                if (unlocked) unlockedCount++;
                _cards[index].Render(
                    definitions[index],
                    unlocked,
                    _service.GetProgress(definitions[index]));
            }
            _summary.text = $"已解锁 {unlockedCount}/{definitions.Length}";
        }
    }
}
