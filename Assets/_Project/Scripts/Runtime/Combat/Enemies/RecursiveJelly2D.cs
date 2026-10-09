using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Targeting;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.World.Playfield;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Enemies
{
    /// <summary>递归特有的追逐、分裂与果冻显示。沿用 EnemyActor 的生命、池、攻击和网络快照。</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RecursiveJelly2D : EnemyMotor2D
    {
        public EnemyActor2D Actor;
        public HealthComponent Health;
        public HealthConfig BaseHealth;
        public Rigidbody2D Body;
        public Collider2D Shape;
        public DamageHitbox2D Hitbox;
        public SpriteRenderer Renderer;
        public CombatPlayfieldConfig Playfield;
        public RecursiveJellyConfig Config;
        private EnemyActorPool2D _children;
        private CoopSessionController _session;
        private PlayerCombatTarget2D _target;
        private Vector2 _direction, _fallback, _launch, _strideDirection;
        private Vector3 _baseScale, _basePosition;
        private Quaternion _baseRotation;
        private float _retarget, _separationAge, _birthAge, _hitAge, _hitAxis, _strideAge;
        private float _inheritedHealthMultiplier;
        private bool _running, _entered, _separating, _bound, _valid;

        public override bool IsRunning => _running;
        public override Vector2 TravelDirection => _direction;
        public bool IsBirthProtected => _birthAge < Config.BirthProtectionSeconds;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[RecursiveJelly] " + reason, this); enabled = false; return; }
            _baseScale = Renderer.transform.localScale;
            _basePosition = Renderer.transform.localPosition;
            _baseRotation = Renderer.transform.localRotation;
            Actor.DespawnRequested += OnDespawn;
            _valid = true;
        }

        private void OnEnable()
        {
            if (_valid) Health.DamageAccepted += OnHit;
        }

        private void OnDisable()
        {
            if (!_valid) return;
            Health.DamageAccepted -= OnHit;
            Stop();
            RestoreVisual();
        }

        private void OnDestroy()
        {
            if (Actor != null) Actor.DespawnRequested -= OnDespawn;
        }

        /// <summary>池创建时显式注入；客机只消费世界镜像，不独立分裂。</summary>
        public void Bind(EnemyActorPool2D children, CoopSessionController session)
        { _children = children; _session = session; _bound = true; }

        public override void Begin(Vector2 position, in EnemySpawnVariation2D variation)
        {
            _fallback = _direction = variation.TravelDirection;
            _target = null; _retarget = 0; _running = true;
            _entered = Playfield.WorldBounds.Contains(position);
            _separating = false; _separationAge = 0; _strideAge = 0; _strideDirection = _direction;
            _inheritedHealthMultiplier = 0;
            _birthAge = Config.BirthProtectionSeconds; _hitAge = Config.HitRecoverSeconds;
            // 出生立即对齐显示与物理位置；不能等下一物理刻才把新生显示从池原点移过来。
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Body.position = position; Body.rotation = 0;
            Shape.enabled = true; Hitbox.enabled = true;
            RestoreVisual();
        }

        public void BeginSeparation(Vector2 direction, float healthMultiplier)
        {
            _inheritedHealthMultiplier = healthMultiplier;
            _launch = direction.normalized;
            _separating = true; _separationAge = 0; _birthAge = 0;
            Shape.enabled = !IsBirthProtected; Hitbox.enabled = !IsBirthProtected;
        }

        public override void PrepareSimulation(float dt)
        {
            if (!_running || dt <= 0) return;
            _retarget -= dt;
            if (_retarget <= 0 || _target == null || !_target.IsTargetable)
            { PlayerCombatTarget2D.TryFindNearest(Body.position, out _target); _retarget = Config.RetargetSeconds; }
            Vector2 offset = _target != null && _target.IsTargetable ? _target.Position - Body.position : _fallback;
            if (offset.sqrMagnitude > .0001f) _direction = offset.normalized;
        }

        public override void Simulate(float dt)
        {
            if (!_running || dt <= 0) return;
            Vector2 movement;
            if (_separating)
            {
                _separationAge = Mathf.Min(Config.SeparationSeconds, _separationAge + dt);
                float progress = _separationAge / Config.SeparationSeconds;
                movement = (_launch * (Config.SeparationSpeed * (1 - progress)) + _direction * Config.Speed * progress) * dt;
                if (progress >= 1) { _separating = false; _strideAge = 0; }
            }
            else movement = AdvanceStride(dt);
            Vector2 next = Body.position + movement;
            // 后代只在逻辑区内分离，避免边缘击杀直接把新生怪弹出战区。
            if (_separating)
            {
                Rect area = Playfield.WorldBounds;
                next = new Vector2(Mathf.Clamp(next.x, area.xMin, area.xMax), Mathf.Clamp(next.y, area.yMin, area.yMax));
            }
            Body.MovePosition(next);
            Rect bounds = Playfield.WorldBounds;
            _entered |= bounds.Contains(next);
            float margin = Config.DespawnMargin;
            if (_entered && (next.x < bounds.xMin - margin || next.x > bounds.xMax + margin ||
                next.y < bounds.yMin - margin || next.y > bounds.yMax + margin)) NotifyExitedPlayfield();
        }

        private Vector2 AdvanceStride(float dt)
        {
            Vector2 movement = Vector2.zero;
            // 跨周期的时间也按同一曲线积分；一整步内锁住方向，不边蠕动边甩头。
            while (dt > 0)
            {
                if (_strideAge == 0) _strideDirection = _direction;
                float slice = Mathf.Min(dt, Config.StrideSeconds - _strideAge);
                float before = StrideProgress(_strideAge / Config.StrideSeconds);
                _strideAge += slice;
                float after = StrideProgress(_strideAge / Config.StrideSeconds);
                movement += _strideDirection * (Config.StrideDistance * (after - before));
                dt -= slice;
                if (_strideAge >= Config.StrideSeconds) _strideAge = 0;
            }
            return movement;
        }

        private float StrideProgress(float phase)
            => Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - Config.ReachPhase01) / Config.PullPhase01));

        public override void Stop()
        {
            _running = false; _target = null; _separating = false;
            if (Body != null) { Body.linearVelocity = Vector2.zero; Body.angularVelocity = 0; }
        }

        private void OnHit(DamagePacket damage)
        {
            _hitAge = 0;
            _hitAxis = Mathf.Abs(damage.Direction.x) >= Mathf.Abs(damage.Direction.y) ? -1 : 1;
        }

        private void LateUpdate() => AdvanceVisual(Time.deltaTime);

        private void AdvanceVisual(float dt)
        {
            if (!_running) return;
            _hitAge = Mathf.Min(Config.HitRecoverSeconds, _hitAge + dt);
            _birthAge += dt;
            if (!IsBirthProtected && !Shape.enabled) { Shape.enabled = true; Hitbox.enabled = true; }
            float hit01 = _hitAge / Config.HitRecoverSeconds;
            float hit = _hitAxis * Config.HitSquash * Mathf.Cos(hit01 * 2 * Mathf.PI) * (1 - hit01) * (1 - hit01);
            float birth01 = Mathf.Clamp01(_birthAge / Config.SeparationSeconds);
            float birth = Config.BirthSquash * Mathf.Cos(birth01 * 2 * Mathf.PI) * (1 - birth01) * (1 - birth01);
            float phase = _strideAge / Config.StrideSeconds;
            float reach = phase < Config.ReachPhase01 ? Mathf.SmoothStep(0, 1, phase / Config.ReachPhase01) : 1 - StrideProgress(phase);
            if (_separating) reach = 0;
            float moving = Mathf.Abs(_strideDirection.x) - Mathf.Abs(_strideDirection.y);
            float deformation = Mathf.Clamp(Config.MoveStretch * moving * reach + hit + birth, -.25f, .25f);
            // 互逆缩放保持近似面积；这里只改变单张身体 Sprite，Collider 和物理根始终不缩放。
            Renderer.transform.localScale = Vector3.Scale(_baseScale, new Vector3(1 + deformation, 1 / (1 + deformation), 1));
            // 前伸时显示中心前移半个伸出距离，后部留在原处；根在回收阶段才迈一步。
            Vector2 frontOffset = _strideDirection * (Config.StrideDistance * .5f * reach);
            Vector3 localOffset = Renderer.transform.parent.InverseTransformVector(new Vector3(frontOffset.x, frontOffset.y, 0));
            Renderer.transform.localPosition = _basePosition + localOffset;
            Renderer.transform.localRotation = _baseRotation;
        }

        private void RestoreVisual()
        {
            Renderer.transform.localScale = _baseScale; Renderer.transform.localPosition = _basePosition;
            Renderer.transform.localRotation = _baseRotation;
            Renderer.color = new Color(1, 1, 1, Config.Opacity);
        }

        private void OnDespawn(EnemyActor2D actor, EnemyDespawnRequest2D request)
        {
            if (request.Reason != EnemyDespawnReason.Defeated || Config.SplitDirections.Length == 0) return;
            if (!_bound || _children == null || _session == null)
            { Debug.LogError("[RecursiveJelly] 分裂后代池或权威会话未显式注入。", this); return; }
            if (_session.Phase != SessionPhase.Offline && !_session.IsAuthority) return;
            float multiplier = _inheritedHealthMultiplier > 0 ? _inheritedHealthMultiplier : Health.MaximumHealth / BaseHealth.MaximumHealth;
            Rect bounds = Playfield.WorldBounds;
            foreach (Vector2 raw in Config.SplitDirections)
            {
                Vector2 direction = raw.normalized;
                Vector2 position = request.EffectPosition + direction * Config.SplitOffset;
                position = new Vector2(Mathf.Clamp(position.x, bounds.xMin, bounds.xMax), Mathf.Clamp(position.y, bounds.yMin, bounds.yMax));
                var variation = new EnemySpawnVariation2D(direction, Config.Speed, 0, 0);
                if (!_children.TryRent(position, in variation, out EnemyActor2D child))
                { Debug.LogError("[RecursiveJelly] 后代容量不足，检查生成容量约束。", this); return; }
                // 新生边界缓存专属组件，之后每帧不查组件；倍率继承本段，不继承父体当前血量。
                var jelly = child.GetComponent<RecursiveJelly2D>();
                child.Health.SetMaximumHealthBonus(jelly.BaseHealth.MaximumHealth * multiplier - jelly.BaseHealth.MaximumHealth);
                child.Health.ResetToMaximum();
                jelly.BeginSeparation(direction, multiplier);
            }
        }

        public override bool TryValidateConfiguration(out string reason)
        {
            if (Actor == null || Health == null || BaseHealth == null || Body == null ||
                Body.bodyType != RigidbodyType2D.Kinematic || Shape == null || !Shape.isTrigger || Hitbox == null ||
                Renderer == null || Renderer.transform == transform || Playfield == null || Config == null)
            { reason = "递归须显式配置生命、Kinematic 主体、触发器、受击框、独立视觉、战区和参数。"; return false; }
            return Config.TryValidate(out reason) && Playfield.TryValidate(out reason) && BaseHealth.TryValidate(out reason);
        }
    }
}
