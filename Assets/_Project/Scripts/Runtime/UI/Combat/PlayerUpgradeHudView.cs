using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>只在构筑变化时更新预装配的已获强化栏；共享主机同步后的运行状态，不另设玩法或计时器。</summary>
    public sealed class PlayerUpgradeHudView : MonoBehaviour
    {
        public PlayerUpgradeRuntimeState State;
        public PlayerRole Role;
        public Image[] Icons;
        public Text[] Ranks;
        private bool _ready;

        private void OnEnable()
        {
            if (!ValidateBindings())
            {
                Debug.LogError("[PlayerUpgradeHudView] State、角色对应的图标/等级槽或目录 Icon 未完整装配：" + name, this);
                enabled = false;
                return;
            }
            _ready = true;
            State.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_ready) State.Changed -= Refresh;
            _ready = false;
        }

        /// <summary>按目录顺序紧凑显示已有强化；同一个图标的角标表达等级，不复制等级素材。</summary>
        public void Refresh()
        {
            if (!_ready) return;
            int slot = 0;
            var definitions = State.Catalog.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                UpgradeDefinition definition = definitions[i];
                if (!definition.Supports(Role)) continue;
                int rank = State.GetRank(Role, definition.Id);
                if (rank <= 0) continue;
                Icons[slot].sprite = definition.Icon;
                Ranks[slot].text = rank.ToString();
                Icons[slot].gameObject.SetActive(true);
                slot++;
            }
            for (; slot < Icons.Length; slot++) Icons[slot].gameObject.SetActive(false);
        }

        private bool ValidateBindings()
        {
            if (State == null || State.Catalog == null || State.Catalog.Definitions == null ||
                (Role != PlayerRole.DeepSeek && Role != PlayerRole.Harness) ||
                Icons == null || Ranks == null || Icons.Length != Ranks.Length) return false;
            int count = 0;
            foreach (var definition in State.Catalog.Definitions)
            {
                if (definition == null) return false;
                if (!definition.Supports(Role)) continue;
                if (definition.Icon == null) return false;
                count++;
            }
            if (Icons.Length < count) return false;
            for (int i = 0; i < Icons.Length; i++)
                if (Icons[i] == null || Ranks[i] == null) return false;
            return true;
        }
    }
}
