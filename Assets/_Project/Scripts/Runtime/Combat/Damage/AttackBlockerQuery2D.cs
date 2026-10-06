using DeepSleep.Runtime.Combat.Beams;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Damage
{
    /// <summary>复用有界缓冲的遮挡查询。层由配置提供，米粒/近战/束线共用同一实体盾。</summary>
    public sealed class AttackBlockerQuery2D
    {
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[1];
        public bool IsBlocked(Vector2 origin, Vector2 end, LayerMask layers)
        {
            Vector2 offset = end - origin;
            if (layers.value == 0 || offset.sqrMagnitude < .000001f) return false;
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(layers);
            if (Physics2D.Raycast(origin, offset.normalized, filter, _hits, offset.magnitude) == 0) return false;
            if (_hits[0].collider.TryGetComponent<PlayerAttackBlocker2D>(out var blocker)) blocker.NotifyBlocked();
            return true;
        }
        public BeamLaneSnapshot Clip(in BeamLaneSnapshot lane, LayerMask layers)
        {
            if (layers.value == 0) return lane;
            var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(layers);
            if (Physics2D.BoxCast(lane.Origin, new Vector2(.001f, lane.Width), lane.RotationDegrees,
                lane.Direction, filter, _hits, lane.Length) == 0) return lane;
            if (_hits[0].collider.TryGetComponent<PlayerAttackBlocker2D>(out var blocker)) blocker.NotifyBlocked();
            return new BeamLaneSnapshot(lane.LaneIndex, lane.Origin, lane.Direction,
                Mathf.Max(.001f, _hits[0].distance), lane.Width, lane.PrimaryTargetDamage,
                lane.PiercingDamage, lane.VisualLayers, lane.IsBranch, lane.BranchSource);
        }
    }
}
