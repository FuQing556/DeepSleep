using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    /// <summary>
    /// 消费终端激光的冻结快照，并在开火瞬间完成一次伤害结算。
    /// 不决定瞄准、冷却或视觉保持时间。
    /// </summary>
    public sealed class HarnessTerminalLaserDamageExecutor2D : MonoBehaviour
    {
        [SerializeField] private HarnessTerminalLaserController _controller;

        public event Action<HarnessTerminalLaserHitConfirmed> HitConfirmed;

        private readonly BeamHitResolver2D _resolver = new();
        private readonly List<BeamResolvedHit2D> _resolvedHits = new();
        private bool _isInitialized;

        private void Awake()
        {
            if (_controller == null)
            {
                Debug.LogError(
                    $"[{nameof(HarnessTerminalLaserDamageExecutor2D)}] " +
                    "未配置终端激光控制器。",
                    this);
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                _controller.FireRequested += OnFireRequested;
            }
        }

        private void OnDisable()
        {
            if (_isInitialized && _controller != null)
            {
                _controller.FireRequested -= OnFireRequested;
            }
        }

        private void OnFireRequested(
            HarnessTerminalLaserFireRequest request)
        {
            if (request == null || !request.IsValid)
            {
                return;
            }

            request.PrimaryTarget.TryGetReceiver(
                out IDamageReceiver primaryReceiver);

            BeamFireSnapshot snapshot = request.BeamSnapshot;

            for (int laneIndex = 0;
                 laneIndex < snapshot.LaneCount;
                 laneIndex++)
            {
                BeamLaneSnapshot lane = snapshot.GetLane(laneIndex);
                _resolver.Resolve(
                    in lane,
                    snapshot.TargetLayers,
                    primaryReceiver,
                    _resolvedHits);

                ApplyLaneDamage(request.Source, in lane);
            }
        }

        private void ApplyLaneDamage(
            GameObject source,
            in BeamLaneSnapshot lane)
        {
            for (int index = 0; index < _resolvedHits.Count; index++)
            {
                BeamResolvedHit2D hit = _resolvedHits[index];

                if (!hit.IsValid)
                {
                    continue;
                }

                float amount = hit.IsPrimaryTarget
                    ? lane.PrimaryTargetDamage
                    : lane.PiercingDamage;
                DamagePacket damage = new DamagePacket(
                    amount,
                    hit.HitPoint,
                    lane.Direction,
                    source);

                if (!hit.Hitbox.TryReceiveDamage(in damage))
                {
                    continue;
                }

                HitConfirmed?.Invoke(
                    new HarnessTerminalLaserHitConfirmed(
                        lane.LaneIndex,
                        hit.Hitbox,
                        hit.HitPoint,
                        lane.Direction,
                        lane.Width,
                        amount,
                        hit.IsPrimaryTarget));
            }
        }
    }
}
