using DeepSleep.Runtime.AppFlow;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Bestiary
{
    /// <summary>图鉴首版单条目详情页；挑战只向既有场景路由提交入口意图。</summary>
    public sealed class BestiaryMenuView : MonoBehaviour
    {
        public BestiaryEntryDefinition Entry;
        public Image Portrait;
        public Text NameLabel, SubtitleLabel, DescriptionLabel, SkillsLabel, ChallengeLabel;
        public Button ChallengeButton;

        private void OnEnable()
        {
            if (Entry == null || !Entry.TryValidate(out _) || Portrait == null || NameLabel == null ||
                SubtitleLabel == null || DescriptionLabel == null || SkillsLabel == null ||
                ChallengeLabel == null || ChallengeButton == null)
            { Debug.LogError("[Bestiary] 图鉴引用缺失。", this); enabled = false; return; }
            Portrait.sprite = Entry.Portrait; Portrait.preserveAspect = true;
            NameLabel.text = Entry.DisplayName; SubtitleLabel.text = Entry.Subtitle;
            DescriptionLabel.text = Entry.Description; SkillsLabel.text = Entry.SkillNotes;
            ChallengeLabel.text = $"快速挑战 · 领取 {Entry.StartingTokensPerRole} Token";
            ChallengeButton.onClick.AddListener(Challenge);
        }

        private void OnDisable()
        { if (ChallengeButton != null) ChallengeButton.onClick.RemoveListener(Challenge); }

        private void Challenge() => GameAppRoot.Instance.SceneRouter.StartChallenge(Entry);
    }
}
