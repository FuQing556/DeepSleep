using System;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.CharacterSelection
{
    /// <summary>
    /// 开局角色选择门：暂停玩法，接收一次选择，并把结果交给现有控制分配器。
    /// </summary>
    public sealed class OpeningCharacterSelectionController : MonoBehaviour
    {
        [SerializeField] private PlayerControlAssignment _controlAssignment;
        [SerializeField] private GameObject _menuRoot;
        [SerializeField] private Button _deepSeekButton;
        [SerializeField] private Button _harnessButton;

        private float _timeScaleBeforeSelection;
        private bool _ownsPause;

        public bool IsSelectionComplete { get; private set; }
        public event Action<PlayerRole> SelectionConfirmed;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(OpeningCharacterSelectionController)}] " +
                    $"开局选角装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _timeScaleBeforeSelection = Time.timeScale;
            Time.timeScale = 0f;
            _ownsPause = true;
            _menuRoot.SetActive(true);
        }

        private void OnEnable()
        {
            if (_deepSeekButton != null)
            {
                _deepSeekButton.onClick.AddListener(SelectDeepSeek);
            }

            if (_harnessButton != null)
            {
                _harnessButton.onClick.AddListener(SelectHarness);
            }
        }

        private void OnDisable()
        {
            if (_deepSeekButton != null)
            {
                _deepSeekButton.onClick.RemoveListener(SelectDeepSeek);
            }

            if (_harnessButton != null)
            {
                _harnessButton.onClick.RemoveListener(SelectHarness);
            }

            RestoreTimeScale();
        }

        private void OnDestroy() => RestoreTimeScale();

        public bool TrySelect(PlayerRole role)
        {
            if (IsSelectionComplete || !_controlAssignment.TrySelectLocalPlayerRole(role))
            {
                return false;
            }

            IsSelectionComplete = true;
            SelectionConfirmed?.Invoke(role);
            RestoreTimeScale();
            _menuRoot.SetActive(false);
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_controlAssignment == null)
            {
                reason = "未配置玩家控制分配器。";
                return false;
            }

            if (_menuRoot == null)
            {
                reason = "未配置选角界面根对象。";
                return false;
            }

            if (_deepSeekButton == null || _harnessButton == null)
            {
                reason = "两个角色按钮必须完整配置。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void SelectDeepSeek() => TrySelect(PlayerRole.DeepSeek);

        private void SelectHarness() => TrySelect(PlayerRole.Harness);

        private void RestoreTimeScale()
        {
            if (!_ownsPause)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforeSelection;
            _ownsPause = false;
        }
    }
}
