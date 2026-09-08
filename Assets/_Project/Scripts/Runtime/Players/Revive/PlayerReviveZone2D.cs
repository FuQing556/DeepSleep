using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Revive
{
    /// <summary>
    /// 维护倒地玩家复活范围内的有效救援者。
    /// 本组件只负责空间关系，不累计进度，也不恢复生命。
    /// </summary>
    public sealed class PlayerReviveZone2D : MonoBehaviour
    {
        private sealed class OverlapRecord
        {
            public PlayerActor Actor;
            public PlayerLifeStateController2D LifeState;
            public int ColliderCount;
            public bool IsEligible;
        }

        [SerializeField] private CircleCollider2D _trigger;
        [SerializeField] private Rigidbody2D _sensorBody;
        [SerializeField] private PlayerActor _owner;
        [SerializeField] private PlayerLifeStateController2D _ownerLifeState;

        private readonly Dictionary<PlayerActor, OverlapRecord> _overlaps =
            new Dictionary<PlayerActor, OverlapRecord>();

        private bool _isInitialized;

        public event Action<PlayerActor> EligibleRescuerEntered;
        public event Action<PlayerActor> EligibleRescuerExited;

        public int EligibleRescuerCount { get; private set; }

        public bool IsSensing =>
            _trigger != null && _trigger.isActiveAndEnabled;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerReviveZone2D)}] " +
                    $"复活范围装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _ownerLifeState.StateChanged += OnOwnerLifeStateChanged;
            SynchronizeSensingState();
        }

        private void OnDisable()
        {
            if (_ownerLifeState != null)
            {
                _ownerLifeState.StateChanged -= OnOwnerLifeStateChanged;
            }

            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            ClearOverlaps();
        }

        public bool TryGetFirstEligibleRescuer(out PlayerActor rescuer)
        {
            foreach (OverlapRecord record in _overlaps.Values)
            {
                if (record.IsEligible)
                {
                    rescuer = record.Actor;
                    return true;
                }
            }

            rescuer = null;
            return false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsSensing ||
                !TryResolveCandidate(other, out PlayerActor actor,
                    out PlayerLifeStateController2D lifeState))
            {
                return;
            }

            if (_overlaps.TryGetValue(actor, out OverlapRecord record))
            {
                record.ColliderCount++;
                return;
            }

            record = new OverlapRecord
            {
                Actor = actor,
                LifeState = lifeState,
                ColliderCount = 1,
                IsEligible = lifeState.State == PlayerLifeState.Alive,
            };
            _overlaps.Add(actor, record);
            lifeState.StateChanged += OnCandidateLifeStateChanged;

            if (record.IsEligible)
            {
                EligibleRescuerCount++;
                EligibleRescuerEntered?.Invoke(actor);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerActor actor = other != null
                ? other.GetComponentInParent<PlayerActor>()
                : null;

            if (actor == null ||
                !_overlaps.TryGetValue(actor, out OverlapRecord record))
            {
                return;
            }

            record.ColliderCount--;

            if (record.ColliderCount > 0)
            {
                return;
            }

            RemoveRecord(record);
        }

        private bool TryResolveCandidate(
            Collider2D other,
            out PlayerActor actor,
            out PlayerLifeStateController2D lifeState)
        {
            actor = other != null
                ? other.GetComponentInParent<PlayerActor>()
                : null;
            lifeState = actor != null
                ? actor.GetComponent<PlayerLifeStateController2D>()
                : null;

            return actor != null && actor != _owner && lifeState != null;
        }

        private void OnOwnerLifeStateChanged(
            PlayerLifeStateController2D controller,
            PlayerLifeState state)
        {
            SynchronizeSensingState();
        }

        private void OnCandidateLifeStateChanged(
            PlayerLifeStateController2D controller,
            PlayerLifeState state)
        {
            PlayerActor actor = controller.GetComponent<PlayerActor>();

            if (actor == null ||
                !_overlaps.TryGetValue(actor, out OverlapRecord record))
            {
                return;
            }

            bool shouldBeEligible = state == PlayerLifeState.Alive;

            if (record.IsEligible == shouldBeEligible)
            {
                return;
            }

            record.IsEligible = shouldBeEligible;

            if (shouldBeEligible)
            {
                EligibleRescuerCount++;
                EligibleRescuerEntered?.Invoke(actor);
            }
            else
            {
                EligibleRescuerCount--;
                EligibleRescuerExited?.Invoke(actor);
            }
        }

        private void SynchronizeSensingState()
        {
            bool shouldSense =
                _ownerLifeState.State == PlayerLifeState.Downed;

            if (!shouldSense)
            {
                ClearOverlaps();
            }

            _trigger.enabled = shouldSense;
        }

        private void ClearOverlaps()
        {
            if (_overlaps.Count == 0)
            {
                EligibleRescuerCount = 0;
                return;
            }

            var records = new List<OverlapRecord>(_overlaps.Values);

            for (int index = 0; index < records.Count; index++)
            {
                RemoveRecord(records[index]);
            }

            EligibleRescuerCount = 0;
        }

        private void RemoveRecord(OverlapRecord record)
        {
            _overlaps.Remove(record.Actor);
            record.LifeState.StateChanged -= OnCandidateLifeStateChanged;

            if (!record.IsEligible)
            {
                return;
            }

            EligibleRescuerCount--;
            EligibleRescuerExited?.Invoke(record.Actor);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_trigger == null || _sensorBody == null || _owner == null ||
                _ownerLifeState == null)
            {
                reason = "圆形触发器、传感器刚体、玩家身份和生命状态必须全部配置。";
                return false;
            }

            if (!_trigger.isTrigger)
            {
                reason = "复活范围碰撞体必须启用 Is Trigger。";
                return false;
            }

            if (_trigger.transform != transform ||
                _sensorBody.transform != transform ||
                _trigger.attachedRigidbody != _sensorBody)
            {
                reason = "脚本、圆形触发器和独立传感器刚体必须位于同一对象。";
                return false;
            }

            if (_sensorBody.bodyType != RigidbodyType2D.Kinematic)
            {
                reason = "复活范围传感器刚体必须使用 Kinematic。";
                return false;
            }

            if (_owner.transform == transform ||
                !transform.IsChildOf(_owner.transform) ||
                _ownerLifeState.gameObject != _owner.gameObject)
            {
                reason = "复活范围必须位于对应玩家根节点之下。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
