using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Progression.Meta
{
    public sealed class AchievementCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _name;
        [SerializeField] private Text _description;
        [SerializeField] private Text _progress;

        public void Render(
            AchievementDefinition definition,
            bool unlocked,
            int progress)
        {
            bool concealed = definition.Hidden && !unlocked;
            _name.text = concealed ? "???" : definition.DisplayName;
            _description.text = concealed
                ? "隐藏成就"
                : definition.Description;
            _progress.text = concealed
                ? "未解锁"
                : unlocked
                    ? "已解锁"
                    : Mathf.Min(progress, definition.TargetCount) + "/" +
                      definition.TargetCount;
            _icon.sprite = definition.Icon;
            _icon.enabled = definition.Icon != null && !concealed;
        }
    }
}
