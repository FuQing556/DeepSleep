using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>
    /// 实际 Play/Router/选角/伤害/节点回归；最终结算跳过永久奖励落盘，最后回菜单。
    /// 仅推进场景实例计时器。成就使用临时内存档案的已完成分支，绝不改存档路径或资产。
    /// </summary>
    public static class ChapterFlowLifecycleChecks
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string LevelRoot = "Assets/_Project/Configs/Progression/Meta/CFG_META_Level_";
        public static bool Running { get; private set; }
        public static string Status { get; private set; } = "Not run";
        public static string LastReport { get; private set; } = string.Empty;

        public static string Start()
        {
            if (!EditorApplication.isPlaying || GameAppRoot.Instance == null || !Debug.isDebugBuild)
                return "Requires Editor Play from Boot/MainMenu or fresh offline selection.";
            var router = GameAppRoot.Instance.SceneRouter;
            if (Running || router.IsTransitioning) return "A check or transition is already running.";
            foreach (var session in UnityEngine.Object.FindObjectsByType<CoopSessionController>(FindObjectsInactive.Include))
                if (session.Phase != SessionPhase.Offline || session.HasPeer || session.Selection == null || session.Selection.IsSelectionComplete)
                    return "Refused: leave the active match first; only fresh offline selection/MainMenu is accepted.";
            var prototype = AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(LevelRoot + "PrototypeSky.asset");
            var world = AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(LevelRoot + "World01_EarlyInternet.asset");
            if (prototype == null || world == null) return "Missing level definitions.";
            Running = true;
            LastReport = string.Empty;
            Status = "Starting chapter flow checks";
            router.StartCoroutine(GuardedRun(router, prototype, world));
            return Status;
        }

        private static IEnumerator GuardedRun(GameSceneRouter router, params MetaLevelDefinition[] levels)
        {
            ProfileIsolation profile = null;
            Exception failure = null;
            var random = UnityEngine.Random.state;
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                try { profile = new ProfileIsolation(GameAppRoot.Instance.Profile, GameAppRoot.Instance.Achievements.Definitions); }
                catch (Exception exception) { failure = exception; }
                if (failure == null)
                    yield return Drive(RunMatrix(router, levels), exception => failure = exception);
                if (failure == null)
                {
                    SceneExitLifecycleChecks.Start();
                    yield return Drive(Until(() => !SceneExitLifecycleChecks.Running, "scene exit matrix", 120f),
                        exception => failure = exception);
                    if (!SceneExitLifecycleChecks.Status.StartsWith("PASSED", StringComparison.Ordinal))
                        failure = Combine(failure, new InvalidOperationException(SceneExitLifecycleChecks.LastReport));
                    LastReport += SceneExitLifecycleChecks.LastReport + "\n";
                }
                // Cleanup also uses the real Router, even if a nested test iterator failed.
                yield return Drive(Route(router, null), exception => failure = Combine(failure, exception));
                try { profile?.RestoreAndVerify(); }
                catch (Exception exception) { failure = Combine(failure, exception); }
                Status = failure == null ? "PASSED chapter flow in both scenes; returned to MainMenu; profile/backup unchanged."
                    : "FAILED chapter flow: " + failure;
                LastReport = Status + "\n" + LastReport;
                if (failure == null) Debug.Log("[ChapterFlowLifecycleChecks] " + LastReport, router);
                else Debug.LogError("[ChapterFlowLifecycleChecks] " + LastReport, router);
            }
            finally
            {
                profile?.Dispose();
                UnityEngine.Random.state = random;
                Application.runInBackground = background;
                Running = false;
            }
        }

        // Flatten nested IEnumerator ourselves so errors cannot escape Unity's nested-coroutine runner.
        private static IEnumerator Drive(IEnumerator work, Action<Exception> failed)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(work);
            try
            {
                while (stack.Count > 0)
                {
                    bool more = false;
                    object next = null;
                    Exception error = null;
                    try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                    catch (Exception exception) { error = exception; }
                    if (error != null) { failed(new InvalidOperationException(Status, error)); yield break; }
                    if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (next is IEnumerator nested) stack.Push(nested);
                    else yield return next;
                }
            }
            finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
        }

        private static IEnumerator RunMatrix(GameSceneRouter router, MetaLevelDefinition[] levels)
        {
            yield return Route(router, null);
            foreach (var level in levels)
            {
                Require(level.ChapterRunConfig.CombatSegmentCount >= 2, "Fixture requires at least two combat segments.");
                yield return Route(router, level);
                var chapter = One<ChapterRunController>();
                // NGO's root can already be in DontDestroyOnLoad; use the explicit current-level binding.
                var session = chapter.LevelBindings.Session;
                var node = chapter.LevelBindings.RestNode;
                var world = chapter.CombatWorld;
                var upgrades = One<RestNodeUpgradeController>();
                var wallet = One<TokenWallet>();
                var ranks = One<PlayerUpgradeRuntimeState>();
                Require(chapter.IsInitialized && world != null && chapter.enabled, "Chapter/world did not initialize.");
                CheckStartValidationBoundaries(chapter);
                Passed(level, "initialized start gate skips static content revalidation but rejects captured level-ID mismatch");
                CheckReplicaFlow(chapter, session, node, upgrades, wallet);
                Passed(level, "connecting guest cannot author; replica chapter/node order and duplicate states preserve wallet/shop");
                Require(session.Selection.TrySelect(PlayerRole.DeepSeek), "Actual offline selection failed.");
                using (new IdleControls(session))
                {
                    DelaySpawns(world);
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Combat, "selection -> combat");
                    var initialWallet = wallet.CaptureSnapshot();
                    var ds = Get<PlayerLifeStateController2D>(chapter, "_deepSeekLife");
                    var hs = Get<PlayerLifeStateController2D>(chapter, "_harnessLife");
                    float initialDs = ds.CurrentHealth, initialHs = hs.CurrentHealth;

                    Status = level.SceneName + ": first-segment failure";
                    wallet.RecordBattleReward(13);
                    chapter.FailObjectiveForDevelopment();
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Defeat, "first failure");
                    Require(chapter.FailureReason == ChapterFailureReason.ObjectiveIncomplete, "Wrong first failure reason.");
                    AssertStopped(world);
                    AdvanceDefeat(chapter, level);
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Combat, "first-segment retry");
                    DelaySpawns(world);
                    Require(chapter.SegmentNumber == 1 && node.State == RestNodeState.Combat, "First failure skipped segment/node.");
                    AssertWallet(wallet, initialWallet, initialWallet.IsBattleSettled);
                    Require(Near(ds.CurrentHealth, initialDs) && Near(hs.CurrentHealth, initialHs), "Initial HP checkpoint was not restored.");
                    Passed(level, "first failure retries segment 1 and rolls back earnings/HP");

                    Status = level.SceneName + ": clearing still permits team-down failure";
                    RentResidual(world);
                    CompleteAdditionalObjectives(chapter, session.DeepSeek.gameObject);
                    chapter.CompleteObjectiveAndExpireForDevelopment();
                    yield return Until(() => node.State == RestNodeState.Clearing, "enter clearing");
                    AssertClearing(chapter, world);
                    Down(ds, session.DeepSeek.gameObject);
                    Down(hs, session.Harness.gameObject);
                    Set(chapter, "_teamDownedSeconds", level.ChapterRunConfig.TeamDownedTimeoutSeconds);
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Defeat, "clearing team failure");
                    Require(chapter.FailureReason == ChapterFailureReason.TeamDowned, "Clearing ignored team-down failure.");
                    AssertStopped(world);
                    AdvanceDefeat(chapter, level);
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Combat, "retry after clearing failure");
                    DelaySpawns(world);
                    Require(chapter.SegmentNumber == 1, "Clearing failure changed segment.");
                    AssertWallet(wallet, initialWallet, initialWallet.IsBattleSettled);
                    Passed(level, "clearing stops spawns but preserves life-failure rules");

                    Status = level.SceneName + ": natural clear and node";
                    var residual = RentResidual(world);
                    CompleteAdditionalObjectives(chapter, session.DeepSeek.gameObject);
                    chapter.CompleteObjectiveAndExpireForDevelopment();
                    yield return Until(() => node.State == RestNodeState.Clearing, "second clearing");
                    AssertClearing(chapter, world);
                    int kills = chapter.Defeats, earned = wallet.BattleEarned;
                    Require(residual.Health.TryReceiveDamage(new DamagePacket(residual.Health.CurrentHealth + 1f,
                        residual.transform.position, Vector2.left, session.DeepSeek.gameObject)), "Residual enemy rejected real damage.");
                    Require(chapter.Defeats == kills + 1 && wallet.BattleEarned > earned, "Clearing kill did not count/reward.");
                    SeedProjectiles(world, session.DeepSeek.gameObject);
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Node, "clear -> node");
                    AssertStopped(world);
                    Set(node, "_revealElapsed", Get<float>(node, "_cloudFadeSeconds"));
                    yield return Until(() => node.State == RestNodeState.Open, "node open");
                    Require(wallet.IsBattleSettled && Get<bool>(upgrades, "_nodeActive"), "Node did not settle battle/open shop.");
                    var settled = wallet.CaptureSnapshot();
                    upgrades.BeginNode();
                    AssertWallet(wallet, settled, true);
                    Passed(level, "residual enemy stays damageable/rewarded; clear reclaims projectiles and opens node once");

                    Status = level.SceneName + ": purchase and departure checkpoint";
                    wallet.CreditRole(PlayerRole.DeepSeek, 1000);
                    var offers = Get<UpgradeCardId[]>(Get<object>(upgrades, "_deepSeek"), "Offers");
                    UpgradeCardId purchased = offers[0];
                    int oldRank = ranks.GetRank(PlayerRole.DeepSeek, purchased), oldBalance = wallet.DeepSeekBalance;
                    Invoke(upgrades, "RequestSelection", PlayerRole.DeepSeek, 0);
                    Require(ranks.GetRank(PlayerRole.DeepSeek, purchased) == oldRank + 1 && wallet.DeepSeekBalance < oldBalance,
                        "Actual node purchase failed.");
                    Invoke(upgrades, "RequestRefreshFor", PlayerRole.DeepSeek);
                    Require(Get<int>(Get<object>(upgrades, "_deepSeek"), "RefreshCount") == 1, "Shop refresh was not recorded.");
                    Require(Get<HealthComponent>(ds, "_health").RestoreCheckpointHealth(Mathf.Max(1f, ds.MaximumHealth - 1f)), "Cannot set distinguishable checkpoint HP.");
                    float checkpointDs = ds.CurrentHealth, checkpointHs = hs.CurrentHealth;
                    string shop = ShopSignature(upgrades), rankSignature = RankSignature(ranks);
                    yield return Depart(node, session, chapter, 2);
                    DelaySpawns(world);
                    var checkpointWallet = wallet.CaptureSnapshot();
                    Require(!checkpointWallet.IsBattleSettled && checkpointWallet.BattleEarned == 0, "Departure did not begin fresh battle.");
                    Require(chapter.SegmentNumber == 2 && world.Gate.CombatAllowed, "Departure did not resume segment 2.");
                    Passed(level, "real portal readiness departs once; purchases/refresh/HP checkpoint committed");

                    Status = level.SceneName + ": segment 2 rollback and same-segment retry";
                    wallet.RecordBattleReward(17);
                    Require(wallet.TrySpend(PlayerRole.DeepSeek, 1), "Cannot mutate battle wallet for rollback test.");
                    bool changedRank = false;
                    foreach (var definition in ranks.Catalog.Definitions)
                        if (ranks.TryApply(PlayerRole.DeepSeek, definition.Id)) { changedRank = true; break; }
                    Require(changedRank, "No eligible upgrade for rollback fixture.");
                    Down(ds, session.DeepSeek.gameObject);
                    chapter.FailObjectiveForDevelopment();
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Defeat, "segment 2 failure");
                    AdvanceDefeat(chapter, level);
                    yield return Until(() => chapter.Phase == ChapterRunPhase.Node && node.State == RestNodeState.Open, "return checkpoint node");
                    Require(chapter.SegmentNumber == 2, "Failure incremented segment.");
                    AssertStopped(world);
                    AssertWallet(wallet, checkpointWallet, true);
                    Require(ShopSignature(upgrades) == shop && RankSignature(ranks) == rankSignature, "Offers/refresh/purchase/ranks did not roll back.");
                    Require(ds.State == PlayerLifeState.Alive && hs.State == PlayerLifeState.Alive &&
                        Near(ds.CurrentHealth, checkpointDs) && Near(hs.CurrentHealth, checkpointHs), "Retry node overwrote exact HP checkpoint.");
                    yield return Depart(node, session, chapter, 2);
                    DelaySpawns(world);
                    Require(chapter.SegmentNumber == 2, "Leaving retry node skipped the failed segment.");
                    Passed(level, "segment 2 failure restores shop/wallet/exact HP; retry departure stays segment 2");

                    Status = level.SceneName + ": repeated forced stop";
                    RentResidual(world);
                    SeedProjectiles(world, session.DeepSeek.gameObject);
                    var encounter = FindInScene<DoubaoWordWallEncounter2D>();
                    if (encounter != null)
                    {
                        encounter.BeginEncounter();
                        for (int step = 0; step < 1200 && !(encounter.Boss.IsActive && encounter.ActiveBlocks.Count > 0); step++)
                            encounter.Simulate(.05f);
                        Require(encounter.Boss.IsActive && encounter.ActiveBlocks.Count > 0, "Real Doubao encounter did not populate within 60 simulated seconds.");
                    }
                    var beforeStop = wallet.CaptureSnapshot();
                    int beforeKills = chapter.Defeats;
                    world.StopCombat(ChapterCombatStopReason.Failure);
                    world.StopCombat(ChapterCombatStopReason.Failure);
                    AssertStopped(world);
                    AssertWallet(wallet, beforeStop, beforeStop.IsBattleSettled);
                    Require(chapter.Defeats == beforeKills, "Forced stop manufactured defeat credit.");
                    if (encounter != null) Require(encounter.State == DoubaoEncounterState.Idle && !encounter.Boss.IsActive && encounter.ActiveBlocks.Count == 0,
                        "Forced stop did not synchronously reset Doubao and bubbles.");
                    Passed(level, "repeated forced stop clears all pools" + (encounter == null ? "" : " plus live Doubao/bubbles") + " without rewards");
                }
                yield return Route(router, level);
                yield return CheckFourWaveFlow(level);
                yield return Route(router, null);
            }
        }

        private static IEnumerator CheckFourWaveFlow(MetaLevelDefinition level)
        {
            var config = level.ChapterRunConfig;
            Require(config.CombatSegmentCount == 4, "Four-wave fixture requires exactly four waves.");
            var chapter = One<ChapterRunController>();
            var session = chapter.LevelBindings.Session;
            var node = chapter.LevelBindings.RestNode;
            var world = chapter.CombatWorld;
            var wallet = One<TokenWallet>();
            var upgrades = One<RestNodeUpgradeController>();
            var ranks = One<PlayerUpgradeRuntimeState>();
            // Scene instance only: exercise final Token settlement without writing completion vouchers to the user's profile.
            Set(chapter, "_metaRewardGranted", true);
            Require(session.Selection.TrySelect(PlayerRole.DeepSeek), "Four-wave selection failed.");
            using (new IdleControls(session))
            {
                yield return Until(() => chapter.Phase == ChapterRunPhase.Combat, "four-wave combat start");
                for (int wave = 1; wave <= 4; wave++)
                {
                    Status = level.SceneName + ": sequential wave " + wave;
                    DelaySpawns(world);
                    Require(chapter.SegmentNumber == wave, "Sequential flow skipped a wave.");
                    var segment = config.GetSegment(wave);
                    if (wave == 4)
                    {
                        Require(Near(segment.EnemyHealthMultiplier, 3f) && Near(segment.DurationSeconds, 65f) &&
                            segment.RequiredDefeats == 22, "Wave 4 HP/time/kill target differs from approved values.");
                        foreach (var entry in world.Bindings.Enemies)
                        {
                            Require(segment.TryGetRule(entry.Director.Channel, out var rule), "Missing wave 4 spawn rule.");
                            Require(config.GetSegment(3).TryGetRule(entry.Director.Channel, out var previous), "Missing wave 3 spawn rule.");
                            Require(rule.Enabled && Near(rule.IntervalMultiplier, previous.IntervalMultiplier * .8f) &&
                                rule.MaximumAliveCount == previous.MaximumAliveCount && Near(rule.InitialDelaySeconds, previous.InitialDelaySeconds),
                                "Wave 4 spawn progression changed delay/cap or has wrong frequency.");
                            Require(Near(Get<float>(entry.Director, "_runtimeHealthMultiplier"), 3f) &&
                                Near(Get<float>(entry.Director, "_runtimeIntervalMultiplier"), rule.IntervalMultiplier) &&
                                Get<int>(entry.Director, "_runtimeMaximumAliveCount") == rule.MaximumAliveCount,
                                "Wave 4 runtime tuning differs from config.");
                            entry.Pool.DespawnAll(EnemyDespawnReason.RunReset);
                            Require(entry.Director.TrySpawnNow(), "Wave 4 actual spawn failed.");
                            float baseHp = Get<EnemyActor2D>(entry.Pool, "_enemyPrefab").Health.MaximumHealth;
                            foreach (var actor in entry.Pool.Instances)
                                if (actor.gameObject.activeSelf)
                                    Require(Near(actor.Health.MaximumHealth, Mathf.Floor(baseHp * 3f)) &&
                                        Near(actor.Health.CurrentHealth, actor.Health.MaximumHealth), "Wave 4 spawned HP is incorrect.");
                            entry.Pool.DespawnAll(EnemyDespawnReason.RunReset);
                        }
                        var checkpoint = wallet.CaptureSnapshot();
                        string shop = ShopSignature(upgrades), rankState = RankSignature(ranks);
                        CompleteAdditionalObjectives(chapter, session.DeepSeek.gameObject, true);
                        wallet.RecordBattleReward(17);
                        chapter.FailObjectiveForDevelopment();
                        yield return Until(() => chapter.Phase == ChapterRunPhase.Defeat, "wave 4 failure");
                        AdvanceDefeat(chapter, level);
                        yield return Until(() => chapter.Phase == ChapterRunPhase.Node && node.State == RestNodeState.Open, "wave 4 checkpoint node");
                        Require(chapter.SegmentNumber == 4 && ShopSignature(upgrades) == shop && RankSignature(ranks) == rankState,
                            "Wave 4 failure lost third-node shop/rank checkpoint.");
                        AssertWallet(wallet, checkpoint, true);
                        yield return Depart(node, session, chapter, 4);
                        DelaySpawns(world);
                        AssertWallet(wallet, checkpoint, false);
                        Require(Near(chapter.CurrentEnemyHealthMultiplier, 3f), "Retry lost wave 4 health multiplier.");
                        Passed(level, "wave 4 real enemy HP/config verified; failure rolls back earnings/shop/ranks; retry stays wave 4");
                    }
                    CompleteAdditionalObjectives(chapter, session.DeepSeek.gameObject, true);
                    var beforeClear = wallet.CaptureSnapshot();
                    chapter.CompleteObjectiveAndExpireForDevelopment();
                    if (wave < 4)
                    {
                        yield return Until(() => chapter.Phase == ChapterRunPhase.Node, "wave " + wave + " clear -> node");
                        Set(node, "_revealElapsed", Get<float>(node, "_cloudFadeSeconds"));
                        yield return Until(() => node.State == RestNodeState.Open, "wave " + wave + " node open");
                        Require(wallet.IsBattleSettled && wallet.DeepSeekBalance == beforeClear.DeepSeekBalance + beforeClear.BattleEarned &&
                            wallet.HarnessBalance == beforeClear.HarnessBalance + beforeClear.BattleEarned, "Node reward settlement incorrect.");
                        yield return Depart(node, session, chapter, wave + 1);
                        Passed(level, "wave " + wave + " -> rest shop -> wave " + (wave + 1) + " via actual portal");
                    }
                    else
                    {
                        yield return Until(() => chapter.Phase == ChapterRunPhase.Complete, "wave 4 final completion");
                        Require(chapter.SegmentNumber == 4 && wallet.IsBattleSettled &&
                            wallet.DeepSeekBalance == beforeClear.DeepSeekBalance + beforeClear.BattleEarned &&
                            wallet.HarnessBalance == beforeClear.HarnessBalance + beforeClear.BattleEarned, "Final wave did not settle once.");
                        var settled = wallet.CaptureSnapshot();
                        upgrades.SettleFinalBattle();
                        AssertWallet(wallet, settled, true);
                        AssertStopped(world);
                        Passed(level, "wave 4 final completion/Token settlement is idempotent; permanent voucher write intentionally skipped");
                    }
                }
            }
        }

        private static void CheckStartValidationBoundaries(ChapterRunController chapter)
        {
            // 仅临时改变场景实例的编辑期引用；不改已捕获的运行目标，不 yield，不触碰资产。
            var objectives = Get<MonoBehaviour[]>(chapter, "_additionalObjectiveComponents");
            string capturedId = Get<string>(chapter, "_runLevelId");
            try
            {
                Set(chapter, "_additionalObjectiveComponents", new MonoBehaviour[] { null });
                Require(!chapter.TryValidateConfiguration(out _) && chapter.TryValidateLevelStart(out _),
                    "Play start boundary repeated the static content graph validation after initialization.");
                Set(chapter, "_additionalObjectiveComponents", objectives);
                Set(chapter, "_runLevelId", capturedId + "_stale_identity");
                Require(!chapter.TryValidateLevelStart(out _),
                    "Play start boundary accepted a level ID different from its captured run identity.");
            }
            finally
            {
                Set(chapter, "_additionalObjectiveComponents", objectives);
                Set(chapter, "_runLevelId", capturedId);
            }
        }

        private static IEnumerator Depart(RestNodePrototypeController2D node, CoopSessionController session,
            ChapterRunController chapter, int expectedSegment)
        {
            foreach (var actor in new[] { session.DeepSeek, session.Harness })
            {
                var body = actor.GetComponent<Collider2D>();
                Require(body != null && node.TryReadPortalGoal(actor, body, out _), "Missing actual portal/body geometry.");
                node.TryReadPortalGoal(actor, body, out RestNodePortalGoal goal);
                actor.transform.position = new Vector3(goal.Destination.x, goal.Destination.y, actor.transform.position.z);
                var rigidbody = actor.GetComponent<Rigidbody2D>();
                if (rigidbody != null) { rigidbody.position = goal.Destination; rigidbody.linearVelocity = Vector2.zero; }
            }
            Physics2D.SyncTransforms();
            yield return null;
            Require(node.State == RestNodeState.Open, "Portal departed before the human readied.");
            Invoke(node, "ActivateNearestHotspot");
            yield return Until(() => node.State == RestNodeState.Departing, "portal readiness -> departure");
            int before = chapter.SegmentNumber;
            Require(!node.ReturnToCombat() && chapter.SegmentNumber == before, "Repeated departure was accepted.");
            Set(node, "_revealElapsed", Get<float>(node, "_cloudFadeSeconds"));
            yield return Until(() => node.State == RestNodeState.Combat && chapter.Phase == ChapterRunPhase.Combat, "departure -> combat");
            Require(chapter.SegmentNumber == expectedSegment, "Departure advanced wrong number of segments.");
        }

        private static void CompleteAdditionalObjectives(ChapterRunController chapter, GameObject source, bool verifyScalingAndReward = false)
        {
            foreach (var component in Get<MonoBehaviour[]>(chapter, "_additionalObjectiveComponents"))
            {
                var objective = (IChapterCombatObjective)component;
                if (!objective.IsRequiredForSegment(chapter.SegmentNumber) || objective.IsComplete) continue;
                Require(component is DoubaoChapterEncounterDriver2D, "Fixture needs an explicit completion path for this objective.");
                var encounter = Get<DoubaoWordWallEncounter2D>(component, "_encounter");
                var driver = (DoubaoChapterEncounterDriver2D)component;
                for (int step = 0; step < 1200 && !encounter.Boss.IsActive; step++) driver.Simulate(.05f);
                var wallet = One<TokenWallet>();
                int earned = wallet.BattleEarned;
                if (verifyScalingAndReward)
                    Require(Near(encounter.Boss.CurrentHealth, Mathf.Floor(45f * chapter.CurrentEnemyHealthMultiplier)), "Doubao spawned HP does not match wave scaling.");
                Require(encounter.Boss.TryReceiveDamage(new DamagePacket(encounter.Boss.CurrentHealth + 1f,
                    encounter.Boss.transform.position, Vector2.left, source)) && objective.IsComplete,
                    "Actual Doubao defeat did not complete the required objective.");
                if (verifyScalingAndReward)
                {
                    Require(wallet.BattleEarned == earned + 100, "Doubao reward must be exactly 100 shared Token.");
                    driver.Simulate(.05f);
                    Require(objective.IsComplete && wallet.BattleEarned == earned + 100 && !encounter.Boss.IsActive,
                        "Completed Doubao encounter restarted or rewarded twice.");
                }
            }
        }

        private static void CheckReplicaFlow(ChapterRunController chapter, CoopSessionController session,
            RestNodePrototypeController2D node, RestNodeUpgradeController upgrades, TokenWallet wallet)
        {
            Require(!session.IsAuthority, "Replica fixture requires unopened offline transport.");
            var originalPhase = session.Phase;
            var originalChapter = chapter.Phase;
            var snapshot = wallet.CaptureSnapshot();
            string shop = ShopSignature(upgrades);
            object checkpoint = Get<object>(upgrades, "_checkpoint");
            int changes = 0;
            Action<RestNodeState> changed = _ => changes++;
            node.StateChanged += changed;
            try
            {
                Set(session, "<Phase>k__BackingField", SessionPhase.Connecting);
                Invoke(chapter, "OnSelectionConfirmed", PlayerRole.DeepSeek);
                Require(chapter.Phase == ChapterRunPhase.WaitingForSelection && !chapter.CombatWorld.Gate.CombatAllowed,
                    "Connecting guest authored combat before authority start.");
                Set(session, "<Phase>k__BackingField", SessionPhase.Playing);
                ReplicaPhase(chapter, ChapterRunPhase.Combat);
                Invoke(node, "ApplyState", RestNodeState.Clearing, false);
                Require(chapter.CombatWorld.Gate.CombatAllowed, "Replica clearing blocked combat.");
                Invoke(node, "ApplyState", RestNodeState.Revealing, false);
                Require(!chapter.CombatWorld.Gate.CombatAllowed, "Node-first clear failed to close replica gate.");
                ReplicaPhase(chapter, ChapterRunPhase.Node);
                Invoke(node, "ApplyState", RestNodeState.Open, false);
                Require(Get<bool>(upgrades, "_nodeActive"), "Replica node shop did not open.");
                int before = changes;
                Invoke(node, "ApplyState", RestNodeState.Open, false);
                Require(changes == before, "Duplicate Open re-entered lifecycle.");
                ReplicaPhase(chapter, ChapterRunPhase.Combat);
                Require(!chapter.CombatWorld.Gate.CombatAllowed, "Chapter-first departure opened gate before node.");
                Invoke(node, "ApplyState", RestNodeState.Combat, false);
                Require(chapter.CombatWorld.Gate.CombatAllowed, "Replica departure did not open combat.");
                ReplicaPhase(chapter, ChapterRunPhase.Node);
                Invoke(node, "ApplyState", RestNodeState.Open, false);
                Invoke(node, "ApplyState", RestNodeState.Combat, false);
                Require(!chapter.CombatWorld.Gate.CombatAllowed, "Node-first departure opened gate before chapter.");
                ReplicaPhase(chapter, ChapterRunPhase.Combat);
                Require(chapter.CombatWorld.Gate.CombatAllowed, "Node-first departure never reopened gate.");
                ReplicaPhase(chapter, ChapterRunPhase.Defeat);
                Require(!chapter.CombatWorld.Gate.CombatAllowed && !Get<bool>(upgrades, "_nodeActive"), "Replica defeat did not close combat/shop.");
                AssertWallet(wallet, snapshot, snapshot.IsBattleSettled);
                Require(ShopSignature(upgrades) == shop && ReferenceEquals(checkpoint, Get<object>(upgrades, "_checkpoint")),
                    "Replica state changes authored shop/checkpoint state.");
            }
            finally
            {
                node.StateChanged -= changed;
                ReplicaPhase(chapter, originalChapter);
                Invoke(node, "ApplyState", RestNodeState.Combat, true);
                Set(session, "<Phase>k__BackingField", originalPhase);
            }
        }

        private static void ReplicaPhase(ChapterRunController chapter, ChapterRunPhase phase)
        { typeof(ChapterRunController).GetProperty(nameof(ChapterRunController.Phase)).SetValue(chapter, phase); Invoke(chapter, "RefreshReplicaFlow"); }

        private static IEnumerator Route(GameSceneRouter router, MetaLevelDefinition level)
        {
            Status = "Router -> " + (level == null ? "MainMenu" : level.SceneName);
            // A failed earlier station may have left a legitimate transition in flight.
            yield return Until(() => !router.IsTransitioning, "previous transition", 20f);
            if (level == null) router.LoadMainMenu(MainMenuPage.Home);
            else router.StartLevel(level, GameLaunchMode.Solo);
            yield return null;
            yield return Until(() => !router.IsTransitioning, "scene transition", 20f);
            Require(string.IsNullOrEmpty(router.LastTransitionError), router.LastTransitionError);
            Require(SceneManager.GetActiveScene().name == (level == null ? "MainMenu" : level.SceneName), "Router loaded wrong scene.");
            yield return null; // Start has captured initial progression/player checkpoints before any selection.
        }

        private static IEnumerator Until(Func<bool> condition, string name, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition()) { Require(Time.realtimeSinceStartup < deadline, "Timeout: " + name); yield return null; }
        }

        private static void AdvanceDefeat(ChapterRunController chapter, MetaLevelDefinition level) =>
            Set(chapter, "_defeatElapsed", level.ChapterRunConfig.DefeatPresentationSeconds);
        private static void DelaySpawns(ChapterCombatWorld2D world)
        { foreach (var entry in world.Bindings.Enemies) Set(entry.Director, "_remainingSeconds", 1000f); }
        private static EnemyActor2D RentResidual(ChapterCombatWorld2D world)
        {
            Require(world.IsCleared, "Fixture requires an empty combat world before inserting a residual enemy.");
            var variation = new EnemySpawnVariation2D(Vector2.left, .001f, 0f, 0f);
            Require(world.Bindings.Enemies[0].Pool.TryRent(new Vector2(7f, 3f), variation, out var actor), "Cannot rent residual enemy.");
            actor.Health.SetMaximumHealthBonus(1000000f); // Instance only; survive autonomous weapons until explicit kill.
            return actor;
        }
        private static void SeedProjectiles(ChapterCombatWorld2D world, GameObject source)
        {
            Require(world.Rice.TryRent(new Vector2(0f, 4f), Vector2.right, source, 1f, out _), "Cannot seed rice projectile.");
            var seen = new HashSet<DeepSleep.Runtime.Combat.Projectiles.EnemyProjectilePool2D>();
            foreach (var entry in world.Bindings.Enemies)
                foreach (var pool in entry.ProjectilePools)
                    if (seen.Add(pool)) Require(pool.TryRent(new Vector2(0f, 4f), Vector2.right, source, out _), "Cannot seed enemy projectile.");
        }
        private static void AssertClearing(ChapterRunController chapter, ChapterCombatWorld2D world)
        {
            Require(chapter.Phase == ChapterRunPhase.Combat && world.Gate.CombatAllowed && !world.IsCleared,
                "Clearing prematurely locked combat or lost the residual enemy.");
            foreach (var entry in world.Bindings.Enemies) Require(!entry.Director.IsRunning, "Spawn director still running during clearing.");
        }
        private static void AssertStopped(ChapterCombatWorld2D world)
        {
            Require(!world.Gate.CombatAllowed && world.IsCleared, "Combat gate/enemy pool was not stopped.");
            Require(world.Rice.AvailableCount == world.Rice.TotalCount, "Rice projectiles survived stop.");
            foreach (var entry in world.Bindings.Enemies)
            {
                Require(!entry.Director.IsRunning && entry.Pool.ActiveCount == 0, "Enemy/director survived stop.");
                foreach (var pool in entry.ProjectilePools) Require(pool.ActiveCount == 0, "Enemy bullets survived stop.");
            }
        }
        private static void Down(PlayerLifeStateController2D life, GameObject source)
        {
            var health = Get<HealthComponent>(life, "_health");
            Require(health.TryReceiveDamage(new DamagePacket(health.CurrentHealth + 1f, life.transform.position, Vector2.right, source)) &&
                life.State == PlayerLifeState.Downed, "Real health/depletion chain did not down player.");
        }
        private static void AssertWallet(TokenWallet wallet, TokenWalletSnapshot snapshot, bool settled) =>
            Require(wallet.DeepSeekBalance == snapshot.DeepSeekBalance && wallet.HarnessBalance == snapshot.HarnessBalance &&
                wallet.BattleEarned == snapshot.BattleEarned && wallet.IsBattleSettled == settled, "Wallet checkpoint/reward mismatch.");
        private static string ShopSignature(RestNodeUpgradeController upgrades)
        {
            string result = Get<int>(upgrades, "_nodeSerial") + ":";
            foreach (string name in new[] { "_deepSeek", "_harness" })
            {
                object state = Get<object>(upgrades, name);
                result += string.Join(",", Get<UpgradeCardId[]>(state, "Offers")) + "/" +
                    Get<int>(state, "RefreshCount") + "/" + Get<int>(state, "PurchaseCount") + ";";
            }
            return result;
        }
        private static string RankSignature(PlayerUpgradeRuntimeState ranks)
        {
            string result = string.Empty;
            foreach (var item in ranks.Catalog.Definitions)
                result += (int)item.Id + ":" + ranks.GetRank(PlayerRole.DeepSeek, item.Id) + "," + ranks.GetRank(PlayerRole.Harness, item.Id) + ";";
            return result;
        }

        private sealed class IdleControls : IDisposable, ICommandSource
        {
            private readonly CoopSessionController _session;
            private readonly PlayerActor[] _actors;
            private readonly ICommandSource[] _original = new ICommandSource[2];
            private readonly bool _autoTakeover;
            private uint _sequence;
            public IdleControls(CoopSessionController session)
            {
                _session = session; _autoTakeover = session.AutoTakeoverOnFocusLoss;
                _actors = new[] { session.DeepSeek, session.Harness };
                for (int i = 0; i < _actors.Length; i++) _original[i] = Get<ICommandSource>(_actors[i].CommandDispatcher, "_commandSource");
                session.AutoTakeoverOnFocusLoss = false;
                for (int i = 0; i < _actors.Length; i++) _actors[i].CommandDispatcher.TryBindCommandSource(this);
            }
            public bool TryGetCommand(uint tick, out PlayerCommand command)
            {
                command = new PlayerCommand(++_sequence, tick, Vector2.zero, default, default, default, default, default, default, default);
                return true;
            }
            public void Dispose()
            {
                if (_session != null) _session.AutoTakeoverOnFocusLoss = _autoTakeover;
                for (int i = 0; i < _actors.Length; i++) if (_actors[i] != null)
                { if (_original[i] == null) _actors[i].CommandDispatcher.ClearCommandSource(); else _actors[i].CommandDispatcher.TryBindCommandSource(_original[i]); }
            }
        }

        private sealed class ProfileIsolation : IDisposable
        {
            private readonly LocalPlayerProfileStore _profile;
            private readonly object _original;
            private readonly string _originalJson, _mainPath, _backupPath, _mainHash, _backupHash;
            private bool _restored;
            public ProfileIsolation(LocalPlayerProfileStore profile, AchievementDefinition[] definitions)
            {
                _profile = profile; _original = Get<object>(profile, "_data");
                Require(_original != null && definitions != null && definitions.Length > 0, "Missing initialized profile/achievement definitions.");
                _originalJson = JsonUtility.ToJson(_original);
                _mainPath = profile.SavePath;
                _backupPath = Path.Combine(Path.GetDirectoryName(_mainPath), "profile.backup.json");
                _mainHash = Hash(_mainPath); _backupHash = Hash(_backupPath);
                object clone = JsonUtility.FromJson(_originalJson, _original.GetType());
                var unlocked = Get<IList>(clone, "unlockedAchievements");
                var progress = Get<IList>(clone, "achievementProgress");
                Type recordType = progress.GetType().GetGenericArguments()[0];
                foreach (var definition in definitions)
                {
                    Require(definition != null && definition.TryValidate(out _), "Invalid achievement definition.");
                    if (!unlocked.Contains(definition.AchievementId)) unlocked.Add(definition.AchievementId);
                    object record = null;
                    foreach (object candidate in progress)
                        if (Get<string>(candidate, "triggerId") == definition.TriggerId) { record = candidate; break; }
                    if (record == null) { record = Activator.CreateInstance(recordType, true); Set(record, "triggerId", definition.TriggerId); progress.Add(record); }
                    Set(record, "count", Math.Max(Get<int>(record, "count"), definition.TargetCount));
                }
                // TryRecordAchievementEvent returns before TrySave when all matching targets are already satisfied.
                // Never call completion/product APIs: those have different write behavior.
                Set(profile, "_data", clone);
            }
            public void RestoreAndVerify()
            {
                Dispose();
                Require(ReferenceEquals(Get<object>(_profile, "_data"), _original) && JsonUtility.ToJson(_original) == _originalJson,
                    "Original in-memory profile changed.");
                Require(Hash(_mainPath) == _mainHash && Hash(_backupPath) == _backupHash, "Profile or backup file changed during fixture.");
                LastReport += "Profile isolation: original reference/JSON restored; main SHA256=" + _mainHash + "; backup SHA256=" + _backupHash + ".\n";
            }
            public void Dispose() { if (!_restored && _profile != null) { Set(_profile, "_data", _original); _restored = true; } }
            private static string Hash(string path)
            {
                if (!File.Exists(path)) return "ABSENT";
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
            }
        }

        private static T FindInScene<T>() where T : Component
        {
            T result = null;
            foreach (var value in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                if (value.gameObject.scene == SceneManager.GetActiveScene())
                { Require(result == null, "Multiple scene instances of " + typeof(T).Name); result = value; }
            return result;
        }
        private static T One<T>() where T : Component
        { T value = FindInScene<T>(); Require(value != null, "Missing scene " + typeof(T).Name); return value; }
        private static T Get<T>(object target, string name)
        { var field = target.GetType().GetField(name, Fields); Require(field != null, "Missing field " + name); return (T)field.GetValue(target); }
        private static void Set(object target, string name, object value)
        { var field = target.GetType().GetField(name, Fields); Require(field != null, "Missing field " + name); field.SetValue(target, value); }
        private static void Invoke(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, Fields); Require(method != null, "Missing method " + name);
            try { method.Invoke(target, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
        private static Exception Combine(Exception first, Exception second) => first == null ? second : new AggregateException(first, second);
        private static void Passed(MetaLevelDefinition level, string message) { LastReport += level.SceneName + ": " + message + ".\n"; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
