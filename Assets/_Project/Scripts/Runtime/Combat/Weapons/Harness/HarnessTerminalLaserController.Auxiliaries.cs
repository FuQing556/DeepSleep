using System;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness
{
    public sealed partial class HarnessTerminalLaserController
    {
        // 固定四槽是已确认阵形，不随升级创建运行时组件。
        private sealed class AuxiliaryPort
        {
            public Collider2D Target;
            public DamageHitbox2D Hitbox;
            public Vector2 Aim;
            public float Wait, Progress;
            public int Burst;
            public bool Visible, Charging;
            public Vector2 Position;
            public bool Positioned, Returning;
            public float Hold;
            public void Bind(Collider2D target)
            {
                Release(); Target = target;
                if (target != null && target.TryGetComponent(out Hitbox)) Hitbox.BecameUnavailable += Unavailable;
            }
            private void Unavailable(DamageHitbox2D ignored) { Release(); Charging = false; Burst = 0; }
            public void Release()
            {
                if (Hitbox != null) Hitbox.BecameUnavailable -= Unavailable;
                Hitbox = null; Target = null;
            }
            public void Reset() { Release(); Wait = Progress = Hold = 0; Burst = 0; Visible = Charging = false; Returning = true; }
        }
        private readonly AuxiliaryPort[] _aux = { new(), new(), new(), new() };
        private Predicate<Collider2D> _auxEligibility;
        private int _selectingAux;
        private void InitializeAuxiliaries() => _auxEligibility = IsUnreservedAuxTarget;
        private bool IsUnreservedAuxTarget(Collider2D candidate)
        {
            if (!IsAutomaticCandidate(candidate)) return false;
            for (int i = 0; i < AdditionalPorts; i++)
                if (i != _selectingAux && _aux[i].Target == candidate) return false;
            return true;
        }
        public static Vector2 AuxiliaryPosition(Rect bounds, Vector2 playerPosition, int count, int index, float inset, float margin)
        {
            float half = bounds.height * (.5f - inset);
            bool top = count == 1 || (count == 2 ? index == 0 : index < 2);
            float x = count < 3 || (count == 3 && index == 2) ? 0 : (index % 2 == 0 ? -half : half);
            return new Vector2(Mathf.Clamp(playerPosition.x+x,bounds.xMin+margin,bounds.xMax-margin),
                Mathf.Clamp(playerPosition.y+(top ? half : -half),bounds.yMin+margin,bounds.yMax-margin));
        }
        private Vector2 AuxiliaryHome(int i) => AuxiliaryPosition(_playfield.WorldBounds, transform.position,
            AdditionalPorts, i, _config.AuxiliaryVerticalInset, _config.AuxiliaryEdgeMargin);
        public Vector2 GetAuxiliaryOrigin(int i) => _aux[i].Positioned ? _aux[i].Position : AuxiliaryHome(i);
        public float AuxiliaryBreath(int i) => 1f + _config.AuxiliaryBreathAmount * Mathf.Sin(Time.time * _config.AuxiliaryDriftSpeed + i * Mathf.PI * .5f);
        public void AdvanceAuxiliaryMotion(float dt)
        {
            if (!_isInitialized || _replica) return;
            for(int i=0;i<4;i++)
            {
                var p=_aux[i];
                if(i>=AdditionalPorts){p.Reset();p.Positioned=false;continue;}
                var home=AuxiliaryHome(i);
                if(!p.Positioned){p.Position=home;p.Positioned=true;}
                bool held=p.Charging||p.Burst>0||p.Hold>0;
                p.Hold=Mathf.Max(0,p.Hold-dt);
                if(held){p.Returning=true;continue;}
                float phase=Time.time*_config.AuxiliaryDriftSpeed+i*Mathf.PI*.5f;
                var target=home+new Vector2(Mathf.Cos(phase),Mathf.Sin(phase)) * _config.AuxiliaryDriftRadius;
                var b=_playfield.WorldBounds;float m=_config.AuxiliaryEdgeMargin;
                target=new Vector2(Mathf.Clamp(target.x,b.xMin+m,b.xMax-m),Mathf.Clamp(target.y,b.yMin+m,b.yMax-m));
                float rate=p.Returning ? _config.AuxiliaryReturnRate : _config.AuxiliaryFollowRate;
                p.Position=Vector2.Lerp(p.Position,target,1f-Mathf.Exp(-rate*Mathf.Max(0,dt)));
                if((p.Position-target).sqrMagnitude<.0001f)p.Returning=false;
            }
        }
        public bool GetAuxiliaryView(int i, out Vector2 target, out float progress, out bool charging)
        {
            // 节点与暂停时没有战斗命令，已购炮口仍由升级状态直接显示，不能等待下一次开火模拟。
            var p = _aux[i];
            charging = p.Visible && p.Charging;
            progress = charging ? p.Progress : 0;
            // 无目标时实时读角色朝向，节点不推进战斗 tick 也能转向；客机保留权威瞄准点。
            bool aimed = p.Visible && (_replica || p.Target != null || p.Charging || p.Burst > 0 || p.Hold > 0);
            target = aimed ? p.Aim : GetAuxiliaryOrigin(i) + _facingController.Forward;
            return _isInitialized && i < AdditionalPorts;
        }
        public void ApplyAuxiliaryReplica(int i, bool visible, bool charging, float progress, Vector2 aim, Vector2 origin)
        {
            if (enabled) return;
            var p = _aux[i]; p.Visible = visible; p.Charging = charging; p.Progress = progress; p.Aim = aim;
            p.Position=origin;p.Positioned=visible;
        }
        private void ResetAuxiliaries() { foreach (var p in _aux) p.Reset(); }
        private bool PickAuxiliaryTarget(int i)
        {
            var p = _aux[i];
            if (_manualPreference != null && IsSelectableDamageTarget(_manualPreference)) { p.Bind(_manualPreference); return true; }
            _selectingAux = i;
            Vector2 origin = GetAuxiliaryOrigin(i);
            if (_targetFinder.TryFindNearPoint(origin, origin, _config.MaximumLockDistance, _config.MaximumLockDistance,
                    _auxEligibility, out var target, _priority) ||
                _targetFinder.TryFindNearPoint(origin, origin, _config.MaximumLockDistance, _config.MaximumLockDistance,
                    _automaticEligibility, out target, _priority)) { p.Bind(target); return true; }
            p.Release(); return false;
        }
        private void TickAuxiliaries(float dt)
        {
            for (int i = 0; i < _aux.Length; i++)
            {
                var p = _aux[i];
                if (i >= AdditionalPorts) { p.Reset(); continue; }
                p.Visible = true;
                if (_manualPreference != null && IsSelectableDamageTarget(_manualPreference) && p.Target != _manualPreference)
                    p.Bind(_manualPreference);
                if (p.Target != null && !IsSelectableDamageTarget(p.Target)) { p.Release(); p.Charging = false; p.Burst = 0; }
                p.Wait = Mathf.Max(0, p.Wait - dt);
                if (p.Target != null) p.Aim = p.Target.bounds.center;
                else if (p.Hold <= 0) p.Aim = GetAuxiliaryOrigin(i) + _facingController.Forward;
                if (p.Wait > 0)
                {
                    p.Progress = p.Charging ? 1f - p.Wait / Mathf.Max(.0001f, _config.CalibrationSeconds) : 0;
                    continue;
                }
                if (!p.Charging && p.Burst == 0)
                {
                    if (!PickAuxiliaryTarget(i)) { p.Wait = _config.AutomaticScanInterval; continue; }
                    p.Charging = true; p.Progress = 0; p.Wait = _config.CalibrationSeconds;
                    p.Position=GetAuxiliaryOrigin(i);p.Positioned=true;
                    p.Aim = p.Target.bounds.center; continue;
                }
                if (p.Target == null && !PickAuxiliaryTarget(i)) { p.Reset(); continue; }
                if (p.Charging) { p.Charging = false; p.Burst = 1 + Mathf.FloorToInt(GetUpgradeBonus(UpgradeEffectKind.BurstCount)); }
                Vector2 origin = GetAuxiliaryOrigin(i), aim = p.Target.bounds.center;
                Vector2 direction = aim - origin;
                if (direction.sqrMagnitude < .000001f) direction = _facingController.Forward;
                float damage = (_config.PrimaryTargetDamage + GetUpgradeBonus(UpgradeEffectKind.WeaponDamage)) / _config.PrimaryTargetDamage;
                if (HarnessTerminalLaserSnapshotFactory.TryCreate(_nextFireSequence++, origin, direction, _playfield, _config,
                    damage, GetUpgradeMultiplier(UpgradeEffectKind.BeamWidth), out var snapshot, out var reason, 0, aim, ChainLevel + 1))
                {
                    snapshot = HarnessLaserChainBuilder.Expand(snapshot, ChainLevel, _config);
                    // 先推进周期；伤害事件可在同一栈内令目标死亡并清理此槽。
                    p.Burst--; p.Wait = p.Burst > 0 ? _config.BurstInterval : _config.FireCooldownSeconds / GetUpgradeMultiplier(UpgradeEffectKind.AttackRate);
                    p.Hold=_config.AuxiliaryShotHoldSeconds;
                    var target = p.Hitbox;
                    if (p.Burst == 0) p.Release();
                    FireRequested?.Invoke(new HarnessTerminalLaserFireRequest(gameObject, target, aim, snapshot, true));
                }
                else { Debug.LogError("[HS副炮] " + reason, this); p.Reset(); }
            }
        }
    }
}
