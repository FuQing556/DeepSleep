using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    public enum ChapterRunPhase : byte
    {
        WaitingForSelection,
        Combat,
        Node,
        Defeat,
        Complete
    }

    public enum ChapterFailureReason : byte
    {
        None,
        DeepSeekLost,
        HarnessLost,
        TeamDowned,
        ObjectiveIncomplete
    }

    /// <summary>
    /// 章节宏观流程的唯一权威：倒计时、任务、失败和节点检查点。
    /// 敌人、节点、生命与强化仍由各自系统保存具体状态。
    /// </summary>
    public sealed class ChapterRunController : MonoBehaviour
    {
        private const byte NetworkRunState = NetworkMessageCatalog.Authority.ChapterState;
        private const float NetworkBroadcastInterval = 0.2f;

        [SerializeField] private LevelSceneBindings _levelBindings;
        // 仅保留为 Editor 装配派生值，禁止成为第二套可编辑关卡定义。
        [SerializeField, HideInInspector] private ChapterRunConfig _config;
        [SerializeField] private RestNodePrototypeController2D _restNode;
        [SerializeField] private ChapterCombatWorld2D _combatWorld;
        [SerializeField] private RestNodeUpgradeController _upgradeController;
        [SerializeField] private OpeningCharacterSelectionController _selection;
        [SerializeField] private CoopSessionController _session;
        [SerializeField] private PlayerLifeStateController2D _deepSeekLife;
        [SerializeField] private PlayerLifeStateController2D _harnessLife;
        [Tooltip("可选：指定战斗段还必须完成的机制遭遇。")]
        [SerializeField] private MonoBehaviour[] _additionalObjectiveComponents;
        [SerializeField] private ChapterRunHudView _hud;
        [SerializeField] private LocalPlayerProfileStore _profile;
        [SerializeField, HideInInspector] private MetaLevelDefinition _level;

        private ChapterRunConfig _runConfig;
        private MetaLevelDefinition _runLevel;
        private string _runLevelId;
        private IReadOnlyList<LevelEnemySceneBinding> _enemies;

        private float _remainingCombatSeconds;
        private float _deepSeekDownedSeconds;
        private float _harnessDownedSeconds;
        private float _teamDownedSeconds;
        private float _defeatElapsed;
        private float _networkElapsed;
        private int _defeats;
        private int _totalDefeats;
        private float _totalCombatSeconds;
        private int _segmentNumber = 1;
        private bool _hasRestNodeCheckpoint;
        private bool _retryCurrentSegmentFromCheckpoint;
        private bool _isInitialized;
        private bool _metaRewardGranted;
        private int _awardedVouchers;
        private bool _wasFirstClear;
        private string _rewardMessage;
        private bool _anyPlayerDowned;
        private bool _checkpointRestoreAttempted;
        private bool _restoringCheckpoint;
        private bool _sceneExitStarted;
        private string _checkpointRestoreError;
        private PlayerLifeCheckpoint _deepSeekCheckpoint;
        private PlayerLifeCheckpoint _harnessCheckpoint;
        private IChapterCombatObjective[] _additionalObjectives;
        private Action<EnemyDespawnRequest2D>[] _enemyDespawnHandlers;

        private ChapterRunPhase _phase = ChapterRunPhase.WaitingForSelection;
        public event Action<ChapterRunPhase> PhaseChanged;
        public ChapterRunPhase Phase
        {
            get => _phase;
            private set
            {
                if (_phase == value) return;
                _phase = value;
                PhaseChanged?.Invoke(value);
            }
        }
        public ChapterFailureReason FailureReason { get; private set; }
        public int SegmentNumber => _segmentNumber;
        public int Defeats => _defeats;
        public float RemainingCombatSeconds => _remainingCombatSeconds;
        /// <summary>仅表示显式配置已通过且本局引用已捕获；不另建开战状态。</summary>
        public bool IsInitialized => _isInitialized;
        /// <summary>本关显式选角门，用于验证持久网络根仍属于当前 Gameplay 场景。</summary>
        public OpeningCharacterSelectionController Selection => _selection;
        public LevelSceneBindings LevelBindings => _levelBindings;
        public ChapterCombatWorld2D CombatWorld => _combatWorld;
        public float CurrentEnemyHealthMultiplier => CurrentSegment.EnemyHealthMultiplier;

        private ChapterCombatSegmentDefinition CurrentSegment =>
            _runConfig.GetSegment(_segmentNumber);

        public void CompleteObjectiveAndExpireForDevelopment()
        {
            if (!Debug.isDebugBuild || Phase != ChapterRunPhase.Combat)
            {
                return;
            }
            _totalDefeats += Mathf.Max(0, CurrentSegment.RequiredDefeats - _defeats);
            _defeats = CurrentSegment.RequiredDefeats;
            _remainingCombatSeconds = 0f;
        }

        public void CompletePrototypeForDevelopment()
        {
            if (!Debug.isDebugBuild || Phase != ChapterRunPhase.Combat)
            {
                return;
            }

            _segmentNumber = _runConfig.CombatSegmentCount;
            CompleteObjectiveAndExpireForDevelopment();
        }

        public void FailObjectiveForDevelopment()
        {
            if (!Debug.isDebugBuild || Phase != ChapterRunPhase.Combat)
            {
                return;
            }
            _defeats = Mathf.Min(_defeats, CurrentSegment.RequiredDefeats - 1);
            _remainingCombatSeconds = 0f;
        }

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;
        private bool CanAuthor => _session == null || _session.Phase == SessionPhase.Offline ||
            _session.IsAuthority;

        private void Awake()
        {
            if (_profile == null && GameAppRoot.Instance != null)
            {
                _profile = GameAppRoot.Instance.Profile;
            }
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError($"[{nameof(ChapterRunController)}] {reason}", this);
                enabled = false;
                return;
            }

            _runLevel = _levelBindings.Level;
            _runConfig = _runLevel.ChapterRunConfig;
            _runLevelId = _runLevel.LevelId;
            _enemies = _levelBindings.Enemies;
            _isInitialized = true;
            int objectiveCount = _additionalObjectiveComponents?.Length ?? 0;
            _additionalObjectives = new IChapterCombatObjective[objectiveCount];
            for (int index = 0; index < objectiveCount; index++)
                _additionalObjectives[index] =
                    (IChapterCombatObjective)_additionalObjectiveComponents[index];
            _remainingCombatSeconds = _runConfig.GetSegment(1).DurationSeconds;
            Render();
        }

        private void OnEnable()
        {
            if (_selection != null)
            {
                _selection.SelectionConfirmed += OnSelectionConfirmed;
            }
            if (_restNode != null)
            {
                _restNode.StateChanged += OnRestNodeStateChanged;
            }
            if (_session != null)
            {
                _session.AuthorityMessage += ReadAuthorityState;
                _session.PeerJoined += BroadcastState;
                _session.PlayingStarted += RefreshReplicaFlow;
                _session.SceneExitStarted += OnSceneExitStarted;
            }
            if (_hud != null)
            {
                _hud.ReturnRequested += ReturnToOpening;
            }
            SubscribeEnemyPools(true);
        }

        private void OnDisable()
        {
            if (_selection != null)
            {
                _selection.SelectionConfirmed -= OnSelectionConfirmed;
            }
            if (_restNode != null)
            {
                _restNode.StateChanged -= OnRestNodeStateChanged;
            }
            if (_session != null)
            {
                _session.AuthorityMessage -= ReadAuthorityState;
                _session.PeerJoined -= BroadcastState;
                _session.PlayingStarted -= RefreshReplicaFlow;
                _session.SceneExitStarted -= OnSceneExitStarted;
            }
            if (_hud != null)
            {
                _hud.ReturnRequested -= ReturnToOpening;
            }
            SubscribeEnemyPools(false);
        }

        private void Start()
        {
            if (!_isInitialized) return;
            if (CanAuthor)
            {
                _upgradeController.CaptureProgressCheckpoint();
                CapturePlayerCheckpoint();
                if (_selection.IsSelectionComplete) StartCombatSegment();
            }
            else RefreshReplicaFlow();
        }

        private void Update()
        {
            if (!_isInitialized || _sceneExitStarted)
            {
                return;
            }

            if (CanAuthor)
            {
                if (Phase == ChapterRunPhase.Combat)
                {
                    SimulateCombat(Time.deltaTime);
                }
                else if (Phase == ChapterRunPhase.Defeat)
                {
                    SimulateDefeat(Time.deltaTime);
                }

                if (IsOnline)
                {
                    _networkElapsed += Time.unscaledDeltaTime;
                    if (_networkElapsed >= NetworkBroadcastInterval)
                    {
                        _networkElapsed = 0f;
                        BroadcastState();
                    }
                }
            }

            Render();
        }

        private void SimulateCombat(float deltaTime)
        {
            if (deltaTime <= 0f ||
                (_restNode.State != RestNodeState.Combat && _restNode.State != RestNodeState.Clearing))
            {
                return;
            }

            UpdateDownedTimers(deltaTime);
            if (TryResolveLifeFailure())
            {
                return;
            }

            _totalCombatSeconds += deltaTime;
            if (_restNode.State == RestNodeState.Clearing)
            {
                // 清残敌仍是战斗：受击、倒地失败、击败奖励继续生效，段倒计时不再重跑。
                if (_combatWorld.IsCleared)
                {
                    _combatWorld.StopCombat(ChapterCombatStopReason.NaturalClear);
                    if (_restNode.CompleteClearing())
                    {
                        Phase = ChapterRunPhase.Node;
                        BroadcastState();
                    }
                }
                return;
            }

            if (CurrentSegment.ObjectiveMode == ChapterObjectiveMode.EncountersOnly)
                _defeats = AreAdditionalObjectivesComplete() ? 1 : 0;

            _remainingCombatSeconds = Mathf.Max(
                0f,
                _remainingCombatSeconds - deltaTime);
            if (_remainingCombatSeconds > 0f)
            {
                return;
            }

            if (_defeats < CurrentSegment.RequiredDefeats ||
                !AreAdditionalObjectivesComplete())
            {
                BeginDefeat(ChapterFailureReason.ObjectiveIncomplete);
            }
            else if (_segmentNumber >= _runConfig.CombatSegmentCount)
            {
                CompleteChapter();
            }
            else _restNode.BeginNodeTransition();
        }

        private void UpdateDownedTimers(float deltaTime)
        {
            bool deepSeekDowned =
                _deepSeekLife.State == PlayerLifeState.Downed;
            bool harnessDowned =
                _harnessLife.State == PlayerLifeState.Downed;
            _anyPlayerDowned |= deepSeekDowned || harnessDowned;
            _deepSeekDownedSeconds = deepSeekDowned
                ? _deepSeekDownedSeconds + deltaTime
                : 0f;
            _harnessDownedSeconds = harnessDowned
                ? _harnessDownedSeconds + deltaTime
                : 0f;
            _teamDownedSeconds = deepSeekDowned && harnessDowned
                ? _teamDownedSeconds + deltaTime
                : 0f;
        }

        private bool TryResolveLifeFailure()
        {
            if (_teamDownedSeconds >= _runConfig.TeamDownedTimeoutSeconds)
            {
                BeginDefeat(ChapterFailureReason.TeamDowned);
                return true;
            }
            if (_deepSeekDownedSeconds >= _runConfig.SingleDownedTimeoutSeconds)
            {
                BeginDefeat(ChapterFailureReason.DeepSeekLost);
                return true;
            }
            if (_harnessDownedSeconds >= _runConfig.SingleDownedTimeoutSeconds)
            {
                BeginDefeat(ChapterFailureReason.HarnessLost);
                return true;
            }
            return false;
        }

        private void BeginDefeat(ChapterFailureReason reason)
        {
            if (Phase != ChapterRunPhase.Combat)
            {
                return;
            }

            FailureReason = reason;
            Phase = ChapterRunPhase.Defeat;
            _defeatElapsed = 0f;
            _checkpointRestoreAttempted = false;
            _checkpointRestoreError = null;
            _combatWorld.StopCombat(ChapterCombatStopReason.Failure);
            _restNode.SuspendCombatForFailure();
            BroadcastState();
        }

        private void SimulateDefeat(float deltaTime)
        {
            if (_checkpointRestoreAttempted) return;
            _defeatElapsed += Mathf.Max(0f, deltaTime);
            if (_defeatElapsed < _runConfig.DefeatPresentationSeconds)
            {
                return;
            }

            _checkpointRestoreAttempted = true;
            if (!TryValidateCheckpointRestore(out _checkpointRestoreError))
            {
                Debug.LogError($"[{nameof(ChapterRunController)}] 检查点恢复已停止：{_checkpointRestoreError}", this);
                return;
            }
            // 整组只读校验通过后才提交；升级先还原最大生命，再恢复玩家精确生命/位置。
            _restoringCheckpoint = true;
            try
            {
                if (!_upgradeController.RestoreProgressCheckpoint(_hasRestNodeCheckpoint))
                {
                    _checkpointRestoreError = "强化检查点在提交前失效。";
                    Debug.LogError($"[{nameof(ChapterRunController)}] {_checkpointRestoreError}", this);
                    return;
                }
                RestorePlayersFromCheckpoint();
                if (_hasRestNodeCheckpoint)
                {
                    _retryCurrentSegmentFromCheckpoint = true;
                    _restNode.RestoreCheckpointNode();
                    Phase = ChapterRunPhase.Node;
                }
                else
                {
                    _restNode.RestartCombatFromCheckpoint();
                    StartCombatSegment();
                }
            }
            finally { _restoringCheckpoint = false; }
            BroadcastState();
        }

        private void OnSelectionConfirmed(PlayerRole role)
        {
            if (CanAuthor && !_sceneExitStarted && Phase == ChapterRunPhase.WaitingForSelection)
            {
                StartCombatSegment();
            }
        }

        private void OnRestNodeStateChanged(RestNodeState state)
        {
            if (!CanAuthor)
            {
                RefreshReplicaFlow();
                return;
            }
            if (!_isInitialized || _sceneExitStarted || _restoringCheckpoint) return;

            if (state == RestNodeState.Clearing && Phase == ChapterRunPhase.Combat)
            {
                _combatWorld.StopSpawning();
            }
            else if (state == RestNodeState.Revealing && Phase == ChapterRunPhase.Combat)
            {
                Phase = ChapterRunPhase.Node;
            }
            else if (state == RestNodeState.Open && Phase == ChapterRunPhase.Node)
            {
                _upgradeController.BeginNode();
                ApplyRestNodeRecovery();
            }
            else if (state == RestNodeState.Departing && Phase == ChapterRunPhase.Node)
            {
                _upgradeController.EndNode();
                CapturePlayerCheckpoint();
                _hasRestNodeCheckpoint = true;
            }
            else if (state == RestNodeState.Combat &&
                     Phase == ChapterRunPhase.Node)
            {
                if (_retryCurrentSegmentFromCheckpoint)
                {
                    _retryCurrentSegmentFromCheckpoint = false;
                }
                else
                {
                    _segmentNumber++;
                }
                StartCombatSegment();
            }
            BroadcastState();
        }

        private void ApplyRestNodeRecovery()
        {
            RestNodeRecoveryMode mode = _runConfig.RestNodeRecovery;
            if (mode == RestNodeRecoveryMode.Disabled)
            {
                return;
            }

            bool full = mode == RestNodeRecoveryMode.FullRestore;
            _deepSeekLife.RestoreAtCheckpoint(
                _deepSeekLife.transform.position,
                full,
                _runConfig.ReviveOnlyHealthFraction,
                _runConfig.CheckpointInvulnerabilitySeconds);
            _harnessLife.RestoreAtCheckpoint(
                _harnessLife.transform.position,
                full,
                _runConfig.ReviveOnlyHealthFraction,
                _runConfig.CheckpointInvulnerabilitySeconds);
        }

        private void StartCombatSegment()
        {
            if (!CanAuthor || _sceneExitStarted) return;
            ApplyCurrentSegmentTuning();
            Phase = ChapterRunPhase.Combat;
            FailureReason = ChapterFailureReason.None;
            _remainingCombatSeconds = CurrentSegment.DurationSeconds;
            _defeats = 0;
            _deepSeekDownedSeconds = 0f;
            _harnessDownedSeconds = 0f;
            _teamDownedSeconds = 0f;
            _defeatElapsed = 0f;
            _checkpointRestoreAttempted = false;
            _checkpointRestoreError = null;
            for (int index = 0; index < _additionalObjectives.Length; index++)
                _additionalObjectives[index].ResetForSegment(_segmentNumber);
            _combatWorld.ResumeCombat();
            BroadcastState();
        }

        private void ApplyCurrentSegmentTuning()
        {
            ChapterCombatSegmentDefinition segment = CurrentSegment;
            for (int index = 0; index < _enemies.Count; index++)
            {
                EnemySpawnDirector2D director = _enemies[index].Director;
                if (segment.TryGetRule(director.Channel, out SegmentSpawnRule rule))
                {
                    director.ApplyRuntimeTuning(
                        rule.Enabled,
                        rule.InitialDelaySeconds,
                        rule.IntervalMultiplier,
                        rule.MaximumAliveCount, segment.EnemyHealthMultiplier);
                }
                else
                {
                    // 未登记到当前段的频道不能被战斗域 ResumeCombat/Begin 再次打开。
                    director.ApplyRuntimeTuning(false, 0f, 1f, 1);
                }
            }
        }

        private void CompleteChapter()
        {
            Phase = ChapterRunPhase.Complete;
            FailureReason = ChapterFailureReason.None;
            // 最终段沿用原直接结算规则；先结束碰撞/待发攻击与遭遇，再提交本段收益。
            _combatWorld.StopCombat(ChapterCombatStopReason.Settlement);
            _upgradeController.SettleFinalBattle();
            GrantMetaRewardOnce();
            _restNode.SuspendCombatForSettlement();
            Time.timeScale = 0f;
            BroadcastState();
        }

        private void ReturnToOpening()
        {
            GameAppRoot.Instance.SceneRouter.LoadMainMenu(
                MainMenuPage.LevelSelection);
        }

        private void GrantMetaRewardOnce()
        {
            if (_metaRewardGranted) return;

            _metaRewardGranted = true;
            if (!TryValidateLevelIdentity(out _rewardMessage) || _runLevel != _levelBindings.Level)
            {
                if (string.IsNullOrEmpty(_rewardMessage))
                    _rewardMessage = "本局关卡定义在运行中发生变化，已阻止通关写入。";
                Debug.LogError($"[{nameof(ChapterRunController)}] {_rewardMessage}", this);
                _awardedVouchers = 0;
                return;
            }
            if (!_profile.TryAwardCompletion(
                    _runLevel,
                    out _awardedVouchers,
                    out _wasFirstClear,
                    out _rewardMessage))
            {
                _awardedVouchers = 0;
                return;
            }
            GameAppRoot.Instance.Achievements.Report(
                AchievementTriggerIds.LevelCleared);
            if (!_anyPlayerDowned)
                GameAppRoot.Instance.Achievements.Report(
                    AchievementTriggerIds.FlawlessLevelCleared);
        }

        private void CapturePlayerCheckpoint()
        {
            _deepSeekCheckpoint = _deepSeekLife.CaptureCheckpoint();
            _harnessCheckpoint = _harnessLife.CaptureCheckpoint();
        }

        private void RestorePlayersFromCheckpoint()
        {
            _deepSeekLife.RestoreCheckpoint(
                _deepSeekCheckpoint,
                _runConfig.CheckpointInvulnerabilitySeconds);
            _harnessLife.RestoreCheckpoint(
                _harnessCheckpoint,
                _runConfig.CheckpointInvulnerabilitySeconds);
        }

        private bool TryValidateCheckpointRestore(out string reason)
        {
            if (!_restNode.TryValidateCheckpointRestore(out reason) ||
                !_upgradeController.HasCheckpoint || !_upgradeController.TryValidateCheckpoint(out reason))
            {
                if (string.IsNullOrEmpty(reason)) reason = "强化检查点不存在。";
                return false;
            }
            if (!TryValidatePlayerCheckpoint(_deepSeekLife, _deepSeekCheckpoint, out reason))
            {
                reason = "DS 检查点无效：" + reason;
                return false;
            }
            if (!TryValidatePlayerCheckpoint(_harnessLife, _harnessCheckpoint, out reason))
            {
                reason = "HS 检查点无效：" + reason;
                return false;
            }
            return true;
        }

        private static bool TryValidatePlayerCheckpoint(PlayerLifeStateController2D life,
            in PlayerLifeCheckpoint checkpoint, out string reason)
        {
            if (life == null || !life.isActiveAndEnabled ||
                !IsFinite(checkpoint.Position.x) || !IsFinite(checkpoint.Position.y) ||
                !IsFinite(checkpoint.Health) || checkpoint.Health <= 0f)
            {
                reason = "生命控制器未启用，或检查点位置/生命不是有效正值。";
                return false;
            }
            return life.TryValidateConfiguration(out reason);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnSceneExitStarted()
        {
            if (!_isInitialized || _sceneExitStarted) return;
            _sceneExitStarted = true;
            _combatWorld.StopCombat(ChapterCombatStopReason.SceneExit);
            _restNode.SetPresentationSuspended(true);
        }

        private void RefreshReplicaFlow()
        {
            if (!_isInitialized || CanAuthor || _sceneExitStarted) return;
            // Chapter/Node 是独立消息：只按两个当前状态的交集放行，任一先到都不能提前开火。
            bool combatAllowed = Phase == ChapterRunPhase.Combat &&
                (_restNode.State == RestNodeState.Combat || _restNode.State == RestNodeState.Clearing);
            _combatWorld.ApplyReplicaCombatAllowed(combatAllowed);
            _restNode.SetPresentationSuspended(Phase == ChapterRunPhase.Defeat || Phase == ChapterRunPhase.Complete);
            _upgradeController.SetReplicaNodeActive(Phase == ChapterRunPhase.Node && _restNode.State == RestNodeState.Open);
        }

        private void OnEnemyDespawned(EnemyDespawnRequest2D request, EnemySpawnChannelDefinition channel)
        {
            if (CanAuthor && Phase == ChapterRunPhase.Combat &&
                request.Reason == EnemyDespawnReason.Defeated)
            {
                if (CurrentSegment.CountsEnemy(channel)) _defeats++;
                _totalDefeats++;
            }
        }

        private void SubscribeEnemyPools(bool subscribe)
        {
            if (_enemies == null)
            {
                return;
            }
            if (_enemyDespawnHandlers == null)
            {
                _enemyDespawnHandlers = new Action<EnemyDespawnRequest2D>[_enemies.Count];
                for (int index = 0; index < _enemies.Count; index++)
                {
                    var channel = _enemies[index].Director.Channel;
                    _enemyDespawnHandlers[index] = request => OnEnemyDespawned(request, channel);
                }
            }
            for (int index = 0; index < _enemies.Count; index++)
            {
                EnemyActorPool2D pool = _enemies[index].Pool;
                if (pool == null) continue;
                if (subscribe)
                    pool.ActorDespawned += _enemyDespawnHandlers[index];
                else
                    pool.ActorDespawned -= _enemyDespawnHandlers[index];
            }
        }

        private void Render()
        {
            bool visible = Phase == ChapterRunPhase.Combat ||
                Phase == ChapterRunPhase.Defeat;
            _hud.Render(BuildHudText(), visible,
                Phase == ChapterRunPhase.Defeat);
            _hud.RenderSettlement(
                BuildSettlementText(),
                Phase == ChapterRunPhase.Complete);
        }

        private string BuildSettlementText()
        {
            if (Phase != ChapterRunPhase.Complete)
            {
                return string.Empty;
            }

            int totalSeconds = Mathf.CeilToInt(_totalCombatSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return "原型演练完成\n\n" +
                $"战斗段  {_runConfig.CombatSegmentCount}/{_runConfig.CombatSegmentCount}\n" +
                $"累计击败  {_totalDefeats}\n" +
                $"战斗用时  {minutes:00}:{seconds:00}\n\n" +
                (_awardedVouchers > 0
                    ? $"{(_wasFirstClear ? "首次" : "重复")}通关：" +
                      $"鲸元券 +{_awardedVouchers}\n" +
                      $"当前持有  {_profile.WhaleVoucherBalance}"
                    : _rewardMessage);
        }

        private string BuildHudText()
        {
            if (Phase == ChapterRunPhase.Defeat)
            {
                return FailureText(FailureReason) + (string.IsNullOrEmpty(_checkpointRestoreError)
                    ? "\n正在返回检查点……" : "\n检查点无效，恢复已停止，请返回关卡选择。");
            }
            if (Phase != ChapterRunPhase.Combat)
            {
                return string.Empty;
            }

            string text = $"第 {_segmentNumber} 波  " +
                $"剩余 {Mathf.CeilToInt(_remainingCombatSeconds)} 秒  " +
                $"{CurrentSegment.DisplayName}  " +
                $"{CurrentSegment.ObjectiveLabel} {Mathf.Min(_defeats, CurrentSegment.RequiredDefeats)}/{CurrentSegment.RequiredDefeats}";
            if (_teamDownedSeconds > 0f)
                text += $"\n全队宕机：{Remaining(_runConfig.TeamDownedTimeoutSeconds, _teamDownedSeconds):0.0}s";
            else if (_deepSeekDownedSeconds > 0f)
                text += $"\nDS 数据丢失倒计时：{Remaining(_runConfig.SingleDownedTimeoutSeconds, _deepSeekDownedSeconds):0.0}s";
            else if (_harnessDownedSeconds > 0f)
                text += $"\nHS 数据丢失倒计时：{Remaining(_runConfig.SingleDownedTimeoutSeconds, _harnessDownedSeconds):0.0}s";
            return text;
        }

        private static float Remaining(float limit, float elapsed) =>
            Mathf.Max(0f, limit - elapsed);

        private bool AreAdditionalObjectivesComplete()
        {
            if (CurrentSegment.ObjectiveMode == ChapterObjectiveMode.EnemyChannel) return true;
            bool hasRequiredObjective = false;
            for (int index = 0; index < _additionalObjectives.Length; index++)
            {
                IChapterCombatObjective objective = _additionalObjectives[index];
                if (!objective.IsRequiredForSegment(_segmentNumber)) continue;
                hasRequiredObjective = true;
                if (!objective.IsComplete) return false;
            }
            return CurrentSegment.ObjectiveMode != ChapterObjectiveMode.EncountersOnly || hasRequiredObjective;
        }

        private static string FailureText(ChapterFailureReason reason)
        {
            return reason switch
            {
                ChapterFailureReason.DeepSeekLost => "任务失败 · DS 数据丢失",
                ChapterFailureReason.HarnessLost => "任务失败 · HS 数据丢失",
                ChapterFailureReason.TeamDowned => "任务失败 · 全队宕机",
                ChapterFailureReason.ObjectiveIncomplete => "任务失败 · 击杀目标未完成",
                _ => "任务失败"
            };
        }

        private void BroadcastState()
        {
            if (!IsOnline || !_session.IsAuthority)
            {
                return;
            }
            _session.SendAuthority(
                NetworkRunState,
                writer =>
                {
                    writer.Write((byte)Phase);
                    writer.Write((byte)FailureReason);
                    writer.Write(_segmentNumber);
                    writer.Write(_remainingCombatSeconds);
                    writer.Write(_defeats);
                    writer.Write(_deepSeekDownedSeconds);
                    writer.Write(_harnessDownedSeconds);
                    writer.Write(_teamDownedSeconds);
                    writer.Write(_anyPlayerDowned);
                    writer.Write(_totalDefeats);
                    writer.Write(_totalCombatSeconds);
                },
                reliable: true);
        }

        private void ReadAuthorityState(byte kind, BinaryReader reader)
        {
            if (kind != NetworkRunState || !IsOnline || _session.IsAuthority)
            {
                return;
            }
            ChapterRunPhase previousPhase = Phase;
            Phase = (ChapterRunPhase)reader.ReadByte();
            FailureReason = (ChapterFailureReason)reader.ReadByte();
            _segmentNumber = reader.ReadInt32();
            _remainingCombatSeconds = reader.ReadSingle();
            _defeats = reader.ReadInt32();
            _deepSeekDownedSeconds = reader.ReadSingle();
            _harnessDownedSeconds = reader.ReadSingle();
            _teamDownedSeconds = reader.ReadSingle();
            _anyPlayerDowned = reader.ReadBoolean();
            _totalDefeats = reader.ReadInt32();
            _totalCombatSeconds = reader.ReadSingle();
            RefreshReplicaFlow();
            if (previousPhase != ChapterRunPhase.Complete &&
                Phase == ChapterRunPhase.Complete)
            {
                GrantMetaRewardOnce();
                _restNode.SuspendCombatForSettlement();
                Time.timeScale = 0f;
            }
            Render();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (!TryValidateLevelIdentity(out reason)) return false;
            if (!_levelBindings.TryValidateConfiguration(out reason)) return false;
            if (_levelBindings.ChapterRun != this || _levelBindings.RestNode != _restNode ||
                _levelBindings.Session != _session)
            {
                reason = "_levelBindings 的章节、节点或会话与章节派生接线不一致。";
                return false;
            }
            if (_restNode == null || _combatWorld == null || _upgradeController == null ||
                _selection == null || _deepSeekLife == null ||
                _harnessLife == null || _hud == null ||
                _profile == null)
            {
                reason = "节点、战斗域、强化、选角、两名玩家、刷怪器、敌人池和 HUD 必须完整配置。";
                return false;
            }
            if (_combatWorld.gameObject.scene != gameObject.scene || _combatWorld.Bindings != _levelBindings)
            {
                reason = "战斗域必须属于当前关卡并引用同一份显式 LevelSceneBindings。";
                return false;
            }
            if (!_combatWorld.TryValidateConfiguration(out reason)) return false;
            int objectiveCount = _additionalObjectiveComponents?.Length ?? 0;
            for (int index = 0; index < objectiveCount; index++)
            {
                if (_additionalObjectiveComponents[index] is not IChapterCombatObjective ||
                    System.Array.IndexOf(_additionalObjectiveComponents, _additionalObjectiveComponents[index]) != index)
                {
                    reason = $"附加目标 {index + 1} 为空、重复或未实现 IChapterCombatObjective。";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>完整定义已在 Awake 验证；联机/选角边界只检查初始化、退出、身份和网络根归属。</summary>
        public bool TryValidateLevelStart(out string reason)
        {
            if (!Application.isPlaying) return TryValidateConfiguration(out reason);
            if (!_isInitialized || !isActiveAndEnabled || _sceneExitStarted)
            {
                reason = "章节未初始化、已停用或正在退出，不能开战。";
                return false;
            }
            return TryValidateLevelIdentity(out reason) && _levelBindings.TryValidateNetworkOwnership(out reason);
        }

        /// <summary>验证启动、场景与结算身份；Editor 无启动意图时仅使用显式场景绑定。</summary>
        public bool TryValidateLevelIdentity(out string reason)
        {
            if (_levelBindings == null || _levelBindings.gameObject.scene != gameObject.scene)
            {
                reason = "_levelBindings 为空或不属于当前场景，请显式完成关卡登记装配。";
                return false;
            }
            MetaLevelDefinition boundLevel = _levelBindings.Level;
            MetaLevelDefinition selectedLevel = GameAppRoot.Instance == null ||
                GameAppRoot.Instance.LaunchContext == null
                    ? null
                    : GameAppRoot.Instance.LaunchContext.SelectedLevel;
            if (!LevelIdentityValidation.TryValidateContext(boundLevel, gameObject.scene.name,
                    selectedLevel, Application.isPlaying && !Application.isEditor, out reason)) return false;
            if (_level != boundLevel || _config != boundLevel.ChapterRunConfig)
            {
                reason = $"关卡 {boundLevel.LevelId} 的派生 _level/_config 与 _levelBindings 不一致；不会在运行时覆盖错绑引用。";
                return false;
            }
            if (_isInitialized && (_runLevel != boundLevel || _runConfig != boundLevel.ChapterRunConfig ||
                !string.Equals(_runLevelId, boundLevel.LevelId, StringComparison.Ordinal)))
            {
                reason = $"本局关卡 {boundLevel.LevelId} 的定义或流程引用在运行中发生变化。";
                return false;
            }
            reason = string.Empty;
            return true;
        }

    }
}
