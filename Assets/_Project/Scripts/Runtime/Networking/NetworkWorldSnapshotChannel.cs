using System.Collections.Generic;
using System.IO;
using System;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Presentation;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>敌人与玩法弹体的权威镜像；只遍历显式对象池，不扫描整个场景，不同步装饰粒子。</summary>
    public sealed class NetworkWorldSnapshotChannel : MonoBehaviour
    {
        private const byte ENTITY = NetworkMessageCatalog.Authority.WorldEntity,
            DESPAWN = NetworkMessageCatalog.Authority.WorldDespawn;
        public CoopSessionController Session;
        public NetworkSpriteCatalog Catalog;
        public EnemyActorPool2D[] EnemyPools = System.Array.Empty<EnemyActorPool2D>();
        public RiceProjectilePool Rice;
        public EnemyProjectilePool2D EnemyBullets;
        // 特殊遭遇拥有自己的池；不混进由关卡普通敌人清单派生的EnemyPools。
        public EnemyActorPool2D[] EncounterEnemyPools = Array.Empty<EnemyActorPool2D>();
        public EnemyProjectilePool2D[] EncounterProjectilePools = Array.Empty<EnemyProjectilePool2D>();
        public NetworkEntityView ViewPrefab;
        public Transform ViewRoot;
        public int MaximumViews;
        private sealed class Source
        {
            public uint Id, Seen, Generation;
            public SpriteRenderer[] Renderers;
            public SpriteHitFlash2D HitFlash;
            public uint HitSequence, LastSentHitSequence;
            public float HitAge;
            public UnityEngine.Rendering.SortingGroup Group;
            public NetworkWorldSnapshotChannel Owner;
            public Transform Root;
            public Action<BinaryWriter> Write;

            public void WriteSnapshot(BinaryWriter writer)
            {
                writer.Write(Owner._frame); writer.Write(Id);
                Vector3 position = Root.position;
                writer.Write(position.x); writer.Write(position.y); writer.Write(position.z);
                bool grouped = Group != null && Group.enabled; writer.Write(grouped);
                writer.Write(grouped ? Group.sortingLayerID : 0); writer.Write(grouped ? Group.sortingOrder : 0);
                NetworkEntityView.Write(writer, Renderers, Owner.Catalog);
                writer.Write(HitSequence); writer.Write(HitAge);
            }
        }
        private readonly Dictionary<Component, Source> _sources = new();
        private readonly Dictionary<uint, NetworkEntityView> _views = new();
        private readonly Stack<NetworkEntityView> _available = new();
        private readonly List<Component> _removed = new();
        private readonly HashSet<uint> _deadThisFrame = new();
        private uint _frame, _nextId, _receivedFrame;
        private int _created;
        private float _nextSend;
        private uint _despawnId;
        private Action<BinaryWriter> _writeDespawn;
        public int VisibleEntities => _views.Count;

        private void Awake()
        {
            if (!TryValidateConfiguration(out _) || !Catalog.Initialize())
            { Debug.LogError("[NetworkWorld] 网络世界装配不完整。", this); enabled = false; }
        }
        public bool TryValidateConfiguration(out string reason)
        {
            if (Session == null || Catalog == null || Rice == null || EnemyBullets == null ||
                ViewPrefab == null || ViewRoot == null || MaximumViews < 1 || EnemyPools == null ||
                EncounterEnemyPools == null || EncounterProjectilePools == null)
            { reason = "World snapshot core references or capacities are invalid."; return false; }
            for (int i = 0; i < EnemyPools.Length; i++)
            {
                if (EnemyPools[i] == null)
                { reason = "World snapshot has a missing enemy pool."; return false; }
                for (int j = 0; j < i; j++)
                    if (EnemyPools[i] == EnemyPools[j])
                    { reason = "World snapshot repeats an enemy pool."; return false; }
            }
            reason = string.Empty;
            for (int i = 0; i < EncounterEnemyPools.Length; i++)
                if (EncounterEnemyPools[i] == null || Array.IndexOf(EncounterEnemyPools, EncounterEnemyPools[i]) != i ||
                    Array.IndexOf(EnemyPools, EncounterEnemyPools[i]) >= 0)
                { reason = "Encounter enemy pools are missing or duplicated."; return false; }
            for (int i = 0; i < EncounterProjectilePools.Length; i++)
                if (EncounterProjectilePools[i] == null || Array.IndexOf(EncounterProjectilePools, EncounterProjectilePools[i]) != i ||
                    EncounterProjectilePools[i] == EnemyBullets)
                { reason = "Encounter projectile pools are missing or duplicated."; return false; }
            return true;
        }
        private void OnEnable() { if (Session == null) return; Session.AuthorityMessage += Read; Session.SessionClosed += Clear; Session.SessionOpened += Open; }
        private void OnDisable() { if (Session != null) { Session.AuthorityMessage -= Read; Session.SessionClosed -= Clear; Session.SessionOpened -= Open; } Clear(); }
        private void Open(bool authority) => Clear();

        private void LateUpdate()
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 1f / Session.Config.SnapshotRate; _frame++;
            // Instances 是 IReadOnlyList：索引访问避免每个池、每个同步刻装箱 List 枚举器。
            for (int poolIndex = 0; poolIndex < EnemyPools.Length; poolIndex++)
            {
                var entities = EnemyPools[poolIndex].Instances;
                for (int i = 0; i < entities.Count; i++)
                    if (entities[i].gameObject.activeInHierarchy) Publish(entities[i]);
            }
            var rice = Rice.Instances;
            for (int p = 0; p < EncounterEnemyPools.Length; p++)
            {
                var entities = EncounterEnemyPools[p].Instances;
                for (int i = 0; i < entities.Count; i++) if (entities[i].gameObject.activeInHierarchy) Publish(entities[i]);
            }
            for (int p = 0; p < EncounterProjectilePools.Length; p++)
            {
                var entities = EncounterProjectilePools[p].Instances;
                for (int i = 0; i < entities.Count; i++) if (entities[i].IsRented) Publish(entities[i]);
            }
            for (int i = 0; i < rice.Count; i++) if (rice[i].IsRented) Publish(rice[i]);
            var bullets = EnemyBullets.Instances;
            for (int i = 0; i < bullets.Count; i++) if (bullets[i].IsRented) Publish(bullets[i]);
            _removed.Clear();
            foreach (var pair in _sources) if (pair.Value.Seen != _frame) _removed.Add(pair.Key);
            foreach (var key in _removed)
            {
                uint id = _sources[key].Id;
                SendDespawn(id);
                _sources.Remove(key);
            }
        }

        private void Publish(Component entity)
        {
            uint generation = entity switch { EnemyActor2D enemy => enemy.SpawnGeneration,
                RiceProjectile rice => rice.SpawnGeneration, EnemyProjectile2D bullet => bullet.SpawnGeneration, _ => 0 };
            if (_sources.TryGetValue(entity, out var previous) && previous.Generation != generation)
            {
                SendDespawn(previous.Id);
                _sources.Remove(entity);
            }
            if (!_sources.TryGetValue(entity, out var source))
            {
                // 每个租借周期只缓存一次已装配的渲染层，实体ID不复用。
                SpriteHitFlash2D flash = entity is EnemyActor2D ? entity.GetComponent<SpriteHitFlash2D>() : null;
                if (entity is EnemyActor2D && (flash == null || flash.Sources == null || flash.Sources.Length == 0))
                { Debug.LogError("[NetworkWorld] 敌人缺少显式基础视觉与受击闪光装配。", entity); return; }
                source = new Source { Id = ++_nextId, Generation = generation, Owner = this, Root = entity.transform,
                    HitFlash = flash,
                    // 覆盖层只由命中事实本地重建，不能混入普通精灵快照。
                    Renderers = flash != null ? flash.Sources : entity.GetComponentsInChildren<SpriteRenderer>(true),
                    Group = entity.GetComponent<UnityEngine.Rendering.SortingGroup>() };
                if (source.Renderers.Length > ViewPrefab.Layers.Length || source.Renderers.Length > byte.MaxValue)
                { Debug.LogError("[NetworkWorld] 镜像预制体层容量不足。", entity); return; }
                source.Write = source.WriteSnapshot;
                _sources.Add(entity, source);
            }
            source.Seen = _frame;
            source.HitSequence = source.HitFlash != null ? source.HitFlash.Sequence : 0;
            source.HitAge = source.HitFlash != null ? source.HitFlash.NormalizedAge : 1f;
            bool newHit = source.HitSequence != source.LastSentHitSequence;
            Session.SendAuthority(ENTITY, source.Write, newHit);
            source.LastSentHitSequence = source.HitSequence;
        }

        private void SendDespawn(uint id)
        {
            _despawnId = id;
            _writeDespawn ??= WriteDespawn;
            // SendAuthority 在返回前同步完成字段序列化；实际 Transport 接收的是独立数组。
            Session.SendAuthority(DESPAWN, _writeDespawn, true);
        }
        private void WriteDespawn(BinaryWriter writer) { writer.Write(_frame); writer.Write(_despawnId); }

        private void Read(byte kind, BinaryReader r)
        {
            if ((kind != ENTITY && kind != DESPAWN) ||
                !NetworkMessageCatalog.TryValidatePayload(kind,
                    NetworkMessageCatalog.Direction.AuthorityToPeer, r, out _)) return;
            uint frame = r.ReadUInt32(), id = r.ReadUInt32();
            if (kind == DESPAWN)
            {
                if (frame >= _receivedFrame) AdvanceFrame(frame);
                _deadThisFrame.Add(id);
                if (_views.Remove(id, out var old)) { old.Clear(); _available.Push(old); }
                return;
            }
            _views.TryGetValue(id, out var view);
            long entityStart = r.BaseStream.Position;
            // 完整线格式和镜像容量先验通过以后，才允许推进水位、取池或写表现。
            r.BaseStream.Position = entityStart + 21;
            int count = r.ReadByte();
            r.BaseStream.Position = entityStart;
            if (count > (view != null ? view.Layers.Length : ViewPrefab.Layers.Length)) return;
            if (frame < _receivedFrame)
            {
                // 另一实体的新普通帧可能越过可靠命中帧。旧姿态仍丢弃，但已存在的同 ID
                // 可以接收独立去重的命中事实；绝不为旧包重建已死亡/尚未出现的实体。
                if (view != null && view.HitFlash != null)
                {
                    r.BaseStream.Position = r.BaseStream.Length - 8;
                    view.HitFlash.ApplyReplica(r.ReadUInt32(), r.ReadSingle());
                }
                return;
            }
            AdvanceFrame(frame);
            if (_deadThisFrame.Contains(id)) return;
            if (view == null)
            {
                if (_available.Count > 0) view = _available.Pop();
                else if (_created < MaximumViews) { view = Instantiate(ViewPrefab, ViewRoot); _created++; }
                else return;
                view.InterpolationSpeed = Session.Config.RemoteInterpolationSpeed;
                _views.Add(id, view);
            }
            view.Read(r, Catalog);
        }
        private void AdvanceFrame(uint frame)
        {
            if (frame <= _receivedFrame) return;
            _receivedFrame = frame; _deadThisFrame.Clear();
        }
        private void Clear()
        {
            foreach (var pair in _views) { pair.Value.Clear(); _available.Push(pair.Value); }
            foreach (var view in _available) view.ClearMotionTrail();
            _views.Clear(); _sources.Clear(); _deadThisFrame.Clear(); _receivedFrame = _frame = _nextId = 0;
        }
    }
}
