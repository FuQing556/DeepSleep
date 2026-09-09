using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.Harness.Melee
{
    /// <summary>取得角色姿态的明确所有权；剑独立于人物贴图，三个动作不拉伸变形。</summary>
    public sealed class HarnessMeleePresenter2D : MonoBehaviour
    {
        [SerializeField] private HarnessMeleeController _controller;
        [SerializeField] private HarnessMeleeDamageExecutor2D _damage;
        [SerializeField] private HarnessTerminalLaserPresenter _laserView;
        [SerializeField] private HarnessLaserHitEffectPresenter2D _hitEffects;
        [SerializeField] private PlayerFacingController2D _facing;
        [SerializeField] private SpriteRenderer _character, _ghostRenderer, _sword;
        [Tooltip("预先装配的剑气视图池；不向运行中的场景临时添加组件。")]
        [SerializeField] private MeleeWaveView2D[] _waves;
        private readonly SpritePoseGhost2D _ghost = new();
        private Vector3 _baseScale, _basePosition;
        private float _visualSwingElapsed;
        private float _followThroughElapsed = float.PositiveInfinity;
        private HarnessMeleeConfig Config => _controller.Config;

        private void OnEnable()
        {
            if (_controller == null || _damage == null || _laserView == null || _hitEffects == null ||
                _facing == null || _character == null || _ghostRenderer == null || _sword == null ||
                _waves == null || _waves.Length == 0)
            {
                Debug.LogError("[HarnessMelee] 近战表现引用不完整。", this);
                enabled = false;
                return;
            }
            _controller.ModeChanged += OnModeChanged;
            _controller.SwingStarted += OnSwingStarted;
            _controller.SwingFinished += OnSwingFinished;
            _controller.WaveRequested += OnWave;
            _damage.HitConfirmed += OnHit;
            _sword.enabled = false;
            foreach (var wave in _waves) wave.Clear();
        }

        private void LateUpdate() => AdvancePresentation(Time.deltaTime);

        private void AdvancePresentation(float deltaTime)
        {
            _ghost.Tick(deltaTime);
            if (!_controller.IsMelee) return;
            _sword.enabled = true;
            _sword.sprite = Config.SwordSprite;
            _sword.color = Color.white;
            if (_controller.IsSwinging)
            {
                // 独立的连续表现时钟，消除固定刻在短缩放阶段的跳帧。
                _visualSwingElapsed += deltaTime;
                float visualProgress = Mathf.Clamp01(_visualSwingElapsed / _controller.Attack.SwingSeconds);
                ApplyPoseLayout(_controller.Attack.CharacterScale, _controller.Attack.CharacterOffset);
                MeleeSwordGeometry2D.Evaluate(_controller.Attack, _controller.GripPosition,
                    _controller.AimDegrees, visualProgress, out Vector2 hilt, out Vector2 tip);
                Vector2 direction = tip - hilt;
                SetSword(hilt, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, direction.magnitude);
            }
            else
            {
                ApplyPoseLayout(Config.IdlePoseScale, Config.IdlePoseOffset);
                if (DrawFollowThrough(deltaTime)) return;
                Vector2 offset = Config.IdleSwordOffset;
                offset.x *= _facing.Forward.x;
                offset.y += Mathf.Sin(Time.time * Mathf.PI * 2 * Config.FloatFrequency) * Config.FloatAmplitude;
                SetSword(_controller.GripPosition + offset, Config.IdleSwordAngle, Config.IdleSwordLength);
            }
        }

        private void OnModeChanged(bool active)
        {
            _followThroughElapsed = float.PositiveInfinity;
            if (active)
            {
                _baseScale = _character.transform.localScale;
                _basePosition = _character.transform.localPosition;
                _laserView.SetPoseOverride(true);
            }
            if (!active)
            {
                ChangePose(Config.RangedPose, 1f, Vector2.zero);
                _sword.enabled = false;
                _laserView.SetPoseOverride(false);
                _facing.ResetToInitialDirection();
            }
        }

        private void OnSwingStarted()
        {
            _visualSwingElapsed = 0f;
            _followThroughElapsed = float.PositiveInfinity;
            ChangePose(_controller.Attack.CharacterPose,
                _controller.Attack.CharacterScale, _controller.Attack.CharacterOffset);
            _sword.sprite = Config.SwordSprite;
            _sword.enabled = true;
            _sword.color = Color.white;
            MeleeSwordGeometry2D.Evaluate(_controller.Attack, _controller.GripPosition,
                _controller.AimDegrees, 0f, out Vector2 hilt, out Vector2 tip);
            Vector2 direction = tip - hilt;
            SetSword(hilt, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, direction.magnitude);
        }
        private void OnSwingFinished()
        {
            _followThroughElapsed = 0;
            ChangePose(Config.IdlePose, Config.IdlePoseScale, Config.IdlePoseOffset);
        }

        private bool DrawFollowThrough(float deltaTime)
        {
            var attack = _controller.Attack;
            if (attack == null || attack.FollowThroughSeconds <= 0 ||
                _followThroughElapsed >= attack.FollowThroughSeconds) return false;
            float t = Mathf.Clamp01(_followThroughElapsed / attack.FollowThroughSeconds);
            _followThroughElapsed += deltaTime;
            MeleeSwordGeometry2D.Evaluate(attack, _controller.GripPosition,
                _controller.AimDegrees, 1, out Vector2 hilt, out Vector2 tip);
            Vector2 blade = tip - hilt;
            hilt += MeleeSwordGeometry2D.AimVector(attack.FollowThroughDrift * t, _controller.AimDegrees);
            float fade = Mathf.SmoothStep(0, 1, t);
            SetSword(hilt, Mathf.Atan2(blade.y, blade.x) * Mathf.Rad2Deg,
                blade.magnitude * Mathf.Lerp(1, attack.FollowThroughEndScale, fade));
            _sword.color = new Color(1, 1, 1, 1 - fade);
            return true;
        }

        private void ChangePose(Sprite sprite, float scale, Vector2 offset)
        {
            if (_character.sprite == sprite) return;
            _ghost.Capture(_character, _ghostRenderer, Config.GhostAlpha, Config.GhostSeconds);
            _character.sprite = sprite;
            ApplyPoseLayout(scale, offset);
        }

        private void ApplyPoseLayout(float scale, Vector2 offset)
        {
            _character.transform.localScale = new Vector3(_baseScale.x * scale, _baseScale.y * scale, _baseScale.z);
            _character.transform.localPosition = _basePosition + (Vector3)offset;
        }

        private void SetSword(Vector2 hilt, float angle, float gripToTipLength)
        {
            // PNG 中握柄不是最左端：按导入 Pivot 到真实剑尖的跨度换算整图宽度。
            float fraction = Config.SwordTipNormalizedX - _sword.sprite.pivot.x / _sword.sprite.rect.width;
            MeleeWaveView2D.SetWorldSprite(_sword, hilt, angle, gripToTipLength / Mathf.Max(.01f, fraction));
        }

        private void OnWave(HarnessMeleeAttackConfig attack, Vector2 origin, float aim)
        {
            foreach (MeleeWaveView2D wave in _waves)
                if (!wave.IsPlaying) { wave.Play(attack, origin, aim, Config.WaveFadeSeconds); return; }
            Debug.LogWarning("[HarnessMelee] 剑气视图池耗尽，请增加预装配槽位。伤害已正常结算。", this);
        }

        private void OnHit(Vector2 point, float angle)
            => _hitEffects.PlayImpact(point, MeleeSwordGeometry2D.Rotate(Vector2.right, angle), 0.15f);

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.ModeChanged -= OnModeChanged;
            _controller.SwingStarted -= OnSwingStarted;
            _controller.SwingFinished -= OnSwingFinished;
            _controller.WaveRequested -= OnWave;
            if (_damage != null) _damage.HitConfirmed -= OnHit;
            _ghost.Clear();
            _followThroughElapsed = float.PositiveInfinity;
            if (_sword != null) _sword.enabled = false;
            if (_laserView != null) _laserView.SetPoseOverride(false);
            if (_waves != null) foreach (var wave in _waves) if (wave != null) wave.Clear();
        }
    }
}
