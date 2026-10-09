using System.IO;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Skins
{
    /// <summary>DS服装选择随角色所有者交换；随机姿态只由权威抽取，复用原角色贴图快照。</summary>
    public sealed class PlayerSkinSessionPresenter : MonoBehaviour
    {
        public CoopSessionController Session;
        public RestNodePrototypeController2D Node;
        public PlayerDownedVisual2D DeepSeekVisual;
        public ShopProductDefinition[] Catalog;
        public PlayerSkinImageView[] Previews;
        private LocalPlayerProfileStore _profile;
        private ushort _skinId, _remoteId;
        private bool _playing;
        private RestNodeState _lastNodeState;
        public ushort CurrentSkinId => _skinId;
        private bool CanDraw => Session.Phase == SessionPhase.Offline || Session.IsAuthority;

        public void Bind(LocalPlayerProfileStore profile)
        {
            _profile = profile; _profile.Changed += Refresh;
            foreach (var preview in Previews) preview.Bind(profile);
            Refresh();
        }
        private void OnEnable()
        {
            Session.Changed += Refresh; Session.PeerJoined += Joined; Session.SessionClosed += Closed;
            Session.PeerMessage += ReadPeer; Session.AuthorityMessage += ReadAuthority;
            Session.Selection.SelectionConfirmed += Selected;
            Node.StateChanged += NodeChanged;
        }
        private void OnDisable()
        {
            Session.Changed -= Refresh; Session.PeerJoined -= Joined; Session.SessionClosed -= Closed;
            Session.PeerMessage -= ReadPeer; Session.AuthorityMessage -= ReadAuthority;
            Session.Selection.SelectionConfirmed -= Selected; Node.StateChanged -= NodeChanged;
            if (_profile != null) _profile.Changed -= Refresh;
        }
        private ShopProductDefinition Resolve(ushort id)
        {
            foreach (var product in Catalog) if (product.Skin.NetworkId == id) return product;
            return null;
        }
        private ushort Local()
        {
            foreach (var product in Catalog)
                if (_profile.GetSkin(PlayerRole.DeepSeek) == product.ProductId) return product.Skin.NetworkId;
            return 0;
        }
        private void Apply()
        {
            var product = Resolve(_skinId);
            if (CanDraw) DeepSeekVisual.SetSkin(product != null ? product.Skin : null);
            foreach (var preview in Previews) preview.SetSessionSkin(_skinId);
        }
        private void Selected(PlayerRole role) => Refresh();
        private void Joined() { _remoteId = 0; Refresh(); }
        private void Closed() { _remoteId = 0; _playing = false; Refresh(); }
        private void Refresh()
        {
            if (_profile == null) return;
            if (Session.Phase == SessionPhase.Offline) _skinId = Local();
            else if (Session.IsAuthority)
            {
                _skinId = Session.HostRole == PlayerRole.DeepSeek ? Local() : _remoteId;
                Session.SendAuthority(NetworkMessageCatalog.Authority.Skin, w => w.Write(_skinId), true);
            }
            else if (Session.HasPeer)
                Session.SendToAuthority(NetworkMessageCatalog.Peer.Skin, w => w.Write(Local()), true);
            Apply();
            bool playing = Session.Phase == SessionPhase.Playing || Session.IsSoloPlaying;
            if (playing && !_playing && CanDraw) DeepSeekVisual.RerollSkinPose();
            _playing = playing;
        }
        private void NodeChanged(RestNodeState state)
        {
            if (state == _lastNodeState) return;
            bool directOpen = state == RestNodeState.Open && _lastNodeState != RestNodeState.Revealing;
            _lastNodeState = state;
            // 入节点以开始揭示为边沿，离节点以开始离场为边沿；Open/网络重复状态不重抽。
            // 图鉴配装/检查点可直接Open，仍算进入节点。
            if (CanDraw && (state == RestNodeState.Revealing || state == RestNodeState.Departing || directOpen))
                DeepSeekVisual.RerollSkinPose();
        }
        private void ReadPeer(byte kind, BinaryReader reader)
        {
            if (kind != NetworkMessageCatalog.Peer.Skin || !Session.IsAuthority) return;
            ushort id = reader.ReadUInt16();
            if (id != 0 && Resolve(id) == null) return;
            if (Session.HostRole != PlayerRole.DeepSeek) _remoteId = id;
            Refresh();
        }
        private void ReadAuthority(byte kind, BinaryReader reader)
        {
            if (kind != NetworkMessageCatalog.Authority.Skin || Session.IsAuthority) return;
            ushort id = reader.ReadUInt16();
            if (id != 0 && Resolve(id) == null) return;
            _skinId = id; Apply();
        }
    }
}
