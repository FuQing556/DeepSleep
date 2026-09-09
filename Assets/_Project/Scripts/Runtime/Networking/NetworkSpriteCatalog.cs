using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>显式资源ID表；网络上传稳定ID，不传路径/名字/本地InstanceID。</summary>
    [CreateAssetMenu(menuName = "DeepSleep/配置/联网角色姿态目录", fileName = "CFG_NetworkSprites")]
    public sealed class NetworkSpriteCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { public uint Id; public Sprite Sprite; }
        public Entry[] Entries;
        private Dictionary<uint, Sprite> _byId;
        private Dictionary<Sprite, uint> _bySprite;
        public bool Initialize()
        {
            if (_byId != null) return true;
            _byId = new Dictionary<uint, Sprite>(); _bySprite = new Dictionary<Sprite, uint>();
            foreach (var entry in Entries)
            {
                if (entry.Id == 0 || entry.Sprite == null || !_byId.TryAdd(entry.Id, entry.Sprite) ||
                    !_bySprite.TryAdd(entry.Sprite, entry.Id))
                { _byId = null; _bySprite = null; Debug.LogError("[NetworkSpriteCatalog] 重复或缺失资源ID。", this); return false; }
            }
            return true;
        }
        public uint GetId(Sprite sprite) => sprite != null && _bySprite.TryGetValue(sprite, out uint id) ? id : 0;
        public bool TryResolve(uint id, out Sprite sprite) => _byId.TryGetValue(id, out sprite);
    }
}
