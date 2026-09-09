using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Projectiles;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>刀刃扫掠与剑气各自去重。剑气碰撞体仅作一次查询，不靠触发回调扣血。</summary>
    public sealed class HarnessMeleeDamageExecutor2D : MonoBehaviour
    {
        [SerializeField] private HarnessMeleeConfig _config;
        [SerializeField] private PolygonCollider2D _waveQuery;
        private readonly List<Collider2D> _candidates = new(64);
        private readonly HashSet<IDamageReceiver> _bladeVictims = new();
        private readonly HashSet<IDamageReceiver> _waveVictims = new();
        public event Action<Vector2, float> HitConfirmed;
        public int BladeHits { get; private set; }
        public int WaveHits { get; private set; }
        public int ClearedProjectiles { get; private set; }
        public bool IsConfigured => _config != null && _waveQuery != null;

        private void Awake()
        {
            if (_waveQuery != null) _waveQuery.enabled = false;
        }

        public void BeginSwing()
        {
            _bladeVictims.Clear();
            _waveVictims.Clear();
            BladeHits = WaveHits = 0;
            ClearedProjectiles = 0;
        }

        public void Sweep(HarnessMeleeAttackConfig attack, Vector2 previousOrigin,
            Vector2 origin, float aim, float previousProgress, float progress)
        {
            ContactFilter2D filter = CreateFilter(_config.EnemyLayers.value | _config.ClearableProjectileLayers.value);
            QueryBlade(attack, previousOrigin, aim, previousProgress, filter);
            SweepInterval(attack, previousOrigin, origin, aim, previousProgress, progress, filter, 0);
        }

        private void SweepInterval(HarnessMeleeAttackConfig attack, Vector2 fromOrigin, Vector2 toOrigin,
            float aim, float from, float to, ContactFilter2D filter, int depth)
        {
            float distance = Vector2.Distance(fromOrigin, toOrigin) +
                MeleeSwordGeometry2D.SweepDistanceBound(attack, from, to);
            // 对时间区间递归细分：加速段自动加密，不能用平均速度均匀取几个点。
            if (distance > Mathf.Max(.01f, _config.SweepSampleDistance) && depth < 14)
            {
                float middle = (from + to) * .5f;
                Vector2 middleOrigin = (fromOrigin + toOrigin) * .5f;
                SweepInterval(attack, fromOrigin, middleOrigin, aim, from, middle, filter, depth + 1);
                SweepInterval(attack, middleOrigin, toOrigin, aim, middle, to, filter, depth + 1);
                return;
            }
            QueryBlade(attack, toOrigin, aim, to, filter);
        }

        private void QueryBlade(HarnessMeleeAttackConfig attack, Vector2 origin, float aim, float progress,
            ContactFilter2D filter)
        {
            MeleeSwordGeometry2D.Evaluate(attack, origin, aim, progress, out Vector2 hilt, out Vector2 tip);
            Vector2 direction = tip - hilt;
            float width = attack.BladeWidth * MeleeSwordGeometry2D.SizeAt(attack, progress);
            Physics2D.OverlapCapsule((hilt + tip) * 0.5f,
                new Vector2(direction.magnitude + width, width), CapsuleDirection2D.Horizontal,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, filter, _candidates);
            ApplyDamage(_bladeVictims, attack.BladeDamage, hilt, direction.normalized, false);
            ClearProjectiles();
        }

        public void Burst(HarnessMeleeAttackConfig attack, Vector2 origin, float aim)
        {
            Transform queryTransform = _waveQuery.transform;
            queryTransform.SetPositionAndRotation(MeleeSwordGeometry2D.WavePosition(attack, origin, aim),
                Quaternion.Euler(0, 0, MeleeSwordGeometry2D.WaveAngle(attack, aim)));
            Vector3 parentScale = queryTransform.parent != null ? queryTransform.parent.lossyScale : Vector3.one;
            Vector2 signs = MeleeSwordGeometry2D.WaveSigns(attack, aim);
            queryTransform.localScale = new Vector3(attack.WaveWidth * signs.x / parentScale.x,
                attack.WaveWidth * signs.y / parentScale.y, 1f);
            _waveQuery.SetPath(0, attack.WavePolygon);
            _waveQuery.enabled = true;
            try
            {
                Physics2D.SyncTransforms();
                Physics2D.OverlapCollider(_waveQuery, CreateFilter(_config.EnemyLayers), _candidates);
                ApplyDamage(_waveVictims, attack.WaveDamage, origin,
                    MeleeSwordGeometry2D.Rotate(Vector2.right, aim), true);
                Physics2D.OverlapCollider(_waveQuery, CreateFilter(_config.ClearableProjectileLayers), _candidates);
                ClearProjectiles();
            }
            finally { _waveQuery.enabled = false; }
        }

        private void ApplyDamage(HashSet<IDamageReceiver> victims, float amount,
            Vector2 origin, Vector2 direction, bool wave)
        {
            foreach (Collider2D candidate in _candidates)
            {
                if (candidate == null || (_config.EnemyLayers.value & (1 << candidate.gameObject.layer)) == 0 ||
                    !candidate.TryGetComponent(out DamageHitbox2D hitbox) ||
                    !hitbox.CanReceiveDamage || !hitbox.TryGetReceiver(out IDamageReceiver receiver) ||
                    !victims.Add(receiver)) continue;
                Vector2 point = candidate.ClosestPoint(origin);
                var packet = new DamagePacket(amount, point, direction, gameObject);
                if (!hitbox.TryReceiveDamage(packet)) continue;
                if (wave) WaveHits++; else BladeHits++;
                HitConfirmed?.Invoke(point, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }

        private void ClearProjectiles()
        {
            foreach (Collider2D candidate in _candidates)
            {
                if (candidate == null || (_config.ClearableProjectileLayers.value & (1 << candidate.gameObject.layer)) == 0)
                    continue;
                if (!candidate.TryGetComponent(out EnemyProjectile2D projectile) && candidate.attachedRigidbody != null)
                    candidate.attachedRigidbody.TryGetComponent(out projectile);
                if (projectile != null && projectile.TryClear()) ClearedProjectiles++;
            }
        }

        private static ContactFilter2D CreateFilter(LayerMask layers)
        {
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(layers);
            return filter;
        }
    }
}
