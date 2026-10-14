using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    [CreateAssetMenu(menuName = "DeepSleep/配置/关卡/难度", fileName = "CFG_RunDifficulty")]
    public sealed class RunDifficultyConfig : ScriptableObject
    {
        [Range(.01f, 1f)] public float EasyDurabilityMultiplier;
    }
}
