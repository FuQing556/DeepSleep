using System;
using System.Linq;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class BossUnifiedPresentationChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var kiScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World01_EarlyInternet.unity");
            var clScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            try
            {
                var ki = kiScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KimiChapterEncounterDriver2D>(true)).Single();
                var cl = clScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ClaudeEncounter2D>(true)).Single();
                var kp = ki.Presentation; var cp = cl.Presentation; var timing = kp.Timing;
                var kh = ki.Hud;
                var ch = clScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHealthHudView>(true)).Single();
                Check(kh.Presentation == kp && ch.Barrier == cp.Barrier, "HUDs explicitly bind shield entrance source");
                Check(timing == cp.Timing && timing == ki.Encounter.Config.Timing && timing == cl.Config.Timing, "One explicit timing source");
                Check(timing.NightSeconds == 3 && timing.FigureSeconds == 1 && timing.ShieldSeconds == .25f &&
                    timing.ReadySeconds == .5f && timing.PhaseSeconds == 2 && timing.DepartureSeconds == 1.5f, "Approved timings");
                Check(ki.Encounter.Config.CastGapSeconds == 2 && ki.Encounter.Config.CycleGapSeconds == 5 &&
                    cl.Config.CastGapOne == 1.2f && cl.Config.CastGapTwo == .9f && cl.Config.CycleGapSeconds == 3, "Skill gaps unchanged");
                foreach (float speed in new[] { .5f, 1f, 2f })
                {
                    ki.StopCombat(ChapterCombatStopReason.SceneExit); kp.ResetPresentation();
                    kp.Boss.BeginAuthority(Vector2.zero, null, null); kp.AdvancePresentation(0, 0);
                    cl.Actor.BeginAuthority(null, null); cl.Actor.Body.SetVulnerable(false);
                    cp.ResetPresentation(); cp.BeginEntrance(); ki.ApplyReplica(true);
                    float age = 0;
                    foreach (float seconds in new[] { 2.5f, .5f, .5f, .5f, .125f, .125f, .49f, .01f })
                    {
                        age += seconds;
                        kp.AdvancePresentation(seconds * speed, seconds);
                        cp.Advance(seconds, false); cp.Barrier.Advance(0);
                        kh.RenderNow(); ch.RenderNow();
                        Check(Mathf.Abs(kh.Visibility.alpha - timing.ShieldAlpha(kp.EntryAge)) < .001f &&
                            Mathf.Abs(ch.Visibility.alpha - cp.Barrier.EntranceAlpha) < .001f,
                            "Both health bars fade in with shields, not bodies or hit feedback");
                        ki.AdvanceBackdrop(seconds, false);
                        Check(Mathf.Abs(kp.Boss.Body.color.a - cp.FigureLayers[0].color.a) < .001f, "Figure alpha matches at each real-time stage");
                        Check(Mathf.Abs((kp.MoonShield.enabled ? kp.MoonShield.color.a / kp.ShieldIdleAlpha : 0) - cp.Barrier.EntranceAlpha) < .001f,
                            "Shield alpha matches at each real-time stage");
                        Check(cp.IsEntranceComplete == (age >= timing.EntranceSeconds - .00001f), "First cast gate after 4.75 real seconds");
                        float figure = kp.Boss.Body.color.a, shield = cp.Barrier.EntranceAlpha;
                        kp.AdvancePresentation(0, 10); cp.Advance(10, true); ki.AdvanceBackdrop(10, true);
                        Check(kp.Boss.Body.color.a == figure && cp.Barrier.EntranceAlpha == shield, "Ordinary pause freezes entry");
                    }
                    kp.BeginDeparture(); cp.BeginDeparture();
                    kp.AdvancePresentation(0, 1.5f); cp.Advance(1.5f, true);
                    Check(!kp.DepartingBody.enabled && !kp.DepartingCloud.enabled && !cp.FigureLayers[0].enabled && !cp.FigureLayers[1].enabled,
                        "Both figures and carriers gone at 1.5 real seconds during settlement pause");
                    Check(cp.State == ClaudePresentationState.Departing && Mathf.Approximately(cp.NightOverlay.color.a, .5f), "Night remains halfway at figure departure");
                    cp.Advance(1.5f, true);
                    Check(cp.State == ClaudePresentationState.Hidden && !cp.NightOverlay.enabled, "Night restored at 3 real seconds");
                    cl.Actor.ResetActor(); kp.Boss.ResetEncounter(); kp.ResetPresentation();
                }
                kp.Boss.ApplyReplica(true, Vector2.zero, 10000, false, KimiPose.Idle);
                kp.ApplyReplicaEntry(4.5f); kp.AdvancePresentation(0, 0);
                Check(kp.Boss.Body.color.a == 1 && Mathf.Approximately(kp.MoonShield.color.a, .8f), "Kimi late join restores completed figure/shield, not a fresh fade");
                var frame = new ClaudeEncounterSnapshot { PresentationState = ClaudePresentationState.Appearing,
                    NightAlpha = 1, FigureAlpha = 1, ShieldAlpha = .5f };
                cp.ApplyReplica(frame);
                Check(cp.Barrier.EntranceAlpha == .5f && cp.FigureLayers[0].color.a == 1, "Claude snapshot explicitly restores shield stage");
                return checks + " unified boss presentation checks passed: .5/1/2x, pause, staged entry/exit, late-join sampling and unchanged skill gaps.";
            }
            finally { EditorSceneManager.ClosePreviewScene(clScene); EditorSceneManager.ClosePreviewScene(kiScene); }
        }
    }
}
