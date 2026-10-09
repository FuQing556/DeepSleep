using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeepSleep.Runtime.Players.Identity;

namespace DeepSleep.Runtime.Progression.Meta
{
    [Serializable]
    internal sealed class OwnedProductRecord
    {
        public string productId;
        public int count;
    }

    [Serializable]
    internal sealed class LocalPlayerProfileData
    {
        public int version = LocalPlayerProfileStore.CurrentVersion;
        public int whaleVoucherBalance;
        public List<OwnedProductRecord> ownedProducts = new();
        public List<string> clearedLevels = new();
        public List<string> completedChallenges = new();
        public List<string> unlockedAchievements = new();
        public List<AchievementProgressRecord> achievementProgress = new();
        public string deepSeekHeadwear;
        public string harnessHeadwear;
        public string deepSeekBackwear;
        public string harnessBackwear;
        public string deepSeekSkin;
        public string harnessSkin;
    }

    [Serializable]
    internal sealed class AchievementProgressRecord
    {
        public string triggerId;
        public int count;
    }

    /// <summary>
    /// 局外档案的唯一写入口。使用稳定字符串 ID，不把 ScriptableObject
    /// 或场景引用写进 JSON。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class LocalPlayerProfileStore : MonoBehaviour
    {
        public const int CurrentVersion = 6;
        private const string FileName = "profile.json";
        private const string BackupFileName = "profile.backup.json";

        private LocalPlayerProfileData _data;
#if UNITY_EDITOR
        // 编辑器验证写入独立临时目录，绝不对用户存档做买入/回滚试验。
        [SerializeField, HideInInspector] internal string TestSaveDirectory;
#endif

        public event Action Changed;
        public int WhaleVoucherBalance => _data?.whaleVoucherBalance ?? 0;
        public string SavePath => Path.Combine(SaveDirectory, FileName);
        private string SaveDirectory
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(TestSaveDirectory)) return TestSaveDirectory;
#endif
                return Application.persistentDataPath;
            }
        }

        private void Awake()
        {
            _data = LoadOrCreate();
        }

        public int GetOwnedCount(string productId)
        {
            OwnedProductRecord record = FindProduct(productId);
            return record?.count ?? 0;
        }

        public string GetHeadwear(PlayerRole role) => role == PlayerRole.DeepSeek
            ? _data.deepSeekHeadwear ?? string.Empty : _data.harnessHeadwear ?? string.Empty;

        public bool TrySetHeadwear(PlayerRole role, ShopProductDefinition product, out string message)
            => TrySetAccessory(role, AccessorySlot.Front, product, out message);

        public string GetAccessory(PlayerRole role, AccessorySlot slot) => slot == AccessorySlot.Front
            ? GetHeadwear(role) : role == PlayerRole.DeepSeek
                ? _data.deepSeekBackwear ?? string.Empty : _data.harnessBackwear ?? string.Empty;

        public bool TrySetAccessory(PlayerRole role, AccessorySlot slot, ShopProductDefinition product, out string message)
        {
            if ((role != PlayerRole.DeepSeek && role != PlayerRole.Harness) ||
                (slot != AccessorySlot.Front && slot != AccessorySlot.Back) ||
                (product != null && (!product.IsAccessory || product.Slot != slot || GetOwnedCount(product.ProductId) == 0)))
            { message = "只能在对应栏位佩戴已拥有的饰品。"; return false; }
            string id = product != null ? product.ProductId : string.Empty;
            if (GetAccessory(role, slot) == id) { message = string.Empty; return true; }
            var before = Clone(_data);
            if (slot == AccessorySlot.Front)
            { if (role == PlayerRole.DeepSeek) _data.deepSeekHeadwear = id; else _data.harnessHeadwear = id; }
            else
            { if (role == PlayerRole.DeepSeek) _data.deepSeekBackwear = id; else _data.harnessBackwear = id; }
            if (!TrySave(_data, out message)) { _data = before; return false; }
            Changed?.Invoke(); message = product == null ? "已摘下饰品" : "已佩戴 " + product.DisplayName;
            return true;
        }

        public bool HasCleared(string levelId)
        {
            return !string.IsNullOrWhiteSpace(levelId) &&
                _data.clearedLevels.Contains(levelId);
        }

        public string GetSkin(PlayerRole role) => role == PlayerRole.DeepSeek
            ? _data.deepSeekSkin ?? string.Empty : _data.harnessSkin ?? string.Empty;

        public bool HasCompletedChallenge(string entryId) =>
            !string.IsNullOrWhiteSpace(entryId) && _data.completedChallenges.Contains(entryId);

        public bool TrySetSkin(PlayerRole role, ShopProductDefinition product, out string message)
        {
            if ((role != PlayerRole.DeepSeek && role != PlayerRole.Harness) ||
                (product != null && (!product.IsSkin || product.Skin.Role != role ||
                    GetOwnedCount(product.ProductId) == 0 || !product.TryValidate(out _))))
            { message = "只能为对应角色穿戴已拥有的专属服装。"; return false; }
            string id = product != null ? product.ProductId : string.Empty;
            if (GetSkin(role) == id) { message = string.Empty; return true; }
            var before = Clone(_data);
            if (role == PlayerRole.DeepSeek) _data.deepSeekSkin = id; else _data.harnessSkin = id;
            if (!TrySave(_data, out message)) { _data = before; return false; }
            Changed?.Invoke(); message = product == null ? "已恢复默认服装" : "已换装 " + product.DisplayName;
            return true;
        }

        public bool IsAchievementUnlocked(string achievementId) =>
            !string.IsNullOrWhiteSpace(achievementId) &&
            _data.unlockedAchievements.Contains(achievementId);

        public int GetAchievementProgress(string triggerId)
        {
            AchievementProgressRecord record = FindAchievementProgress(
                triggerId);
            return record?.count ?? 0;
        }

        public bool TryRecordAchievementEvent(
            string triggerId,
            int amount,
            AchievementDefinition[] definitions,
            out AchievementDefinition[] unlocked,
            out string message)
        {
            unlocked = Array.Empty<AchievementDefinition>();
            if (string.IsNullOrWhiteSpace(triggerId) || amount <= 0 ||
                definitions == null)
            {
                message = "成就事件无效。";
                return false;
            }

            int maximumTarget = 0;
            for (int index = 0; index < definitions.Length; index++)
            {
                AchievementDefinition definition = definitions[index];
                if (definition != null && definition.TriggerId == triggerId)
                    maximumTarget = Mathf.Max(
                        maximumTarget, definition.TargetCount);
            }
            if (maximumTarget == 0)
            {
                message = "没有成就监听事件：" + triggerId;
                return false;
            }

            LocalPlayerProfileData before = Clone(_data);
            AchievementProgressRecord progress =
                FindAchievementProgress(triggerId);
            if (progress == null)
            {
                progress = new AchievementProgressRecord
                {
                    triggerId = triggerId,
                    count = 0
                };
                _data.achievementProgress.Add(progress);
            }
            bool allMatchingUnlocked = true;
            for (int index = 0; index < definitions.Length; index++)
            {
                AchievementDefinition definition = definitions[index];
                if (definition != null && definition.TriggerId == triggerId &&
                    !IsAchievementUnlocked(definition.AchievementId))
                {
                    allMatchingUnlocked = false;
                    break;
                }
            }
            if (progress.count >= maximumTarget && allMatchingUnlocked)
            {
                message = string.Empty;
                return true;
            }
            progress.count = amount >= maximumTarget - progress.count
                ? maximumTarget
                : progress.count + amount;

            var newlyUnlocked = new List<AchievementDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                AchievementDefinition definition = definitions[index];
                if (definition == null || definition.TriggerId != triggerId ||
                    progress.count < definition.TargetCount ||
                    IsAchievementUnlocked(definition.AchievementId))
                    continue;
                _data.unlockedAchievements.Add(definition.AchievementId);
                newlyUnlocked.Add(definition);
            }

            if (!TrySave(_data, out message))
            {
                _data = before;
                return false;
            }
            unlocked = newlyUnlocked.ToArray();
            Changed?.Invoke();
            message = string.Empty;
            return true;
        }

        public bool TryPurchase(
            ShopProductDefinition product,
            out string message)
        {
            if (product == null)
            {
                message = "商品配置为空。";
                return false;
            }
            if (!product.TryValidate(out message))
            {
                return false;
            }

            int owned = GetOwnedCount(product.ProductId);
            if (!product.Repeatable && owned > 0)
            {
                message = "该商品已经拥有。";
                return false;
            }
            if (_data.whaleVoucherBalance < product.Price)
            {
                message = "鲸元券不足。";
                return false;
            }

            LocalPlayerProfileData before = Clone(_data);
            _data.whaleVoucherBalance -= product.Price;
            OwnedProductRecord record = FindProduct(product.ProductId);
            if (record == null)
            {
                record = new OwnedProductRecord
                {
                    productId = product.ProductId,
                    count = 0
                };
                _data.ownedProducts.Add(record);
            }
            record.count++;

            if (!TrySave(_data, out message))
            {
                _data = before;
                return false;
            }

            message = $"获得 {product.DisplayName} ×1";
            Changed?.Invoke();
            return true;
        }

        public bool TryAwardCompletion(
            MetaLevelDefinition level,
            out int awarded,
            out bool firstClear,
            out string message)
        {
            awarded = 0;
            firstClear = false;
            if (level == null)
            {
                message = "关卡配置为空。";
                return false;
            }
            if (!level.TryValidate(out message))
            {
                return false;
            }

            firstClear = !HasCleared(level.LevelId);
            awarded = firstClear
                ? level.FirstClearVoucherReward
                : level.RepeatClearVoucherReward;
            LocalPlayerProfileData before = Clone(_data);
            if (firstClear)
            {
                _data.clearedLevels.Add(level.LevelId);
            }
            _data.whaleVoucherBalance = checked(
                _data.whaleVoucherBalance + awarded);

            if (!TrySave(_data, out message))
            {
                _data = before;
                awarded = 0;
                firstClear = false;
                return false;
            }

            message = firstClear ? "首次通关奖励" : "重复通关奖励";
            Changed?.Invoke();
            return true;
        }

        public bool TryAwardChallengeCompletion(
            DeepSleep.Runtime.Progression.Bestiary.BestiaryEntryDefinition entry,
            out int awarded, out string message)
        {
            awarded = 0;
            if (entry == null) { message = "图鉴挑战配置为空。"; return false; }
            if (!entry.TryValidate(out message)) return false;
            LocalPlayerProfileData before = Clone(_data);
            if (!HasCompletedChallenge(entry.EntryId)) _data.completedChallenges.Add(entry.EntryId);
            _data.whaleVoucherBalance = checked(_data.whaleVoucherBalance + entry.CompletionVoucherReward);
            if (!TrySave(_data, out message)) { _data = before; return false; }
            awarded = entry.CompletionVoucherReward;
            message = awarded == 0 ? "练习挑战 · 不发放鲸元券" : "图鉴挑战奖励";
            Changed?.Invoke();
            return true;
        }

        private LocalPlayerProfileData LoadOrCreate()
        {
            string main = SavePath;
            string backup = Path.Combine(
                SaveDirectory,
                BackupFileName);
            if (TryLoad(main, out LocalPlayerProfileData loaded) ||
                TryLoad(backup, out loaded))
            {
                return loaded;
            }
            return new LocalPlayerProfileData();
        }

        private static bool TryLoad(
            string path,
            out LocalPlayerProfileData data)
        {
            data = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<LocalPlayerProfileData>(
                    File.ReadAllText(path));
                return TryNormalize(data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LocalProfile] 无法读取 {path}：{exception.Message}");
                data = null;
                return false;
            }
        }

        private bool TrySave(LocalPlayerProfileData data, out string message)
        {
            string directory = SaveDirectory;
            string main = SavePath;
            string backup = Path.Combine(directory, BackupFileName);
            string temporary = main + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(main))
                {
                    // 先保留旧主档；不让Replace删除/覆盖上一次的备份文件。
                    // 优先原子替换；Mono/Windows替换失败时使用已有备份保护覆盖写入。
                    File.Copy(main, backup, true);
                    try
                    {
                        File.Replace(temporary, main, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(temporary, main, true);
                    }
                    catch (IOException)
                    {
                        File.Copy(temporary, main, true);
                    }
                }
                else
                {
                    File.Move(temporary, main);
                    File.Copy(main, backup, true);
                }
                message = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                message = "本地存档失败：" + exception.Message;
                Debug.LogError("[LocalProfile] " + message, this);
                return false;
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private OwnedProductRecord FindProduct(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return null;
            }
            for (int index = 0; index < _data.ownedProducts.Count; index++)
            {
                if (_data.ownedProducts[index].productId == productId)
                {
                    return _data.ownedProducts[index];
                }
            }
            return null;
        }

        private AchievementProgressRecord FindAchievementProgress(
            string triggerId)
        {
            if (string.IsNullOrWhiteSpace(triggerId)) return null;
            for (int index = 0; index < _data.achievementProgress.Count; index++)
                if (_data.achievementProgress[index].triggerId == triggerId)
                    return _data.achievementProgress[index];
            return null;
        }

        private static bool TryNormalize(LocalPlayerProfileData data)
        {
            if (data == null || data.version > CurrentVersion ||
                data.version <= 0 || data.whaleVoucherBalance < 0)
            {
                return false;
            }

            data.version = CurrentVersion;
            data.ownedProducts ??= new List<OwnedProductRecord>();
            data.clearedLevels ??= new List<string>();
            data.completedChallenges ??= new List<string>();
            var challengeIds = new HashSet<string>();
            data.completedChallenges.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !challengeIds.Add(id));
            data.unlockedAchievements ??= new List<string>();
            data.achievementProgress ??=
                new List<AchievementProgressRecord>();
            var productIds = new HashSet<string>();
            for (int index = data.ownedProducts.Count - 1; index >= 0; index--)
            {
                OwnedProductRecord record = data.ownedProducts[index];
                if (record == null || string.IsNullOrWhiteSpace(record.productId) ||
                    record.count <= 0 || !productIds.Add(record.productId))
                {
                    data.ownedProducts.RemoveAt(index);
                }
            }
            var levelIds = new HashSet<string>();
            if (!productIds.Contains(data.deepSeekHeadwear ?? string.Empty)) data.deepSeekHeadwear = string.Empty;
            if (!productIds.Contains(data.harnessHeadwear ?? string.Empty)) data.harnessHeadwear = string.Empty;
            if (!productIds.Contains(data.deepSeekBackwear ?? string.Empty)) data.deepSeekBackwear = string.Empty;
            if (!productIds.Contains(data.harnessBackwear ?? string.Empty)) data.harnessBackwear = string.Empty;
            if (!productIds.Contains(data.deepSeekSkin ?? string.Empty)) data.deepSeekSkin = string.Empty;
            if (!productIds.Contains(data.harnessSkin ?? string.Empty)) data.harnessSkin = string.Empty;
            for (int index = data.clearedLevels.Count - 1; index >= 0; index--)
            {
                string id = data.clearedLevels[index];
                if (string.IsNullOrWhiteSpace(id) || !levelIds.Add(id))
                {
                    data.clearedLevels.RemoveAt(index);
                }
            }
            var achievementIds = new HashSet<string>();
            for (int index = data.unlockedAchievements.Count - 1;
                 index >= 0; index--)
            {
                string id = data.unlockedAchievements[index];
                if (string.IsNullOrWhiteSpace(id) || !achievementIds.Add(id))
                    data.unlockedAchievements.RemoveAt(index);
            }
            var triggerIds = new HashSet<string>();
            for (int index = data.achievementProgress.Count - 1;
                 index >= 0; index--)
            {
                AchievementProgressRecord record =
                    data.achievementProgress[index];
                if (record == null || string.IsNullOrWhiteSpace(record.triggerId) ||
                    record.count <= 0 || !triggerIds.Add(record.triggerId))
                    data.achievementProgress.RemoveAt(index);
            }
            return true;
        }

        private static LocalPlayerProfileData Clone(LocalPlayerProfileData data)
        {
            return JsonUtility.FromJson<LocalPlayerProfileData>(
                JsonUtility.ToJson(data));
        }
    }
}
