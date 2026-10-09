using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Audio;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    /// <summary>把场景内稀疏玩法事件转交给常驻成就服务。</summary>
    public sealed class GameplayAchievementReporter : MonoBehaviour
    {
        [System.Serializable]
        public sealed class FactBinding
        {
            public NetworkMessageCatalog.CombatPresentationKind Fact;
            public string TriggerId;
        }
        [SerializeField] private CombatAudioPresenter _facts;
        [SerializeField] private FactBinding[] _bindings;
        private AchievementService _service;

        private void OnEnable()
        {
            if (_facts == null || _facts.Chapter == null || _bindings == null || _bindings.Length == 0)
            { Debug.LogError("[Achievements] 未配置玩法事实源及成就映射。", this); enabled = false; return; }
            _service = GameAppRoot.Instance.Achievements;
            _facts.FactPresented += OnFact;
        }

        private void OnDisable()
        {
            if (_facts != null) _facts.FactPresented -= OnFact;
        }

        private void OnFact(NetworkMessageCatalog.CombatPresentationKind fact)
        {
            if (_facts.Chapter.IsChallenge) return;
            foreach (var binding in _bindings)
                if (binding.Fact == fact)
                    _service.Report(binding.TriggerId);
        }
    }
}
