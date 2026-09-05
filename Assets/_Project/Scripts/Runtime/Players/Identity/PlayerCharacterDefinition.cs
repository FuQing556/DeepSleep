using UnityEngine;

namespace DeepSleep.Runtime.Players.Identity
{
    /// <summary>
    /// 保存角色跨场景、存档和联机都可复用的稳定身份；不保存本局状态。
    /// </summary>
    [CreateAssetMenu(
        fileName = "DEF_PlayerCharacter_",
        menuName = "DeepSleep/配置/玩家角色定义")]
    public sealed class PlayerCharacterDefinition : ScriptableObject
    {
        [SerializeField] private string _characterId;
        [SerializeField] private int _dataVersion;
        [SerializeField] private PlayerRole _role;

        public string CharacterId => _characterId;

        public int DataVersion => _dataVersion;

        public PlayerRole Role => _role;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_characterId))
            {
                reason = "角色 ID 不能为空。";
                return false;
            }

            if (_dataVersion <= 0)
            {
                reason = "数据版本必须大于 0。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
