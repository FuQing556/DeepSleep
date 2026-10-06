using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.DamageNumbers
{
    /// <summary>
    /// 汇总玩家武器的成功命中事实，用一个屏幕空间 Canvas 池化显示伤害数字。
    /// 不读取生命状态，也不参与伤害计算。
    /// </summary>
    public sealed class CombatDamageNumberPresenter2D : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private RiceProjectilePool _riceProjectilePool;
        [SerializeField]
        private HarnessTerminalLaserDamageExecutor2D _laserDamage;
        [SerializeField] private HarnessMeleeDamageExecutor2D _meleeDamage;

        [Header("View")]
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _canvasRect;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private DamageNumberEntryView _entryPrefab;
        [SerializeField] private DamageNumberStyleConfig _deepSeekStyle;
        [SerializeField] private DamageNumberStyleConfig _harnessStyle;
        [SerializeField, Min(1)] private int _prewarmCount = 64;
        [SerializeField, Min(1)] private int _maximumCount = 96;

        private readonly Stack<DamageNumberEntryView> _available = new();
        private readonly List<DamageNumberEntryView> _all = new();
        private System.Random _visualRandom;
        private bool _isInitialized;
        private bool _reportedExhaustion;
        public uint RequestedCount { get; private set; }
        public uint PlayedCount { get; private set; }
        public uint CapacityDropCount { get; private set; }
        public uint DisabledDropCount { get; private set; }
        public uint InvalidDropCount { get; private set; }
        public int ActiveCount => _all.Count - _available.Count;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(CombatDamageNumberPresenter2D)}] " +
                    $"伤害数字装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _visualRandom = new System.Random();
            for (int index = 0; index < _prewarmCount; index++)
            {
                _available.Push(CreateEntry());
            }

            _isInitialized = true;
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            _riceProjectilePool.HitConfirmed += OnRiceHit;
            _laserDamage.HitConfirmed += OnLaserHit;
            _meleeDamage.DamageConfirmed += OnMeleeHit;
        }

        private void OnDisable()
        {
            if (_isInitialized)
            {
                _riceProjectilePool.HitConfirmed -= OnRiceHit;
                _laserDamage.HitConfirmed -= OnLaserHit;
                _meleeDamage.DamageConfirmed -= OnMeleeHit;
            }

            _available.Clear();
            for (int index = 0; index < _all.Count; index++)
            {
                _all[index].PrepareForPool();
                _available.Push(_all[index]);
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_riceProjectilePool == null || _laserDamage == null ||
                _meleeDamage == null)
            {
                reason = "玩家武器命中来源未全部配置。";
                return false;
            }

            if (_worldCamera == null || _canvas == null ||
                _canvasRect == null || _poolRoot == null)
            {
                reason = "相机或跳字 Canvas 引用不完整。";
                return false;
            }

            if (_entryPrefab == null)
            {
                reason = "未配置跳字预制体。";
                return false;
            }

            if (!_entryPrefab.TryValidateConfiguration(out reason))
            {
                reason = $"跳字预制体无效：{reason}";
                return false;
            }

            if (_deepSeekStyle == null ||
                !_deepSeekStyle.TryValidate(out reason))
            {
                reason = $"DS 跳字配置无效：{reason}";
                return false;
            }

            if (_harnessStyle == null ||
                !_harnessStyle.TryValidate(out reason))
            {
                reason = $"HS 跳字配置无效：{reason}";
                return false;
            }

            if (_prewarmCount < 1 || _maximumCount < _prewarmCount)
            {
                reason = "跳字池容量无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void OnRiceHit(RiceProjectileHitConfirmed hit)
            => TryShow(hit.HitPoint, hit.DamageAmount, _deepSeekStyle);

        private void OnLaserHit(HarnessTerminalLaserHitConfirmed hit)
            => TryShow(hit.HitPoint, hit.DamageAmount, _harnessStyle);

        private void OnMeleeHit(HarnessMeleeDamageHitConfirmed hit)
            => TryShow(hit.HitPoint, hit.DamageAmount, _harnessStyle);

        /// <summary>只播放权威命中跳字，不在镜像端重新结算伤害。</summary>
        public void ShowReplicaDamage(Vector2 position, float amount, bool harness)
        {
            TryShow(position, amount, harness ? _harnessStyle : _deepSeekStyle);
        }

        private void TryShow(
            Vector2 worldPosition,
            float amount,
            DamageNumberStyleConfig style)
        {
            RequestedCount++;
            if (!_isInitialized || !isActiveAndEnabled) { DisabledDropCount++; return; }
            if (style == null || amount <= 0f || style.DisplayScale <= 0f)
            {
                InvalidDropCount++;
                return;
            }

            DamageNumberEntryView entry;
            if (_available.Count > 0)
            {
                entry = _available.Pop();
            }
            else if (_all.Count < _maximumCount)
            {
                entry = CreateEntry();
            }
            else
            {
                CapacityDropCount++;
                if (!_reportedExhaustion)
                {
                    Debug.LogWarning(
                        "[DamageNumbers] 跳字池已满，本次只省略表现。",
                        this);
                    _reportedExhaustion = true;
                }
                return;
            }

            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            entry.Play(
                worldPosition,
                amount,
                style,
                _canvasRect,
                _worldCamera,
                uiCamera,
                (float)_visualRandom.NextDouble(),
                (float)_visualRandom.NextDouble());
            PlayedCount++;
        }

        private DamageNumberEntryView CreateEntry()
        {
            DamageNumberEntryView entry = Instantiate(
                _entryPrefab, _poolRoot);
            entry.name = $"DamageNumber_Pooled_{_all.Count:00}";
            entry.Finished += ReturnToPool;
            entry.PrepareForPool();
            _all.Add(entry);
            return entry;
        }

        private void ReturnToPool(DamageNumberEntryView entry)
        {
            entry.PrepareForPool();
            _available.Push(entry);
            _reportedExhaustion = false;
        }
    }
}
