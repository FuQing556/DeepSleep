using System;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Common;
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
        [SerializeField] private ChapterRunController _chapterRun;

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

            // Router 在异步换场景期间持有全局暂停；不能把过渡用的 0 当作开战倍率。
            _timeScaleBeforeSelection = SceneTransitioning ? 1f : Time.timeScale;
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
            if (SceneTransitioning || IsSelectionComplete)
            {
                return false;
            }
            if (!TryValidateLevelStart(out string reason))
            {
                Debug.LogError($"[{nameof(OpeningCharacterSelectionController)}] 开战被阻止：{reason}", this);
                return false;
            }
            if (!_controlAssignment.TrySelectLocalPlayerRole(role)) return false;

            IsSelectionComplete = true;
            SelectionConfirmed?.Invoke(role);
            RestoreTimeScale();
            _menuRoot.SetActive(false);
            return true;
        }

        /// <summary>单人选角与联机开战共用的只读门禁；失败不会解除选角暂停。</summary>
        public bool TryValidateLevelStart(out string reason)
        {
            if (_chapterRun == null || _chapterRun.gameObject.scene != gameObject.scene)
            {
                reason = "_chapterRun 为空或不属于本关场景，请显式完成关卡装配。";
                return false;
            }
            return _chapterRun.TryValidateLevelStart(out reason);
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

        private void SelectDeepSeek() => SelectLocally(PlayerRole.DeepSeek);

        private void SelectHarness() => SelectLocally(PlayerRole.Harness);

        private void SelectLocally(PlayerRole role)
        {
            // 公用 TrySelect 也会接收联机自动分配；只有真人按钮成功才保存界面偏好。
            if (TrySelect(role)) UiThemePreferences.SetFromLocalSelection(role);
        }

        private void RestoreTimeScale()
        {
            if (!_ownsPause)
            {
                return;
            }

            _ownsPause = false;
            // 旧场景卸载时不能盖掉 Router 的暂停，重新启动没有会话的旧战斗。
            if (!SceneTransitioning) Time.timeScale = _timeScaleBeforeSelection;
        }

        private static bool SceneTransitioning => GameAppRoot.Instance != null &&
            GameAppRoot.Instance.SceneRouter.IsTransitioning;
    }
}
