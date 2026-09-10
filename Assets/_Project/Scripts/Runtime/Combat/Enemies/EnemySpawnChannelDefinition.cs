using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>
    /// 连接场景刷怪器与关卡段落配置的稳定身份。
    /// 新增敌人时创建新频道资产，不需要扩充代码枚举。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CFG_EN_SpawnChannel_",
        menuName = "DeepSleep/配置/敌人/刷怪频道")]
    public sealed class EnemySpawnChannelDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
            ? name
            : _displayName;
    }
}
