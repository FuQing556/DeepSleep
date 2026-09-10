using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
    }

    /// <summary>
    /// 局外档案的唯一写入口。使用稳定字符串 ID，不把 ScriptableObject
    /// 或场景引用写进 JSON。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class LocalPlayerProfileStore : MonoBehaviour
    {
        public const int CurrentVersion = 1;
        private const string FileName = "profile.json";
        private const string BackupFileName = "profile.backup.json";

        private LocalPlayerProfileData _data;

        public event Action Changed;
        public int WhaleVoucherBalance => _data?.whaleVoucherBalance ?? 0;
        public string SavePath => Path.Combine(
            Application.persistentDataPath,
            FileName);

        private void Awake()
        {
            _data = LoadOrCreate();
        }

        public int GetOwnedCount(string productId)
        {
            OwnedProductRecord record = FindProduct(productId);
            return record?.count ?? 0;
        }

        public bool HasCleared(string levelId)
        {
            return !string.IsNullOrWhiteSpace(levelId) &&
                _data.clearedLevels.Contains(levelId);
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

        private LocalPlayerProfileData LoadOrCreate()
        {
            string main = SavePath;
            string backup = Path.Combine(
                Application.persistentDataPath,
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
            string directory = Application.persistentDataPath;
            string main = SavePath;
            string backup = Path.Combine(directory, BackupFileName);
            string temporary = main + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(main))
                {
                    try
                    {
                        File.Replace(temporary, main, backup);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(main, backup, true);
                        File.Delete(main);
                        File.Move(temporary, main);
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
            for (int index = data.clearedLevels.Count - 1; index >= 0; index--)
            {
                string id = data.clearedLevels[index];
                if (string.IsNullOrWhiteSpace(id) || !levelIds.Add(id))
                {
                    data.clearedLevels.RemoveAt(index);
                }
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
