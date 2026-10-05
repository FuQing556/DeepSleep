using System;
using System.Collections;
using System.Collections.Generic;
using DeepSleep.Runtime.Progression.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>唯一允许发起主菜单/玩法场景切换的入口。</summary>
    public sealed class GameSceneRouter : MonoBehaviour
    {
        [SerializeField] private GameLaunchContext _launchContext;
        [SerializeField] private string _bootScene = "Boot";
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _prototypeGameplayScene =
            "Gameplay_Prototype";
        private readonly List<ISceneExitParticipant> _exitParticipants = new();

        public bool IsTransitioning { get; private set; }
        public string LastTransitionError { get; private set; } = string.Empty;
        public int RegisteredSceneExitParticipantCount => _exitParticipants.Count;
        public event Action TransitionCompleted;

        public void RegisterSceneExitParticipant(ISceneExitParticipant participant)
        {
            if (IsAlive(participant) && !_exitParticipants.Contains(participant)) _exitParticipants.Add(participant);
        }

        public void UnregisterSceneExitParticipant(ISceneExitParticipant participant) => _exitParticipants.Remove(participant);

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == _bootScene)
            {
                LoadMainMenu(MainMenuPage.Home);
            }
        }

        public void StartLevel(
            MetaLevelDefinition level,
            GameLaunchMode mode)
        {
            if (IsTransitioning) return;
            if (level == null || !level.Implemented)
            {
                Debug.LogError("[SceneRouter] 关卡不存在或尚未实现。", this);
                return;
            }
            if (!level.TryValidateGameplayDefinition(out string reason))
            {
                LastTransitionError = reason;
                Debug.LogError("[SceneRouter] 关卡定义无效：" + reason, this);
                return;
            }

            string sceneName = level.SceneName;
            BeginTransition(sceneName, () =>
            {
                _launchContext.Prepare(level, mode);
                GameAppRoot.Instance.Achievements.Report(AchievementTriggerIds.JourneyStarted);
            });
        }

        public void StartPrototype(
            MetaLevelDefinition level,
            GameLaunchMode mode) => StartLevel(level, mode);

        public void LoadMainMenu(MainMenuPage page)
        {
            if (IsTransitioning) return;
            BeginTransition(_mainMenuScene, () => _launchContext.SetReturnPage(page));
        }

        private void BeginTransition(string sceneName, Action prepare)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LastTransitionError = "目标场景未加入构建列表：" + sceneName;
                Debug.LogError("[SceneRouter] " + LastTransitionError, this);
                return;
            }
            IsTransitioning = true;
            LastTransitionError = string.Empty;
            Time.timeScale = 0f;
            StartCoroutine(Transition(sceneName, prepare));
        }

        private IEnumerator Transition(string sceneName, Action prepare)
        {
            // 会话在 Awake 显式登记；不隐式搜索/补建 NetworkManager。
            var participants = _exitParticipants.ToArray();
            var roots = new List<GameObject>(participants.Length);
            foreach (var participant in participants)
            {
                if (!IsAlive(participant)) { _exitParticipants.Remove(participant); continue; }
                IEnumerator closing = null;
                Exception failure = null;
                try { closing = participant.PrepareForSceneExit(); }
                catch (Exception ex) { failure = ex; }
                if (failure != null) { FailTransition(failure); yield break; }
                // 显式推进迭代器，才能截获异步 shutdown 超时，而非让 Unity 默默终止协程。
                var pending = new Stack<IEnumerator>();
                if (closing != null) pending.Push(closing);
                while (pending.Count > 0)
                {
                    object next = null;
                    bool more = false;
                    try
                    {
                        more = pending.Peek().MoveNext();
                        if (more) next = pending.Peek().Current;
                        else if (pending.Pop() is IDisposable completed) completed.Dispose();
                    }
                    catch (Exception ex) { failure = ex; }
                    if (failure != null)
                    {
                        while (pending.Count > 0)
                        {
                            try { (pending.Pop() as IDisposable)?.Dispose(); }
                            catch { /* 保留首个退出失败原因。 */ }
                        }
                        FailTransition(failure);
                        yield break;
                    }
                    if (!more) continue;
                    if (next is IEnumerator nested) pending.Push(nested);
                    else { Time.timeScale = 0f; yield return next; }
                }
                if (!IsAlive(participant)) continue;
                GameObject root = null;
                try { root = participant.SceneExitRoot; }
                catch (Exception ex) { failure = ex; }
                if (failure != null) { FailTransition(failure); yield break; }
                if (root != null && !roots.Contains(root)) roots.Add(root);
            }
            foreach (var root in roots)
                if (root != null) Destroy(root);
            // 等销毁回调真正释放 Singleton；原场景 UI 此时已被过渡门控且不再响应业务消息。
            if (roots.Count > 0) yield return null;
            foreach (var root in roots)
            {
                if (root == null) continue;
                FailTransition(new InvalidOperationException("退出对象仍未销毁，取消切场景：" + root.name));
                yield break;
            }
            Exception loadFailure = null;
            AsyncOperation loading = null;
            try
            {
                prepare?.Invoke();
                // 旧场景在异步加载期间仍存在；不能在其会话销毁后恢复模拟。
                Time.timeScale = 0f;
                loading = SceneManager.LoadSceneAsync(sceneName);
            }
            catch (Exception ex) { loadFailure = ex; }
            if (loadFailure != null) { FailTransition(loadFailure); yield break; }
            if (loading == null)
            {
                FailTransition(new InvalidOperationException("场景加载没有返回有效操作：" + sceneName));
                yield break;
            }
            yield return loading;
            IsTransitioning = false;
            // 玩法场景继续由选角门持有暂停；它保存的是选角完成后的目标时间倍率。
            if (sceneName == _mainMenuScene) Time.timeScale = 1f;
            TransitionCompleted?.Invoke();
        }

        private void FailTransition(Exception exception)
        {
            LastTransitionError = exception.ToString();
            Time.timeScale = 0f;
            IsTransitioning = false;
            Debug.LogError("[SceneRouter] 退出清理失败，保留原场景并暂停；未强制加载下一场景。\n" + LastTransitionError, this);
        }

        private static bool IsAlive(ISceneExitParticipant participant) => participant != null &&
            (!(participant is UnityEngine.Object unityObject) || unityObject != null);
    }
}
