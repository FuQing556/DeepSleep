using System;
using System.Collections;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Meta;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static partial class ChapterFlowLifecycleChecks
    {
        public static string StartKimiSoak(int scenario = 0)
        {
            if (!EditorApplication.isPlaying || GameAppRoot.Instance == null || Running ||
                GameAppRoot.Instance.SceneRouter.IsTransitioning) return "Requires idle Boot/MainMenu Play.";
            var level = AssetDatabase.LoadAssetAtPath<MetaLevelDefinition>(LevelRoot + "World01_EarlyInternet.asset");
            Running = true; LastReport = ""; Status = "Starting Kimi full-upgrade AI observation";
            GameAppRoot.Instance.SceneRouter.StartCoroutine(KimiSoakGuard(level, scenario));
            return Status;
        }

        private static IEnumerator KimiSoakGuard(MetaLevelDefinition level, int scenario)
        {
            var router = GameAppRoot.Instance.SceneRouter;
            ProfileIsolation isolation = null;
            Exception error = null;
            float oldScale = Time.timeScale;
            bool background = Application.runInBackground;
            try
            {
                isolation = new ProfileIsolation(GameAppRoot.Instance.Profile, GameAppRoot.Instance.Achievements.Definitions);
                Application.runInBackground = true;
                yield return Drive(KimiSoak(router, level, scenario), failure => error = failure);
                Time.timeScale = 1;
                yield return Drive(Route(router, null), failure => error = failure);
                isolation.RestoreAndVerify();
                Status = error == null ? "Kimi observation complete; see measurements, not automatic balance approval." : "FAILED Kimi observation: " + error;
                LastReport = Status + "\n" + LastReport;
                Debug.Log(LastReport);
            }
            finally
            {
                isolation?.Dispose(); Time.timeScale = oldScale;
                Application.runInBackground = background; Running = false;
            }
        }

        private static IEnumerator KimiSoak(GameSceneRouter router, MetaLevelDefinition level, int scenario)
        {
            yield return Route(router, level);
            var chapter = One<ChapterRunController>();
            var session = chapter.LevelBindings.Session;
            Require(session.Selection.TrySelect(PlayerRole.DeepSeek), "Selection failed");
            yield return Until(() => chapter.Phase == ChapterRunPhase.Combat, "combat start");
            Set(chapter, "_metaRewardGranted", true);
            Set(chapter, "_segmentNumber", 4);
            Invoke(chapter, "StartCombatSegment");
            var ranks = One<PlayerUpgradeRuntimeState>();
            foreach (var definition in ranks.Catalog.Definitions)
                foreach (var role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                    if (definition.Supports(role))
                        for (int i = 0; i < definition.MaximumRank; i++) ranks.TryApply(role, definition.Id);
            session.SetLocalAi(true);
            var driver = One<KimiChapterEncounterDriver2D>();
            driver.Simulate(.02f); driver.Simulate(50.01f);
            var boss = driver.Encounter.Boss;
            int shieldHits = 0;
            if (scenario > 0)
            {
                var encounter = driver.Encounter;
                // 只固定测试的首招和阶段；玩家仍通过真实AI命令、武器、移动/碰撞参与。
                encounter.Moon.Cancel(); encounter.Prism.Cancel(); encounter.Laser.Cancel(); encounter.Ultimate.Cancel();
                if (scenario == 2)
                {
                    boss.SetVulnerable(true);
                    var damage = new DeepSleep.Runtime.Combat.Damage.DamagePacket(5000, boss.transform.position, Vector2.right, session.DeepSeek.gameObject);
                    boss.TryReceiveDamage(in damage); boss.CommitPhaseAtSkillBoundary();
                }
                Set(encounter, "<State>k__BackingField", KimiEncounterState.Casting);
                Set(encounter, "<CurrentSkill>k__BackingField", KimiSkill.Ultimate);
                encounter.Ultimate.Curtain.DamageAccepted += packet => shieldHits++;
                Require(encounter.Ultimate.Begin(scenario == 2, session, driver.Targets, driver.Perception), "Forced ultimate failed");
            }
            var numbers = One<CombatDamageNumberPresenter2D>();
            float start = Time.time, next = start, minimumHp = boss.CurrentHealth;
            int frames = 0, peakNumbers = 0; double frameSeconds = 0;
            Time.timeScale = 2;
            while (chapter.Phase == ChapterRunPhase.Combat && Time.time - start < (scenario == 0 ? 240 : 30))
            {
                minimumHp = Mathf.Min(minimumHp, boss.CurrentHealth);
                frames++; frameSeconds += Time.unscaledDeltaTime;
                peakNumbers = Mathf.Max(peakNumbers, numbers.ActiveCount);
                if (Time.time >= next)
                {
                    Status = "Kimi soak " + (Time.time-start).ToString("F1") + "s HP=" + boss.CurrentHealth.ToString("F0");
                    LastReport += Status + " skill=" + driver.Encounter.CurrentSkill + " casts=" + driver.Encounter.CastsStarted +
                        " DS=" + session.DeepSeekAi.Health.CurrentHealth.ToString("F1") + "/" + session.DeepSeekAi.Plan +
                        " HS=" + session.HarnessAi.Health.CurrentHealth.ToString("F1") + "/" + session.HarnessAi.Plan +
                        " curtain=" + driver.Encounter.Ultimate.Curtain.Remaining + " hits=" + shieldHits + "\n";
                    next += 10;
                }
                yield return null;
            }
            LastReport += "Result=" + chapter.Phase + "; duration=" + (Time.time-start).ToString("F1") +
                "; minBossHP=" + minimumHp + "; frames=" + frames + "; meanEditorFrameMs=" + (frameSeconds*1000/Math.Max(1,frames)).ToString("F1") +
                "; damageNumbers=" + numbers.PlayedCount + "; peakNumbers=" + peakNumbers + "; capacityDrops=" + numbers.CapacityDropCount +
                "; scenario=" + scenario + "; shieldHits=" + shieldHits + "\n";
        }
    }
}
