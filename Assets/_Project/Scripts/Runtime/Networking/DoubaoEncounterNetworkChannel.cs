using System.Collections.Generic;
using System.IO;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>同步豆包本体与气泡墙的权威表现；客户端镜像没有任何碰撞和伤害能力。</summary>
    public sealed class DoubaoEncounterNetworkChannel : MonoBehaviour
    {
        private const byte Snapshot = NetworkMessageCatalog.Authority.DoubaoSnapshot;

        [SerializeField] private CoopSessionController _session;
        [SerializeField] private DoubaoWordWallEncounter2D _encounter;
        [SerializeField] private DoubaoBoss2D _boss;
        [SerializeField] private SpriteHitFlash2D _bossHitFlash;
        [SerializeField] private DoubaoWordWallReplicaView2D _viewPrefab;
        [SerializeField] private Transform _viewRoot;
        [SerializeField, Min(1)] private int _maximumViews;

        private readonly Dictionary<uint, DoubaoWordWallReplicaView2D> _views = new();
        private readonly Dictionary<uint, uint> _seenFrames = new();
        private readonly Stack<DoubaoWordWallReplicaView2D> _available = new();
        private readonly List<uint> _removed = new();
        private readonly List<(uint id, Vector2 position, Vector2 size, string phrase)> _incoming = new();
        private readonly HashSet<uint> _incomingIds = new();
        private uint _frame;
        private uint _lastReceived;
        private bool _received;
        private int _created;
        private float _nextSend;

        private void Awake()
        {
            if (_session == null || _encounter == null || _boss == null || _bossHitFlash == null ||
                _viewPrefab == null || _viewRoot == null || _maximumViews < 1)
            {
                Debug.LogError("[DoubaoNetwork] 联机表现通道装配不完整。", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _session.AuthorityMessage += Read;
            _session.SessionOpened += OnSessionOpened;
            _session.SessionClosed += Clear;
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.AuthorityMessage -= Read;
                _session.SessionOpened -= OnSessionOpened;
                _session.SessionClosed -= Clear;
            }
            Clear();
        }

        private void LateUpdate()
        {
            if (!_session.IsAuthority || _session.Phase != SessionPhase.Playing ||
                Time.unscaledTime < _nextSend)
                return;
            _nextSend = Time.unscaledTime + 1f / _session.Config.SnapshotRate;
            WriteSnapshot();
        }

        private void WriteSnapshot()
        {
            IReadOnlyList<DoubaoWordWallBlock2D> blocks = _encounter.ActiveBlocks;
            int count = Mathf.Min(blocks.Count, byte.MaxValue);
            uint frame = ++_frame;
            _session.SendAuthority(Snapshot, writer =>
            {
                writer.Write(frame);
                writer.Write((byte)_encounter.State);
                writer.Write(_boss.IsActive);
                writer.Write(_boss.transform.position.x);
                writer.Write(_boss.transform.position.y);
                writer.Write(_boss.CurrentHealth);
                writer.Write(_boss.IsDeparting);
                writer.Write(_boss.VisualElapsed);
                writer.Write(_bossHitFlash.Sequence);
                writer.Write(_bossHitFlash.NormalizedAge);
                writer.Write((byte)count);
                for (int index = 0; index < count; index++)
                {
                    DoubaoWordWallBlock2D block = blocks[index];
                    writer.Write(block.ReplicationId);
                    writer.Write(block.transform.position.x);
                    writer.Write(block.transform.position.y);
                    writer.Write(block.Size.x);
                    writer.Write(block.Size.y);
                    writer.Write(block.Phrase ?? string.Empty);
                }
            // 密集迷宫快照超过单个 MTU；适配器的可靠通道支持分片。
            }, true);
        }

        private void Read(byte kind, BinaryReader reader)
        {
            if (kind != Snapshot || _session.IsAuthority || _session.Phase != SessionPhase.Playing ||
                !NetworkMessageCatalog.TryValidatePayload(kind,
                    NetworkMessageCatalog.Direction.AuthorityToPeer, reader, out _)) return;
            uint frame = reader.ReadUInt32();
            if (_received && !RemoteCommandSource.IsNewer(frame, _lastReceived)) return;
            DoubaoEncounterState state = (DoubaoEncounterState)reader.ReadByte();
            bool bossShown = reader.ReadBoolean();
            Vector2 bossPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            float bossHealth = reader.ReadSingle();
            bool departing = reader.ReadBoolean();
            float visualElapsed = reader.ReadSingle();
            uint hitSequence = reader.ReadUInt32();
            float hitAge = reader.ReadSingle();
            int count = reader.ReadByte();
            if (count > _maximumViews) return;
            _incoming.Clear();
            _incomingIds.Clear();
            for (int index = 0; index < count; index++)
            {
                uint id = reader.ReadUInt32();
                if (id == 0 || !_incomingIds.Add(id)) return;
                Vector2 position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                Vector2 size = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                string phrase = reader.ReadString();
                _incoming.Add((id, position, size, phrase));
            }

            // 数据完整、ID 唯一且全部可容纳以后才更新序号、Boss 和现有镜像。
            // 拒绝坏帧不能把接收水位推进，下一条合法帧仍可正常恢复。
            _received = true;
            _lastReceived = frame;
            _boss.ApplyReplica(bossShown, bossPosition, bossHealth, departing, visualElapsed);
            _bossHitFlash.ApplyReplica(hitSequence, hitAge);
            foreach (var incoming in _incoming) _seenFrames[incoming.id] = frame;

            // 先回收消失的 ID，再租新 ID；满池换行也不能漏掉新气泡。
            _removed.Clear();
            foreach (var pair in _views)
                if (!_seenFrames.TryGetValue(pair.Key, out uint seen) || seen != frame)
                    _removed.Add(pair.Key);
            foreach (uint id in _removed) ReturnView(id);
            foreach (var incoming in _incoming)
            {
                uint id = incoming.id;
                if (!_views.TryGetValue(id, out DoubaoWordWallReplicaView2D view))
                {
                    view = RentView();
                    if (view == null) continue;
                    _views.Add(id, view);
                }
                _seenFrames[id] = frame;
                view.Apply(id, incoming.position, incoming.size, incoming.phrase, _session.Config.RemoteInterpolationSpeed);
            }

            if (state == DoubaoEncounterState.Idle || state == DoubaoEncounterState.Complete)
                ClearViews();
        }

        private DoubaoWordWallReplicaView2D RentView()
        {
            if (_available.Count > 0) return _available.Pop();
            if (_created >= _maximumViews) return null;
            _created++;
            DoubaoWordWallReplicaView2D view = Instantiate(_viewPrefab, _viewRoot);
            view.Clear();
            return view;
        }

        private void ReturnView(uint id)
        {
            if (!_views.Remove(id, out DoubaoWordWallReplicaView2D view)) return;
            _seenFrames.Remove(id);
            view.Clear();
            _available.Push(view);
        }

        private void OnSessionOpened(bool authority) => Clear();

        private void Clear()
        {
            ClearViews();
            if (_boss != null) _boss.ApplyReplica(false, Vector2.zero, 0f);
            if (_bossHitFlash != null) _bossHitFlash.ResetFeedback();
            _frame = 0;
            _received = false;
            _lastReceived = 0;
            _nextSend = 0f;
        }

        private void ClearViews()
        {
            _removed.Clear();
            foreach (uint id in _views.Keys) _removed.Add(id);
            for (int index = 0; index < _removed.Count; index++) ReturnView(_removed[index]);
        }
    }
}
