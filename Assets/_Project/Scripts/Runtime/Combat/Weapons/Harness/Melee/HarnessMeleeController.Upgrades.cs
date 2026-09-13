using System.Collections.Generic;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    public sealed partial class HarnessMeleeController
    {
        private readonly Dictionary<HarnessMeleeAttackConfig, HarnessMeleeAttackConfig[]> _scaledAttacks = new();
        private readonly List<PendingEcho> _pendingEchoes = new(8);
        private struct PendingEcho
        {
            public HarnessMeleeAttackConfig Attack;
            public Vector2 Origin;
            public float Aim, Remaining;
        }
        public float RangeScale => _config.BaseRangeScale * (1f + Bonus(UpgradeEffectKind.ChainLevel) * _config.RangePerChainRank);
        public float SpeedScale => 1f + Bonus(UpgradeEffectKind.BurstCount) * _config.SpeedPerBurstRank;
        private float Bonus(UpgradeEffectKind kind) => _laser.Upgrades != null
            ? _laser.Upgrades.GetAdditiveValue(PlayerRole.Harness, kind) : 0f;

        public HarnessMeleeAttackConfig PrepareAttack(HarnessMeleeAttackConfig source, int echo = 0,
            float range = -1f, float speed = -1f)
        {
            if (source == null) return null;
            if (!_scaledAttacks.TryGetValue(source, out var variants))
            {
                variants = new HarnessMeleeAttackConfig[3];
                for (int i = 0; i < variants.Length; i++)
                {
                    variants[i] = Instantiate(source);
                    variants[i].hideFlags = HideFlags.DontSave;
                }
                _scaledAttacks.Add(source, variants);
            }
            var a = variants[Mathf.Clamp(echo, 0, 2)];
            float scale = range > 0f ? range : RangeScale;
            float rate = speed > 0f ? speed : SpeedScale;
            a.RuntimeTimeScale = rate; a.RuntimeRangeScale = scale; a.RuntimeEchoIndex = echo;
            // 只改变武器几何，角色CharacterScale/Offset原样保留。
            a.SwingOffset = source.SwingOffset * scale;
            for (int i = 0; i < a.MotionKeys.Length; i++)
            {
                var key = source.MotionKeys[i]; key.Hilt *= scale; a.MotionKeys[i] = key;
            }
            a.SwordLength = source.SwordLength * scale;
            a.BladeWidth = source.BladeWidth * scale;
            a.WaveWidth = source.WaveWidth * scale;
            a.WaveOffset = source.WaveOffset * scale;
            a.WaveRotation = source.WaveRotation;
            a.SwingSeconds = source.SwingSeconds / rate;
            a.RecoverySeconds = source.RecoverySeconds / rate;
            a.FollowThroughSeconds = source.FollowThroughSeconds / rate;
            a.FollowThroughDrift = source.FollowThroughDrift * scale;
            float bonus = Bonus(UpgradeEffectKind.WeaponDamage);
            a.BladeDamage = source.BladeDamage + Mathf.Floor(bonus * .5f);
            a.WaveDamage = source.WaveDamage + bonus;
            if (echo > 0)
            {
                float angle = source.EchoAngleStep * echo;
                a.WaveOffset = MeleeSwordGeometry2D.Rotate(a.WaveOffset, angle) +
                    Vector2.right * (a.SwordLength * _config.EchoOffsetSwordFraction * echo);
                a.WaveRotation += angle;
                a.WaveDamage *= _config.EchoDamageRatio;
            }
            return a;
        }

        private void QueueEchoes(HarnessMeleeAttackConfig source, Vector2 origin, float aim)
        {
            int count = Mathf.Clamp(Mathf.FloorToInt(Bonus(UpgradeEffectKind.ProjectileCount)), 0, 2);
            for (int i = 1; i <= count; i++)
                _pendingEchoes.Add(new PendingEcho { Attack = PrepareAttack(source, i), Origin = origin,
                    Aim = aim, Remaining = _config.EchoInterval * i / SpeedScale });
        }

        private void AdvanceEchoes(float dt)
        {
            for (int i = 0; i < _pendingEchoes.Count;)
            {
                var echo = _pendingEchoes[i]; echo.Remaining -= dt;
                if (echo.Remaining > 0f) { _pendingEchoes[i] = echo; i++; continue; }
                _pendingEchoes.RemoveAt(i);
                _damage.Burst(echo.Attack, echo.Origin, echo.Aim, false);
                WaveRequested?.Invoke(echo.Attack, echo.Origin, echo.Aim);
            }
        }
        private void OnDestroy()
        {
            foreach (var variants in _scaledAttacks.Values)
                foreach (var variant in variants) if (variant != null) Destroy(variant);
            _scaledAttacks.Clear();
        }
    }
}
