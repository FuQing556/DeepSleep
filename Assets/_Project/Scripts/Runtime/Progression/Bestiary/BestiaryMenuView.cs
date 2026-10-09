using DeepSleep.Runtime.AppFlow;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Bestiary
{
    /// <summary>图鉴按条目切换详情；挑战只向既有场景路由提交当前条目的入口意图。</summary>
    public sealed class BestiaryMenuView : MonoBehaviour
    {
        public BestiaryEntryDefinition Entry;
        public BestiaryEntryDefinition[] Entries;
        public Button[] EntryButtons;
        public Text PortraitCaption;
        private UnityEngine.Events.UnityAction[] _entryActions;
        public Image Portrait;
        public Text NameLabel, SubtitleLabel, DescriptionLabel, SkillsLabel, ChallengeLabel;
        public Button ChallengeButton;

        private void OnEnable()
        {
            if (Entry == null || !Entry.TryValidate(out _) || Portrait == null || NameLabel == null ||
                SubtitleLabel == null || DescriptionLabel == null || SkillsLabel == null ||
                ChallengeLabel == null || ChallengeButton == null)
            { Debug.LogError("[Bestiary] 图鉴引用缺失。", this); enabled = false; return; }
            if (Entries != null && Entries.Length > 0)
            {
                if (EntryButtons == null || EntryButtons.Length != Entries.Length)
                { Debug.LogError("[Bestiary] 条目按钮数量不匹配。", this); enabled = false; return; }
                _entryActions = new UnityEngine.Events.UnityAction[Entries.Length];
                for (int i = 0; i < Entries.Length; i++)
                {
                    int index = i;
                    _entryActions[i] = () => SelectEntry(index);
                    EntryButtons[i].onClick.AddListener(_entryActions[i]);
                }
            }
            RenderEntry();
            ChallengeButton.onClick.AddListener(Challenge);
        }

        public void SelectEntry(int index)
        {
            if (index < 0 || index >= Entries.Length) return;
            if (!Entries[index].TryValidate(out string reason))
            { Debug.LogError("[Bestiary] " + reason, this); return; }
            Entry = Entries[index]; RenderEntry();
        }

        private void RenderEntry()
        {
            Portrait.sprite = Entry.Portrait; Portrait.preserveAspect = true;
            if (PortraitCaption != null) PortraitCaption.text = Entry.DisplayName;
            NameLabel.text = Entry.DisplayName; SubtitleLabel.text = Entry.Subtitle;
            DescriptionLabel.text = Entry.Description; SkillsLabel.text = Entry.SkillNotes + "\n\n" + Entry.ChallengeSummary;
            ChallengeLabel.text = "快速挑战";
            if (EntryButtons != null && Entries != null)
                for (int i = 0; i < EntryButtons.Length; i++) EntryButtons[i].interactable = Entries[i] != Entry;
        }

        private void OnDisable()
        {
            if (ChallengeButton != null) ChallengeButton.onClick.RemoveListener(Challenge);
            if (_entryActions != null)
                for (int i = 0; i < _entryActions.Length; i++) EntryButtons[i].onClick.RemoveListener(_entryActions[i]);
        }

        private void Challenge() => GameAppRoot.Instance.SceneRouter.StartChallenge(Entry);
    }
}
