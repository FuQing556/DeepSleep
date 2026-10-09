using System;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只在隔离预览场景推进表现，不修改正式场景或真实存档。</summary>
    public static class ClaudePresentationChecks
    {
        [MenuItem("DeepSleep/Diagnostics/Claude Presentation")]
        private static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            try
            {
                ClaudeEncounterPresentation2D p = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<ClaudeEncounterPresentation2D>(true))
                    { Check(p == null, "Duplicate presentation"); p = item; }
                Check(p != null && p.TryValidateConfiguration(out _), "Assembly");
                Check(p.BackdropFadeSeconds == 3, "Approved 3s transition");
                Check(p.Backdrop.transform.parent == p.NightOverlay.transform.parent, "Background transform frame");
                Check(Vector3.Distance(p.Backdrop.sprite.bounds.size, p.NightSprite.bounds.size) < 0.001f, "Same panorama geometry");
                var original = p.Backdrop.sprite;
                p.ResetPresentation();
                Check(p.State == ClaudePresentationState.Hidden && !p.NightOverlay.enabled && p.Rain.Group.alpha == 0, "Initially hidden");
                p.BeginEntrance(); p.Advance(2.5f, false);
                Check(!p.FigureLayers[0].enabled && !p.IsEntranceComplete, "Figure only after night");
                float alpha = p.NightOverlay.color.a;
                p.BeginEntrance(); p.Advance(10, true);
                Check(p.NightOverlay.color.a == alpha, "Duplicate start and pause");
                p.Advance(0.5f, false);
                Check(p.State == ClaudePresentationState.Appearing && p.NightOverlay.color.a == 1, "3s transition");
                p.Advance(p.FigureFadeSeconds, false);
                Check(!p.IsEntranceComplete && p.FigureLayers[0].color.a == 1 && p.Barrier.EntranceAlpha == 0, "Figure before shield");
                p.Advance(p.Timing.ShieldSeconds, false);
                Check(!p.IsEntranceComplete && p.Barrier.EntranceAlpha == 1, "Shield before opening pause");
                p.Advance(p.Timing.ReadySeconds, false);
                Check(p.IsEntranceComplete, "Battle ready after opening pause");
                ClaudeBossActor2D actor = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<ClaudeBossActor2D>(true)) actor = item;
                Check(actor != null, "Actor explicitly present in scene");
                var transition = actor.PoseTransition;
                typeof(DeepSleep.Runtime.Presentation.Poses.SpritePoseTransition2D).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(transition, null);
                var ghost = (SpriteRenderer)new SerializedObject(transition).FindProperty("_ghostRenderer").objectReferenceValue;
                transition.ResetTo(actor.Config.Poses[(int)ClaudePose.Idle]);
                actor.SetPose(ClaudePose.Move);
                Check(ghost.enabled, "Ordinary movement still produces pose ghost");
                actor.SetPose(ClaudePose.Defeated);
                Check(!ghost.enabled, "Defeat clears old pose before settlement freezes game time");
                p.Rain.SetWeatherActive(true); p.Rain.Advance(1, false);
                Rect uv = p.Rain.Layers[0].Image.uvRect;
                p.BeginDeparture(); p.Advance(1.5f, true); p.Rain.Advance(0.3f, true);
                Check(Mathf.Abs(p.NightOverlay.color.a - 0.5f) < 0.0001f, "Settlement pause permits retreat");
                Check(!p.FigureLayers[0].enabled && p.Barrier.EntranceAlpha == 0, "Figure disappears at 1.5s and shield immediately");
                foreach (var layer in p.FigureLayers) Check(!layer.enabled, "Body and drone both disappear during paused settlement");
                Check(!ghost.enabled, "No frozen ghost remains after paused departure");
                Check(p.Rain.Layers[0].Image.uvRect == uv && p.Rain.Group.alpha < 1, "Rain fades but does not scroll during pause");
                alpha = p.NightOverlay.color.a; p.BeginDeparture();
                Check(p.NightOverlay.color.a == alpha, "Duplicate departure");
                p.Advance(1.5f, true);
                Check(p.State == ClaudePresentationState.Hidden && !p.NightOverlay.enabled &&
                    !p.FigureLayers[0].enabled && p.Rain.Group.alpha == 0, "Retreat cleanup");
                Check(p.Backdrop.sprite == original, "Original backdrop unchanged");
                p.BeginEntrance(); p.Advance(1.5f, false); alpha = p.NightOverlay.color.a;
                p.BeginDeparture(); p.Advance(0.001f, true);
                Check(p.NightOverlay.color.a <= alpha && !p.FigureLayers[0].enabled, "Interrupted entrance reverses without jump");
                p.ResetPresentation(); p.BeginEntrance(); p.Advance(p.Timing.EntranceSeconds, false);
                Check(p.IsEntranceComplete, "Large timestep consumes phase remainder");
                p.ResetPresentation();
                Check(!p.Rain.Group.blocksRaycasts && !p.Rain.Group.interactable, "Does not intercept UI");
                Check(p.FigureLayers[0].GetComponentsInChildren<Collider2D>().Length == 0 &&
                    p.FigureLayers[1].GetComponentsInChildren<Collider2D>().Length == 0, "Presentation has no physics");
                return $"Claude presentation: {checks} checks passed (isolated Editor, not Play/device verification).";
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
