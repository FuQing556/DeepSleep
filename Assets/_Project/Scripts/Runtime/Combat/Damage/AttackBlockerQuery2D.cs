using DeepSleep.Runtime.Combat.Beams;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>复用有界缓冲的遮挡查询。层由配置提供，米粒/近战/束线共用同一实体盾。</summary>
    public sealed class AttackBlockerQuery2D
    {
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[1];
        private readonly List<RaycastHit2D> _beamHits = new();
        public bool IsBlocked(Vector2 origin, Vector2 end, LayerMask layers)
        {
            Vector2 offset = end - origin;
            if (layers.value == 0 || offset.sqrMagnitude < .000001f) return false;
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(layers);
            if (Physics2D.Raycast(origin, offset.normalized, filter, _hits, offset.magnitude) == 0) return false;
            if (_hits[0].collider.TryGetComponent<PlayerAttackBlocker2D>(out var blocker)) blocker.NotifyBlocked();
            return true;
        }
        public BeamLaneSnapshot Clip(in BeamLaneSnapshot lane, LayerMask layers, LayerMask damageLayers = default)
        {
            if ((layers.value | damageLayers.value) == 0) return lane;
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(layers.value | damageLayers.value);
            if (Physics2D.BoxCast(lane.Origin, new Vector2(.001f, lane.Width), lane.RotationDegrees,
                lane.Direction, filter, _beamHits, lane.Length) == 0) return lane;
            float distance = lane.Length;
            Collider2D nearest = null;
            bool damageable = false;
            foreach (var hit in _beamHits)
            {
                bool wall = (layers.value & (1 << hit.collider.gameObject.layer)) != 0;
                bool stop = hit.collider.TryGetComponent<DamageHitbox2D>(out var hitbox) && hitbox.StopsPiercingBeams;
                if ((!wall && !stop) || hit.distance >= distance) continue;
                distance = hit.distance; nearest = hit.collider; damageable = stop;
            }
            if (nearest == null) return lane;
            if (nearest.TryGetComponent<PlayerAttackBlocker2D>(out var blocker)) blocker.NotifyBlocked();
            // 穿入一个查询容差，确保截束端的OverlapBox仍包含可受伤球罩。
            if (damageable) distance = Mathf.Min(lane.Length, distance + .001f);
            return new BeamLaneSnapshot(lane.LaneIndex, lane.Origin, lane.Direction,
                Mathf.Max(.001f, distance), lane.Width, lane.PrimaryTargetDamage,
                lane.PiercingDamage, lane.VisualLayers, lane.IsBranch, lane.BranchSource);
        }
    }
}
