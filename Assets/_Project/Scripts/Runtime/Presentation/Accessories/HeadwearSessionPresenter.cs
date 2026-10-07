using System.IO;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Accessories
{
    /// <summary>交换双角色前饰/背饰四个稳定ID；不传逐帧Transform，不参与伤害或购买授权。</summary>
    public sealed class HeadwearSessionPresenter : MonoBehaviour
    {
        public CoopSessionController Session;
        public HeadwearDefinition[] Catalog;
        public HeadwearSpriteView DeepSeek, Harness;
        public HeadwearSpriteView DeepSeekBack, HarnessBack;
        public HeadwearImageView[] Previews;
        private LocalPlayerProfileStore _profile;
        private ushort _ds, _hs;
        private ushort _dsBack, _hsBack;
        private bool _remoteReceived;
        public void Bind(LocalPlayerProfileStore profile)
        {
            _profile = profile;
            _profile.Changed += Refresh;
            foreach (var preview in Previews) preview.Bind(profile);
            Refresh();
        }
        private void OnEnable()
        {
            Session.Changed += Refresh; Session.PeerJoined += Joined;
            Session.SessionClosed += Closed;
            Session.PeerMessage += ReadPeer; Session.AuthorityMessage += ReadAuthority;
        }
        private void OnDisable()
        {
            Session.Changed -= Refresh; Session.PeerJoined -= Joined; Session.SessionClosed -= Closed;
            Session.PeerMessage -= ReadPeer; Session.AuthorityMessage -= ReadAuthority;
            if (_profile != null) _profile.Changed -= Refresh;
        }
        private ushort Local(PlayerRole role, AccessorySlot slot = AccessorySlot.Front)
        {
            string id = _profile.GetAccessory(role, slot);
            foreach (var item in Catalog) if (item.Product.Slot == slot && item.Product.ProductId == id) return (ushort)item.NetworkId;
            return 0;
        }
        private HeadwearDefinition Resolve(ushort id)
        {
            foreach (var item in Catalog) if (item.NetworkId == id) return item;
            return null;
        }
        private bool Known(ushort id, AccessorySlot slot) => id == 0 || (Resolve(id) != null && Resolve(id).Product.Slot == slot);
        private bool Known(ushort ds, ushort hs, ushort dsBack, ushort hsBack) =>
            Known(ds, AccessorySlot.Front) && Known(hs, AccessorySlot.Front) && Known(dsBack, AccessorySlot.Back) && Known(hsBack, AccessorySlot.Back);
        private void Apply()
        {
            DeepSeek.SetEquipped(Resolve(_ds)); Harness.SetEquipped(Resolve(_hs));
            DeepSeekBack.SetEquipped(Resolve(_dsBack)); HarnessBack.SetEquipped(Resolve(_hsBack));
            foreach (var preview in Previews)
                preview.SetSessionEquipment(preview.Slot == AccessorySlot.Back ? _dsBack : _ds, preview.Slot == AccessorySlot.Back ? _hsBack : _hs);
        }
        private void Joined() { _remoteReceived = false; Refresh(); }
        private void Closed() { _remoteReceived = false; Refresh(); }
        private void Refresh()
        {
            if (_profile == null) return;
            if (Session.Phase == SessionPhase.Offline || !_remoteReceived)
            { _ds = Local(PlayerRole.DeepSeek); _hs = Local(PlayerRole.Harness);
              _dsBack = Local(PlayerRole.DeepSeek, AccessorySlot.Back); _hsBack = Local(PlayerRole.Harness, AccessorySlot.Back); }
            if (Session.IsAuthority)
            {
                if (Session.HostRole == PlayerRole.DeepSeek)
                { _ds = Local(PlayerRole.DeepSeek); _dsBack = Local(PlayerRole.DeepSeek, AccessorySlot.Back); }
                else { _hs = Local(PlayerRole.Harness); _hsBack = Local(PlayerRole.Harness, AccessorySlot.Back); }
                Session.SendAuthority(NetworkMessageCatalog.Authority.Headwear, Write, true);
            }
            else if (Session.HasPeer)
                Session.SendToAuthority(NetworkMessageCatalog.Peer.Headwear,
                    w => { w.Write(Local(PlayerRole.DeepSeek)); w.Write(Local(PlayerRole.Harness));
                        w.Write(Local(PlayerRole.DeepSeek, AccessorySlot.Back)); w.Write(Local(PlayerRole.Harness, AccessorySlot.Back)); }, true);
            Apply();
        }
        private void Write(BinaryWriter writer) { writer.Write(_ds); writer.Write(_hs); writer.Write(_dsBack); writer.Write(_hsBack); }
        private void ReadPeer(byte kind, BinaryReader reader)
        {
            if (kind != NetworkMessageCatalog.Peer.Headwear || !Session.IsAuthority) return;
            ushort ds = reader.ReadUInt16(), hs = reader.ReadUInt16(), dsBack = reader.ReadUInt16(), hsBack = reader.ReadUInt16();
            if (!Known(ds, hs, dsBack, hsBack)) return;
            // 客人不能覆盖房主的饰品；客人外观仅为表现声明，不授予本地持有权。
            if (Session.HostRole == PlayerRole.DeepSeek) { _hs = hs; _hsBack = hsBack; }
            else { _ds = ds; _dsBack = dsBack; }
            _remoteReceived = true; Apply();
            Session.SendAuthority(NetworkMessageCatalog.Authority.Headwear, Write, true);
        }
        private void ReadAuthority(byte kind, BinaryReader reader)
        {
            if (kind != NetworkMessageCatalog.Authority.Headwear || Session.IsAuthority) return;
            ushort ds = reader.ReadUInt16(), hs = reader.ReadUInt16(), dsBack = reader.ReadUInt16(), hsBack = reader.ReadUInt16();
            if (!Known(ds, hs, dsBack, hsBack)) return;
            _ds = ds; _hs = hs; _dsBack = dsBack; _hsBack = hsBack; _remoteReceived = true; Apply();
        }
    }
}
