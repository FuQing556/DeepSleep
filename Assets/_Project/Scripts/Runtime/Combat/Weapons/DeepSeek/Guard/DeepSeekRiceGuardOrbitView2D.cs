using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Players.Health;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard
{
    /// <summary>显示六碗护航；只消费玩法事件，不参与伤害裁决。</summary>
    public sealed class DeepSeekRiceGuardOrbitView2D : MonoBehaviour
    {
        private enum BowlVisualState { Hidden, Intact, Breaking, Ending }

        [SerializeField] private DeepSeekRiceGuardController _controller;
        [SerializeField] private SpriteRenderer[] _bowls;
        [SerializeField] private Sprite _intactSprite;
        [SerializeField] private Sprite _brokenSprite;
        [Header("轨道")]
        [SerializeField, Min(0f)] private float _orbitRadius = 1.45f;
        [SerializeField] private float _orbitDegreesPerSecond = 28f;
        [SerializeField] private float _selfRotationDegreesPerSecond = 70f;
        [SerializeField, Min(1f)] private float _redistributionDegreesPerSecond = 300f;
        [Header("显示")]
        [SerializeField, Min(0.01f)] private float _bowlScale = 0.2f;
        [SerializeField, Min(0.01f)] private float _brokenBowlScaleMultiplier = 0.88f;
        [SerializeField, Min(0f)] private float _breakHoldSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float _breakFadeSeconds = 0.4f;
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _warningPulsesPerSecond = 4f;
        [SerializeField, Range(0f, 1f)] private float _warningMinimumAlpha = 0.45f;
        [SerializeField] private int _backSortingOrder = -10;
        [SerializeField] private int _frontSortingOrder = 10;

        private BowlVisualState[] _states;
        private float[] _phaseOffsets;
        private float[] _alphas;
        private float[] _breakElapsedSeconds;
        private float _orbitPhase;
        private bool _isEnding;

        public float OrbitRadius => _orbitRadius;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError($"[{nameof(DeepSeekRiceGuardOrbitView2D)}] 护航表现装配无效：{reason}", this);
                enabled = false;
                return;
            }
            int count = _bowls.Length;
            _states = new BowlVisualState[count];
            _phaseOffsets = new float[count];
            _alphas = new float[count];
            _breakElapsedSeconds = new float[count];
            HideImmediately();
        }

        private void OnEnable()
        {
            if (_controller == null) return;
            _controller.Activated += OnActivated;
            _controller.ChargeConsumed += OnChargeConsumed;
            _controller.Ended += OnEnded;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Activated -= OnActivated;
                _controller.ChargeConsumed -= OnChargeConsumed;
                _controller.Ended -= OnEnded;
            }
            if (_states != null) HideImmediately();
        }

        private void LateUpdate()
        {
            if (_states == null || Time.deltaTime <= 0f) return;
            _orbitPhase = Mathf.Repeat(_orbitPhase + _orbitDegreesPerSecond * Time.deltaTime, 360f);
            UpdateTargetsAndRender(Time.deltaTime);
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_controller == null) { reason = "未配置护航控制器。"; return false; }
            if (_bowls == null || _bowls.Length != _controller.Capacity)
            { reason = $"米饭视图数量必须等于护航容量 {_controller.Capacity}。"; return false; }
            for (int index = 0; index < _bowls.Length; index++)
                if (_bowls[index] == null) { reason = $"第 {index} 个米饭视图为空。"; return false; }
            if (_intactSprite == null || _brokenSprite == null)
            { reason = "完整米饭或破碎米饭贴图未配置。"; return false; }
            reason = string.Empty;
            return true;
        }

        private void OnActivated()
        {
            _isEnding = false;
            int count = _bowls.Length;
            for (int index = 0; index < count; index++)
            {
                _states[index] = BowlVisualState.Intact;
                _phaseOffsets[index] = index * 360f / count;
                _alphas[index] = 1f;
                _breakElapsedSeconds[index] = 0f;
                _bowls[index].sprite = _intactSprite;
                _bowls[index].enabled = true;
            }
        }

        private void OnChargeConsumed(PlayerDamageReceiver2D target, DamagePacket damage, int remainingCharges)
        {
            int index = FindNearestIntactBowl(damage.HitPoint);
            if (index < 0) return;
            _states[index] = BowlVisualState.Breaking;
            _alphas[index] = 1f;
            _breakElapsedSeconds[index] = 0f;
            _bowls[index].sprite = _brokenSprite;
        }

        private void OnEnded()
        {
            _isEnding = true;
            for (int index = 0; index < _states.Length; index++)
                if (_states[index] == BowlVisualState.Intact)
                    _states[index] = BowlVisualState.Ending;
        }

        private int FindNearestIntactBowl(Vector2 hitPoint)
        {
            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < _bowls.Length; index++)
            {
                if (_states[index] != BowlVisualState.Intact) continue;
                float distance = ((Vector2)_bowls[index].transform.position - hitPoint).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = index;
            }
            return bestIndex;
        }

        private void UpdateTargetsAndRender(float deltaTime)
        {
            int intactCount = 0;
            bool hasBreakingBowl = false;
            for (int index = 0; index < _states.Length; index++)
            {
                if (_states[index] == BowlVisualState.Intact) intactCount++;
                if (_states[index] == BowlVisualState.Breaking) hasBreakingBowl = true;
            }

            int rank = 0;
            float warningAlpha = GetWarningAlpha();
            for (int index = 0; index < _bowls.Length; index++)
            {
                switch (_states[index])
                {
                    case BowlVisualState.Intact:
                        // 破碎碗淡出期间冻结队形；全部淡出后才开始重新均分。
                        if (!hasBreakingBowl && !_isEnding && intactCount > 0)
                        {
                            float target = rank * 360f / intactCount;
                            _phaseOffsets[index] = Mathf.MoveTowardsAngle(
                                _phaseOffsets[index], target,
                                _redistributionDegreesPerSecond * deltaTime);
                        }
                        _alphas[index] = warningAlpha;
                        rank++;
                        break;
                    case BowlVisualState.Breaking:
                        UpdateBreakingBowl(index, deltaTime);
                        break;
                    case BowlVisualState.Ending:
                        Fade(index, deltaTime, _fadeOutSeconds);
                        break;
                }
                RenderBowl(index);
            }
            if (_isEnding && !AnyBowlVisible()) _isEnding = false;
        }

        private void UpdateBreakingBowl(int index, float deltaTime)
        {
            if (_breakElapsedSeconds[index] < _breakHoldSeconds)
            {
                _breakElapsedSeconds[index] = Mathf.Min(
                    _breakElapsedSeconds[index] + deltaTime,
                    _breakHoldSeconds);
                _alphas[index] = 1f;
                return;
            }

            Fade(index, deltaTime, _breakFadeSeconds);
        }

        private void Fade(int index, float deltaTime, float duration)
        {
            _alphas[index] = Mathf.MoveTowards(_alphas[index], 0f, deltaTime / duration);
            if (_alphas[index] <= 0f) _states[index] = BowlVisualState.Hidden;
        }

        private void RenderBowl(int index)
        {
            SpriteRenderer bowl = _bowls[index];
            float angle = _orbitPhase + _phaseOffsets[index];
            float radians = angle * Mathf.Deg2Rad;
            Vector3 position = new Vector3(Mathf.Cos(radians) * _orbitRadius, Mathf.Sin(radians) * _orbitRadius, 0f);
            bowl.transform.localPosition = position;
            bowl.transform.localRotation = Quaternion.Euler(0f, 0f,
                angle + index * 60f + Time.time * _selfRotationDegreesPerSecond * (index % 2 == 0 ? 1f : -1f));
            float spriteScale = bowl.sprite == _brokenSprite ? _brokenBowlScaleMultiplier : 1f;
            bowl.transform.localScale = Vector3.one * _bowlScale * spriteScale * Mathf.SmoothStep(0f, 1f, _alphas[index]);
            Color color = bowl.color;
            color.a = _alphas[index];
            bowl.color = color;
            bowl.sortingOrder = position.y >= 0f ? _backSortingOrder : _frontSortingOrder;
            bowl.enabled = _alphas[index] > 0f;
        }

        private float GetWarningAlpha()
        {
            if (!_controller.IsWarning || _warningPulsesPerSecond <= 0f) return 1f;
            float wave = (Mathf.Sin(Time.time * Mathf.PI * 2f * _warningPulsesPerSecond) + 1f) * 0.5f;
            return Mathf.Lerp(_warningMinimumAlpha, 1f, wave);
        }

        private bool AnyBowlVisible()
        {
            for (int index = 0; index < _alphas.Length; index++)
                if (_alphas[index] > 0f) return true;
            return false;
        }

        private void HideImmediately()
        {
            for (int index = 0; index < _bowls.Length; index++)
            {
                _states[index] = BowlVisualState.Hidden;
                _alphas[index] = 0f;
                _breakElapsedSeconds[index] = 0f;
                _bowls[index].sprite = _intactSprite;
                _bowls[index].enabled = false;
            }
            _isEnding = false;
        }
    }
}
