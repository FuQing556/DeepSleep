using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Meta
{
    /// <summary>只覆盖常态/倒地角色图；不携带玩法参数或技能素材。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/Presentation/Player Skin")]
    public sealed class PlayerSkinDefinition : ScriptableObject
    {
        public ushort NetworkId;
        public PlayerRole Role;
        public Sprite DefaultAlive, DefaultDowned;
        public Sprite Preview;
        public Sprite[] AliveVariants, DownedVariants;
        public bool TryValidate(out string reason)
        {
            if (NetworkId == 0 || DefaultAlive == null || DefaultDowned == null || Preview == null ||
                AliveVariants == null || AliveVariants.Length == 0 || DownedVariants == null || DownedVariants.Length == 0)
            { reason = "服装缺少稳定ID、原版姿态、展示图或随机姿态。"; return false; }
            foreach (var sprite in AliveVariants) if (sprite == null) { reason = "服装常态图为空。"; return false; }
            foreach (var sprite in DownedVariants) if (sprite == null) { reason = "服装倒地图为空。"; return false; }
            reason = string.Empty; return true;
        }
        public bool Contains(Sprite sprite)
        {
            if (sprite == DefaultAlive || sprite == Preview) return true;
            foreach (var pose in AliveVariants) if (pose == sprite) return true;
            return false;
        }
    }
}
