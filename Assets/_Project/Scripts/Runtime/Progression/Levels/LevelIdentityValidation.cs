using System;
using System.Text;
using DeepSleep.Runtime.Progression.Meta;

namespace DeepSleep.Runtime.Progression.Levels
{
    /// <summary>关卡身份规则；只检查显式参数，不加载场景、不覆盖错绑的定义。</summary>
    public static class LevelIdentityValidation
    {
        // 身份需要进入有限容量握手帧；这是线格式上限，不是可调玩法数值。
        public const int MaximumLevelIdUtf8Bytes = 128;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>拒绝不稳定的空白身份及无法按标准 UTF-8 唯一传输的字符串。</summary>
        public static bool TryValidateLevelId(string levelId, out string reason)
        {
            if (string.IsNullOrWhiteSpace(levelId) || levelId != levelId.Trim())
            {
                reason = "LevelId 不能为空或带首尾空白。";
                return false;
            }
            try
            {
                if (StrictUtf8.GetByteCount(levelId) > MaximumLevelIdUtf8Bytes)
                {
                    reason = $"LevelId 的 UTF-8 编码不能超过 {MaximumLevelIdUtf8Bytes} 字节。";
                    return false;
                }
            }
            catch (EncoderFallbackException)
            {
                reason = "LevelId 包含不能编码为标准 UTF-8 的字符。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static bool TryValidate(MetaLevelDefinition level, string sceneName,
            MetaLevelDefinition selectedLevel, bool requireLaunchIntent, out string reason)
        {
            if (!TryValidateContext(level, sceneName, selectedLevel, requireLaunchIntent, out reason)) return false;
            return level.TryValidate(out reason);
        }

        /// <summary>只比较场景和启动归属；定义本身由完整装配校验验证一次。</summary>
        public static bool TryValidateContext(MetaLevelDefinition level, string sceneName,
            MetaLevelDefinition selectedLevel, bool requireLaunchIntent, out string reason)
        {
            if (level == null)
            {
                reason = "场景缺少明确的关卡定义。";
                return false;
            }
            if (!string.Equals(level.SceneName, sceneName, StringComparison.Ordinal))
            {
                reason = $"关卡 {level.LevelId} 声明场景 {level.SceneName}，实际为 {sceneName}。";
                return false;
            }
            if (selectedLevel != null && selectedLevel != level)
            {
                reason = $"启动关卡 {selectedLevel.LevelId} 与场景绑定 {level.LevelId} 不是同一关卡定义。";
                return false;
            }
            if (requireLaunchIntent && selectedLevel == null)
            {
                reason = $"关卡 {level.LevelId} 缺少启动意图，请从主菜单进入。";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
