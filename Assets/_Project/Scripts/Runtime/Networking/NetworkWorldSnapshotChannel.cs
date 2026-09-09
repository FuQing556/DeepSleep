using System.Collections.Generic;
using System.IO;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Projectiles;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>敌人与玩法弹体的权威镜像；只遍历显式对象池，不扫描整个场景，不同步装饰粒子。</summary>
    public sealed class NetworkWorldSnapshotChannel : MonoBehaviour
    {
        private const byte ENTITY = 33, DESPAWN = 34;
        public CoopSessionController Session;
        public NetworkSpriteCatalog Catalog;
        public EnemyActorPool2D Windows, Snakes;
        public RiceProjectilePool Rice;
        public EnemyProjectilePool2D EnemyBullets;
        public NetworkEntityView ViewPrefab;
        public Transform ViewRoot;
        public int MaximumViews;
        private sealed class Source
        {
            public uint Id, Seen, Generation;
            public SpriteRenderer[] Renderers;
            public UnityEngine.Rendering.SortingGroup Group;
        }
        private readonly Dictionary<Component, Source> _sources = new();
        private readonly Dictionary<uint, NetworkEntityView> _views = new();
        private readonly Stack<NetworkEntityView> _available = new();
        private readonly List<Component> _removed = new();
        private readonly HashSet<uint> _deadThisFrame = new();
        private uint _frame, _nextId, _receivedFrame;
        private int _created;
        private float _nextSend;
        public int VisibleEntities => _views.Count;

        private void Awake()
        {
            if (Session == null || Catalog == null || !Catalog.Initialize() || Windows == null || Snakes == null ||
                Rice == null || EnemyBullets == null || ViewPrefab == null || ViewRoot == null || MaximumViews < 1)
            { Debug.LogError("[NetworkWorld] 网络世界装配不完整。", this); enabled = false; }
        }
        private void OnEnable() { Session.AuthorityMessage += Read; Session.SessionClosed += Clear; Session.SessionOpened += Open; }
        private void OnDisable() { Session.AuthorityMessage -= Read; Session.SessionClosed -= Clear; Session.SessionOpened -= Open; Clear(); }
        private void Open(bool authority) => Clear();

        private void LateUpdate()
        {
            if (!Session.IsAuthority || Session.Phase != SessionPhase.Playing || Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 1f / Session.Config.SnapshotRate; _frame++;
            foreach (var entity in Windows.Instances) if (entity.gameObject.activeInHierarchy) Publish(entity);
            foreach (var entity in Snakes.Instances) if (entity.gameObject.activeInHierarchy) Publish(entity);
            foreach (var entity in Rice.Instances) if (entity.IsRented) Publish(entity);
            foreach (var entity in EnemyBullets.Instances) if (entity.IsRented) Publish(entity);
            _removed.Clear();
            foreach (var pair in _sources) if (pair.Value.Seen != _frame) _removed.Add(pair.Key);
            foreach (var key in _removed)
            {
                uint id = _sources[key].Id;
                Session.SendAuthority(DESPAWN, w => { w.Write(_frame); w.Write(id); }, true);
                _sources.Remove(key);
            }
        }

        private void Publish(Component entity)
        {
            uint generation = entity switch { EnemyActor2D enemy => enemy.SpawnGeneration,
                RiceProjectile rice => rice.SpawnGeneration, EnemyProjectile2D bullet => bullet.SpawnGeneration, _ => 0 };
            if (_sources.TryGetValue(entity, out var previous) && previous.Generation != generation)
            {
                Session.SendAuthority(DESPAWN, w => { w.Write(_frame); w.Write(previous.Id); }, true);
                _sources.Remove(entity);
            }
            if (!_sources.TryGetValue(entity, out var source))
            {
                // 每个租借周期只缓存一次已装配的渲染层，实体ID不复用。
                source = new Source { Id = ++_nextId, Generation = generation,
                    Renderers = entity.GetComponentsInChildren<SpriteRenderer>(true),
                    Group = entity.GetComponent<UnityEngine.Rendering.SortingGroup>() };
                if (source.Renderers.Length > ViewPrefab.Layers.Length)
                { Debug.LogError("[NetworkWorld] 镜像预制体层容量不足。", entity); return; }
                _sources.Add(entity, source);
            }
            source.Seen = _frame;
            Session.SendAuthority(ENTITY, w => { w.Write(_frame); w.Write(source.Id);
                w.Write(entity.transform.position.x); w.Write(entity.transform.position.y); w.Write(entity.transform.position.z);
                bool grouped = source.Group != null && source.Group.enabled; w.Write(grouped);
                w.Write(grouped ? source.Group.sortingLayerID : 0); w.Write(grouped ? source.Group.sortingOrder : 0);
                NetworkEntityView.Write(w, source.Renderers, Catalog); }, false);
        }

        private void Read(byte kind, BinaryReader r)
        {
            if (kind != ENTITY && kind != DESPAWN) return;
            uint frame = r.ReadUInt32(), id = r.ReadUInt32();
            if (kind == DESPAWN)
            {
                if (frame >= _receivedFrame) AdvanceFrame(frame);
                _deadThisFrame.Add(id);
                if (_views.Remove(id, out var old)) { old.Clear(); _available.Push(old); }
                return;
            }
            if (frame < _receivedFrame) return;
            AdvanceFrame(frame);
            if (_deadThisFrame.Contains(id)) return;
            if (!_views.TryGetValue(id, out var view))
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
            _views.Clear(); _sources.Clear(); _deadThisFrame.Clear(); _receivedFrame = _frame = _nextId = 0;
        }
    }
}
