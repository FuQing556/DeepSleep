using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>主机在扣血前冻结完整分叉树；各级共用束线几何查询。</summary>
    public static class HarnessLaserChainBuilder
    {
        private sealed class Branch
        {
            public BeamLaneSnapshot Lane;
            public int Depth;
            public HashSet<IDamageReceiver> Ancestors;
        }

        public static BeamFireSnapshot Expand(BeamFireSnapshot source, int level, HarnessTerminalLaserConfig config)
        {
            if (level <= 0) return source;
            var lanes = new List<BeamLaneSnapshot>(config.MaximumChainBeams);
            var queue = new Queue<Branch>();
            for (int i = 0; i < source.LaneCount; i++)
            {
                var lane = source.GetLane(i);
                lanes.Add(lane);
                queue.Enqueue(new Branch { Lane = lane, Ancestors = new HashSet<IDamageReceiver>() });
            }
            var resolver = new BeamHitResolver2D();
            var hits = new List<BeamResolvedHit2D>(32);
            var candidates = new List<Collider2D>(32);
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = source.TargetLayers, useTriggers = true };
            bool capped = false;
            var reserved = new[] { new HashSet<IDamageReceiver>(), new HashSet<IDamageReceiver>() };
            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                if (parent.Depth >= level) continue;
                resolver.Resolve(in parent.Lane, source.TargetLayers, null, hits);
                foreach (var hit in hits)
                {
                    if (!hit.IsValid || !hit.Hitbox.TryGetReceiver(out var emitter) || parent.Ancestors.Contains(emitter)) continue;
                    Vector2 branchOrigin = hit.Hitbox.GetComponent<Collider2D>().bounds.center;
                    var ancestors = new HashSet<IDamageReceiver>(parent.Ancestors) { emitter };
                    float radius = config.ChainRadius(parent.Depth + 1);
                    Physics2D.OverlapCircle(branchOrigin, radius, filter, candidates);
                    float closest = float.PositiveInfinity;
                    Vector2 end = default;
                    IDamageReceiver chosen = null;
                    int chosenId = int.MaxValue;
                    bool found = false;
                    foreach (var c in candidates)
                    {
                        if (c == null || !c.TryGetComponent(out DamageHitbox2D box) || !box.CanReceiveDamage ||
                            !box.TryGetReceiver(out var receiver) || ancestors.Contains(receiver)) continue;
                        Vector2 offset = (Vector2)c.bounds.center - branchOrigin;
                        if (offset.sqrMagnitude < .000001f) continue;
                        float score = offset.sqrMagnitude + (reserved[parent.Depth].Contains(receiver) ? radius * radius * 4f : 0f);
                        int candidateId = c.GetEntityId().GetHashCode();
                        if (score > closest || (Mathf.Approximately(score, closest) && candidateId >= chosenId)) continue;
                        closest = score; end = c.bounds.center; chosen = receiver; chosenId = candidateId; found = true;
                    }
                    if (!found) continue;
                    if (lanes.Count >= config.MaximumChainBeams) { capped = true; continue; }
                    reserved[parent.Depth].Add(chosen);
                    Vector2 direction = (end - branchOrigin).normalized;
                    // 分支覆盖整个配置搜索半径，越过锁定者仍可贯穿。
                    float damage = parent.Lane.PrimaryTargetDamage * config.ChainDamageRatio;
                    var child = new BeamLaneSnapshot(lanes.Count, branchOrigin, direction, radius,
                        parent.Lane.Width, damage, damage, level - parent.Depth, true, emitter);
                    lanes.Add(child);
                    queue.Enqueue(new Branch { Lane = child, Depth = parent.Depth + 1, Ancestors = ancestors });
                }
            }
            if (capped) Debug.LogWarning("[HS连锁] 本次分叉达到配置安全上限，请检查密集场景预算。");
            return new BeamFireSnapshot(source.Sequence, source.SourceOrigin, source.AimDirection, source.TargetLayers, lanes.ToArray());
        }
    }
}
